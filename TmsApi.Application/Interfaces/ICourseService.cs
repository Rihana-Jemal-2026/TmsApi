using TmsApi.Domain.Entities;
using TmsApi.Application.DTOs;

namespace TmsApi.Application.Interfaces;

public interface ICourseService
{
    Task<Course?> GetByIdAsync(
        int id,
        CancellationToken ct);


    Task<CourseDetailDto?> GetDetailByIdAsync(
        int id,
        CancellationToken ct);


    Task<Course> CreateAsync(
        Course course,
        CancellationToken ct);


    Task<bool> CodeExistsAsync(
        string code,
        CancellationToken ct);


    Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(
        PagedRequest request,
        CancellationToken ct);
}