using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Application.Interfaces.Repositories;
using TmsApi.Infrastructure.Caching;

namespace TmsApi.Infrastructure.Services;

public class CachedCourseService(
    HybridCache cache,
    ICourseRepository repo,
    ILogger<CachedCourseService> logger)
    : ICachedCourseService
{
    public async Task<CourseDetailDto> GetCourseAsync(
        string code,
        CancellationToken ct)
    {
        var key = CacheKeys.Course(code);
        var dbHit = false;

        var dto = await cache.GetOrCreateAsync(
            key,
            (repo, code),
            async (state, token) =>
            {
                dbHit = true;

                logger.LogInformation(
                    "Cache MISS for {Key} - fetching from DB",
                    key);

                var course = await state.repo.GetByCodeAsync(
                    state.code,
                    token);

                if (course is null)
                {
                    throw new Exception(
                        $"Course {state.code} not found.");
                }

                return new CourseDetailDto
                {
                    Id = course.Id,
                    Code = course.Code,
                    Title = course.Title,
                    MaxCapacity = course.MaxCapacity,
                    EnrollmentCount = course.Enrollments.Count,
                    Links = []
                };
            },
            tags: [CacheKeys.CoursesTag],
            cancellationToken: ct);

        if (dbHit)
        {
            TmsMeters.CacheMisses.Add(1, new KeyValuePair<string, object?>("key.kind", "course"));
        }
        else
        {
            TmsMeters.CacheHits.Add(1, new KeyValuePair<string, object?>("key.kind", "course"));
            logger.LogInformation(
                "Cache HIT for {Key}",
                key);
        }

        return dto;
    }

    public async Task<List<CourseResponseDto>> GetAllCoursesAsync(
        CancellationToken ct)
    {
        var key = CacheKeys.CoursesAll;
        var dbHit = false;

        var courses = await cache.GetOrCreateAsync(
            key,
            repo,
            async (state, token) =>
            {
                dbHit = true;

                logger.LogInformation(
                    "Cache MISS for {Key} - fetching from DB",
                    key);

                var items = await state.GetAllCoursesAsync(token);

                return items.Select(c => new CourseResponseDto
                {
                    Id = c.Id,
                    Code = c.Code,
                    Title = c.Title,
                    MaxCapacity = c.MaxCapacity,
                    EnrollmentCount = c.Enrollments.Count
                }).ToList();
            },
            tags: [CacheKeys.CoursesTag],
            cancellationToken: ct);

        if (dbHit)
        {
            TmsMeters.CacheMisses.Add(1, new KeyValuePair<string, object?>("key.kind", "course"));
        }
        else
        {
            TmsMeters.CacheHits.Add(1, new KeyValuePair<string, object?>("key.kind", "course"));
            logger.LogInformation(
                "Cache HIT for {Key}",
                key);
        }

        return courses;
    }

    public async Task InvalidateCourseCacheAsync(
        CancellationToken ct)
    {
        logger.LogInformation(
            "Invalidating cache tag {Tag}",
            CacheKeys.CoursesTag);

        await cache.RemoveByTagAsync(
            CacheKeys.CoursesTag,
            ct);
    }
}