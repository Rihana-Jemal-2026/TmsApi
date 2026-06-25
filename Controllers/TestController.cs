using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/test")]
public class TestController(TmsDbContext context) : ControllerBase
{
    // =========================
    // Exercise 1: Deferred Execution
    // =========================
    [HttpGet("deferred")]
    public IActionResult TestDeferred()
    {
        Console.WriteLine("\n>>> STEP 1: Building query (no DB call)");

        var query = context.Students.Where(s => s.GPA >= 3.0m);

        Console.WriteLine(">>> STEP 2: Adding ordering");

        var orderedQuery = query.OrderBy(s => s.Name);

        Console.WriteLine(">>> STEP 3: Executing query");

        var results = orderedQuery.ToList();

        Console.WriteLine(">>> STEP 4: Done\n");

        return Ok(results);
    }

    // =========================
    // Translation failure test
    // =========================
    private static bool IsHonorRoll(decimal gpa)
    {
        return gpa >= 3.5m;
    }

    [HttpGet("translation-fail")]
    public IActionResult TestTranslationFail()
    {
        Console.WriteLine("\n>>> STEP 1: Non-translatable query");

        try
        {
            var students = context.Students
                .Where(s => IsHonorRoll(s.GPA)) // ❌ cannot be translated
                .ToList();

            return Ok(students);
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>> ERROR: {ex.Message}");

            return BadRequest(new { Message = ex.Message });
        }
    }

    // =========================
    // Pagination (Exercise 3)
    // =========================
    [HttpGet("students")]
    public async Task<IActionResult> GetStudents(int page = 1)
    {
        const int pageSize = 20;

        var students = await context.Students
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(students);
    }

    // =========================
    // Top courses (Exercise 3)
    // =========================
    [HttpGet("top-courses")]
    public async Task<IActionResult> TopCourses()
    {
        var result = await context.Courses
            .Select(c => new
            {
                c.Title,
                EnrollmentCount = c.Enrollments.Count
            })
            .OrderByDescending(x => x.EnrollmentCount)
            .Take(5)
            .ToListAsync();

        return Ok(result);
    }

    // =========================
    // Exercise 7: N+1 problem
    // =========================
    [HttpGet("nplus1")]
    public async Task<IActionResult> TestNPlus1(CancellationToken cancellationToken)
    {
        var students = await context.Students
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var s in students)
        {
            var count = await context.Enrollments
                .AsNoTracking()
                .CountAsync(e => e.StudentId == s.Id, cancellationToken);

            Console.WriteLine($"{s.Name}: {count} enrollments");
        }

        return Ok();
    }

    // =========================
    // Exercise 7: Fixed version
    // =========================
    [HttpGet("nplus1-fixed")]
    public async Task<IActionResult> TestNPlus1Fixed(CancellationToken cancellationToken)
    {
        var report = await context.Students
            .AsNoTracking()
            .Select(s => new
            {
                s.Name,
                EnrollmentCount = s.Enrollments.Count
            })
            .ToListAsync(cancellationToken);

        foreach (var r in report)
        {
            Console.WriteLine($"{r.Name}: {r.EnrollmentCount} enrollments");
        }

        return Ok(report);
    }
}