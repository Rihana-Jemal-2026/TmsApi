using MediatR;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces.Repositories;

namespace TmsApi.Application.Courses.Queries;

public record GetCourseQuery(string Code) : IRequest<CourseDto?>;

public class GetCourseQueryHandler(ICourseRepository repository)
    : IRequestHandler<GetCourseQuery, CourseDto?>
{
    public async Task<CourseDto?> Handle(
        GetCourseQuery request,
        CancellationToken ct)
    {
        var course = await repository.GetByCodeAsync(request.Code, ct);
        if (course is null && int.TryParse(request.Code, out var id))
        {
            var all = await repository.GetAllCoursesAsync(ct);
            course = all.FirstOrDefault(c => c.Id == id);
        }

        if (course is null) return null;

        return new CourseDto(
            course.Id,
            course.Code,
            course.Title,
            course.MaxCapacity,
            course.Enrollments.Count);
    }
}
