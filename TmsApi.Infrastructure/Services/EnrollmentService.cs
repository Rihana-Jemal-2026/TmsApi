using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TmsApi.Infrastructure.Services;

public class EnrollmentService(
    TmsDbContext context,
    ILogger<EnrollmentService> logger)
    : IEnrollmentService
{
    public Task<EnrollmentResponseDto?> GetByIdAsync(
        int courseId,
        int id,
        CancellationToken ct)
    {
        return context.Enrollments
            .AsNoTracking()
            .Where(e => e.Id == id && e.CourseId == courseId)
            .Select(e => new EnrollmentResponseDto(
                e.Id,
                e.CourseId,
                e.StudentId,
                e.EnrolledAt))
            .FirstOrDefaultAsync(ct);
    }


    public async Task<EnrollmentResponseDto> CreateAsync(
        int courseId,
        EnrollStudentRequest request,
        CancellationToken ct)
    {
        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = request.StudentId,
            EnrolledAt = DateTime.UtcNow,
            Grade = 0
        };

        context.Enrollments.Add(enrollment);

        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Student {StudentId} enrolled in Course {CourseId}",
            request.StudentId,
            courseId);

        return await GetByIdAsync(
            courseId,
            enrollment.Id,
            ct)
            ?? throw new InvalidOperationException(
                "Enrollment not found after creation.");
    }


    // M6 Session 3 - List enrollments for a course
    public async Task<IReadOnlyList<EnrollmentResponseDto>> GetByCourseAsync(
        int courseId,
        CancellationToken ct)
    {
        return await context.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .Select(e => new EnrollmentResponseDto(
                e.Id,
                e.CourseId,
                e.StudentId,
                e.EnrolledAt))
            .ToListAsync(ct);
    }
}