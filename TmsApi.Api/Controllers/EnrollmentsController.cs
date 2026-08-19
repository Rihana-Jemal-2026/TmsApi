using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Enrollments.Queries;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/enrollments")]
[Route("api/enrollments")]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
public class EnrollmentsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll()
    {
        var sampleEnrollments = new[]
        {
            new { id = "ENR-101", studentId = 101, courseId = 302, studentName = "Dawit Abebe", courseName = "CS302 Web Architecture", status = "Pending", submittedAt = "2026-08-18T10:00:00Z" },
            new { id = "ENR-102", studentId = 102, courseId = 302, studentName = "Liya Tadesse", courseName = "CS302 Web Architecture", status = "Approved", submittedAt = "2026-08-18T10:15:00Z" },
            new { id = "ENR-103", studentId = 103, courseId = 401, studentName = "Abeba Kebede", courseName = "CS401 Database Internals", status = "Pending", submittedAt = "2026-08-18T11:00:00Z" }
        };

        return Ok(sampleEnrollments);
    }

    [HttpPost("{id}/approve")]
    public IActionResult Approve(string id)
    {
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Enroll(
        EnrollStudentCommand command,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);

        return result.Match<IActionResult>(
            onSuccess: created =>
                CreatedAtAction(
                    nameof(GetSchedule),
                    new { studentId = created.StudentId },
                    created),

            onFailure: error =>
            {
                var status = error.Code switch
                {
                    "course_not_found" =>
                        StatusCodes.Status404NotFound,

                    "course_full" or "already_enrolled" =>
                        StatusCodes.Status409Conflict,

                    _ =>
                        StatusCodes.Status400BadRequest
                };

                return Problem(
                    statusCode: status,
                    title: "Enrollment rejected",
                    detail: error.Message,
                    type: $"https://tms.local/errors/{error.Code}");
            });
    }

    [HttpGet("{studentId}/schedule")]
    public async Task<IActionResult> GetSchedule(
        int studentId,
        CancellationToken ct)
    {
        var schedule = await mediator.Send(
            new GetStudentScheduleQuery(studentId),
            ct);

        return Ok(schedule);
    }
}