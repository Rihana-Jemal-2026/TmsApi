using TmsApi.Entities;

namespace TmsApi.Services;

public interface ICourseService
{
    Task<Course?> GetByIdAsync(int id, CancellationToken ct);

    Task<Course> CreateAsync(Course course, CancellationToken ct);

    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
}