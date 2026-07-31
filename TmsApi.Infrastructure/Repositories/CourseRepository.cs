using Microsoft.EntityFrameworkCore;
using TmsApi.Application.Interfaces.Repositories;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Repositories;

public class CourseRepository(TmsDbContext context)
    : ICourseRepository
{
    public async Task<Course?> GetByCodeAsync(
        string code,
        CancellationToken ct)
    {
        return await context.Courses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(
                c => c.Code == code,
                ct);
    }
    public async Task<List<Course>> GetAllCoursesAsync(
    CancellationToken ct)
{
    return await context.Courses
        .Include(c => c.Enrollments)
        .ToListAsync(ct);
}
}