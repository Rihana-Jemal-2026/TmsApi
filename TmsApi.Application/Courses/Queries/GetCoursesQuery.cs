using MediatR;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces.Repositories;

namespace TmsApi.Application.Courses.Queries;

public record GetCoursesQuery(PagedRequest Paging) : IRequest<PagedResponse<CourseDto>>;

public class GetCoursesQueryHandler(ICourseRepository repository)
    : IRequestHandler<GetCoursesQuery, PagedResponse<CourseDto>>
{
    public async Task<PagedResponse<CourseDto>> Handle(
        GetCoursesQuery request,
        CancellationToken ct)
    {
        var allCourses = await repository.GetAllCoursesAsync(ct);

        var totalCount = allCourses.Count;
        var page = Math.Max(1, request.Paging.Page);
        var pageSize = Math.Clamp(request.Paging.PageSize, 1, 50);

        var items = allCourses
            .OrderBy(c => c.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CourseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count))
            .ToList();

        return new PagedResponse<CourseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
