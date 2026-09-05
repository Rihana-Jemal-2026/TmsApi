using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/certificates")]
[Route("api/certificates")]
[ApiVersion("1.0")]
public class CertificatesController : ControllerBase
{
    private static readonly List<object> SampleCertificates = new()
    {
        new
        {
            id = "CERT-2026-002",
            certificateNumber = "TMS-CS401-4410",
            studentId = 1002,
            studentName = "Alemu Tadesse",
            courseId = 401,
            courseCode = "CS401",
            courseTitle = "Database Internals & Distributed Storage",
            issueDate = "2026-07-20T14:30:00Z",
            grade = "A",
            gpa = 3.9,
            instructorName = "Prof. Elena Rostova",
            verificationCode = "VER-CS401-ALEMU-331",
            skillsAcquired = new[] { "Query Optimization", "B-Trees & LSM", "Transactions", "PostgreSQL" }
        }
    };

    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(SampleCertificates);
    }

    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var cert = SampleCertificates.FirstOrDefault(c => 
            ((dynamic)c).id == id || ((dynamic)c).certificateNumber == id);
            
        if (cert == null)
        {
            return NotFound(new { message = $"Certificate with ID '{id}' not found." });
        }

        return Ok(cert);
    }

    [HttpGet("verify/{verificationCode}")]
    public IActionResult Verify(string verificationCode)
    {
        var cert = SampleCertificates.FirstOrDefault(c => 
            ((dynamic)c).verificationCode.Equals(verificationCode, StringComparison.OrdinalIgnoreCase));

        if (cert == null)
        {
            return NotFound(new { 
                valid = false, 
                message = "Verification code invalid or certificate not found." 
            });
        }

        return Ok(new {
            valid = true,
            verificationCode,
            certificate = cert,
            verifiedAt = DateTime.UtcNow
        });
    }

    [HttpPost("issue")]
    public IActionResult Issue([FromBody] IssueCertificateDto request)
    {
        var newCert = new
        {
            id = $"CERT-2026-{Random.Shared.Next(100, 999)}",
            certificateNumber = $"TMS-{request.CourseCode}-{Random.Shared.Next(1000, 9999)}",
            studentId = request.StudentId,
            studentName = request.StudentName,
            courseId = request.CourseId,
            courseCode = request.CourseCode,
            courseTitle = request.CourseTitle,
            issueDate = DateTime.UtcNow.ToString("o"),
            grade = request.Grade ?? "A",
            gpa = 4.0,
            instructorName = request.InstructorName ?? "Dr. Marcus Vance",
            verificationCode = $"VER-{request.CourseCode}-{request.StudentId}-{Random.Shared.Next(100, 999)}",
            skillsAcquired = request.Skills ?? new[] { "Course Mastery", "Technical Competency" }
        };

        SampleCertificates.Add(newCert);

        return CreatedAtAction(nameof(GetById), new { id = newCert.id }, newCert);
    }
}

public record IssueCertificateDto(
    int StudentId,
    string StudentName,
    int CourseId,
    string CourseCode,
    string CourseTitle,
    string? Grade,
    string? InstructorName,
    string[]? Skills
);
