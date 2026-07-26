using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces.Repositories;

public interface ICourseRepository
{
    Task<Course?> GetByCodeAsync(
        string code,
        CancellationToken ct);
}