using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/grades")]
[Route("api/grades")]
[ApiVersion("1.0")]
public class GradesController : ControllerBase
{
    private readonly TmsDbContext _context;

    public GradesController(TmsDbContext context)
    {
        _context = context;
    }

    public record GradeSubmissionRequest(
        int StudentId,
        int CourseId,
        decimal Score,
        string? LetterGrade = null,
        string? FeedbackNote = null);

    private static string CalculateLetterGrade(decimal score) => score switch
    {
        >= 95 => "A+",
        >= 85 => "A",
        >= 75 => "B",
        >= 65 => "C",
        >= 50 => "D",
        _ => "F"
    };

    [HttpPost]
    public async Task<IActionResult> PostGrade([FromBody] GradeSubmissionRequest request)
    {
        var letterGrade = string.IsNullOrWhiteSpace(request.LetterGrade) || request.LetterGrade == "Unassigned"
            ? CalculateLetterGrade(request.Score)
            : request.LetterGrade;

        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == request.StudentId && e.CourseId == request.CourseId);

        if (enrollment != null)
        {
            enrollment.Grade = request.Score;
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            id = Guid.NewGuid().ToString("N"),
            success = true,
            studentId = request.StudentId,
            courseId = request.CourseId,
            score = request.Score,
            letterGrade,
            feedback = request.FeedbackNote
        });
    }

    [HttpPost("batch")]
    public async Task<IActionResult> BatchPostGrades([FromBody] List<GradeSubmissionRequest> requests)
    {
        if (requests == null || requests.Count == 0)
        {
            return BadRequest(new { message = "No grade submissions provided in batch payload." });
        }

        foreach (var req in requests)
        {
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.StudentId == req.StudentId && e.CourseId == req.CourseId);

            if (enrollment != null)
            {
                enrollment.Grade = req.Score;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            publishedCount = requests.Count,
            message = $"Successfully published grades for {requests.Count} student(s)."
        });
    }

    [HttpGet("{studentId:int}")]
    public async Task<IActionResult> GetStudentGrades(int studentId)
    {
        var enrollments = await _context.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == studentId)
            .Select(e => new
            {
                e.Id,
                e.CourseId,
                CourseCode = e.Course.Code,
                CourseTitle = e.Course.Title,
                Score = e.Grade,
                LetterGrade = e.Grade.HasValue ? CalculateLetterGrade(e.Grade.Value) : "Unassigned"
            })
            .ToListAsync();

        return Ok(enrollments);
    }
}
