using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TmsApi.Infrastructure.Services;

public class CourseService(
    TmsDbContext context,
    ILogger<CourseService> logger,
    ICachedCourseService cachedCourseService)
    : ICourseService
{

    // V1 - existing endpoint uses this
    public async Task<Course?> GetByIdAsync(
        int id,
        CancellationToken ct)
    {
        return await context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }



    // V2 - detail endpoint with HATEOAS links
    public async Task<CourseDetailDto?> GetDetailByIdAsync(
        int id,
        CancellationToken ct)
    {
        var course = await context.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseDetailDto
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                MaxCapacity = c.MaxCapacity,
                EnrollmentCount = c.Enrollments.Count,

                Links = new List<LinkDto>
                {
                    new LinkDto(
                        $"/api/v2/courses/{c.Id}",
                        "self",
                        "GET"
                    ),

                    new LinkDto(
                        $"/api/v2/courses/{c.Id}/enroll",
                        "enroll",
                        "POST"
                    )
                }
            })
            .FirstOrDefaultAsync(ct);


        return course;
    }



    public async Task<Course> CreateAsync(
        Course course,
        CancellationToken ct)
    {
        context.Courses.Add(course);

        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Course created: {Code}",
            course.Code);

        return course;
    }



    public async Task<bool> CodeExistsAsync(
        string code,
        CancellationToken ct)
    {
        return await context.Courses
            .AnyAsync(c => c.Code == code, ct);
    }



    public async Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(
        PagedRequest request,
        CancellationToken ct)
    {
        var courses = await cachedCourseService
            .GetAllCoursesAsync(ct);

        var query = courses.AsQueryable();


        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(c =>
                c.Title.Contains(
                    request.Search,
                    StringComparison.OrdinalIgnoreCase)
                ||
                c.Code.Contains(
                    request.Search,
                    StringComparison.OrdinalIgnoreCase));
        }


        var totalCount = query.Count();


        query = request.OrderBy switch
        {
            "Code" => request.Descending
                ? query.OrderByDescending(c => c.Code)
                : query.OrderBy(c => c.Code),

            "MaxCapacity" => request.Descending
                ? query.OrderByDescending(c => c.MaxCapacity)
                : query.OrderBy(c => c.MaxCapacity),

            _ => request.Descending
                ? query.OrderByDescending(c => c.Title)
                : query.OrderBy(c => c.Title)
        };


        var items = query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CourseResponseDto
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                MaxCapacity = c.MaxCapacity,
                EnrollmentCount = c.EnrollmentCount
            })
            .ToList();


        return new PagedResponse<CourseResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}