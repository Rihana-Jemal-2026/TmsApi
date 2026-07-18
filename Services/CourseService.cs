using TmsApi.Dtos;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;

namespace TmsApi.Services;

public class CourseService(
    TmsDbContext context,
    ILogger<CourseService> logger)
    : ICourseService
{
    public async Task<Course?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
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
    // Start with a no-tracking IQueryable<Course>
    IQueryable<Course> query = context.Courses.AsNoTracking();

    // Apply search filter if provided
    if (!string.IsNullOrWhiteSpace(request.Search))
    {
        query = query.Where(c =>
            EF.Functions.ILike(c.Title, $"%{request.Search}%") ||
            EF.Functions.ILike(c.Code, $"%{request.Search}%"));
    }

    // Count BEFORE paging
    var totalCount = await query.CountAsync(ct);

    // Apply OrderBy based on request
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

    // Materialise: Skip, Take, and Select
    var items = await query
        .Skip((request.Page - 1) * request.PageSize)
        .Take(request.PageSize)
        .Select(c => new CourseResponseDto
        {
            Id = c.Id,
            Code = c.Code,
            Title = c.Title,
            MaxCapacity = c.MaxCapacity,
            EnrollmentCount = c.Enrollments.Count
        })
        .ToListAsync(ct);

    return new PagedResponse<CourseResponseDto>
    {
        Items = items,
        TotalCount = totalCount,
        Page = request.Page,
        PageSize = request.PageSize
    };
}
}