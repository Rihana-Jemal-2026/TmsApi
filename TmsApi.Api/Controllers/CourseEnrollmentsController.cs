using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/enrollments")]
public class CourseEnrollmentsController : ControllerBase
{
    private readonly IEnrollmentService _service;

    public CourseEnrollmentsController(IEnrollmentService service)
    {
        _service = service;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EnrollmentResponseDto>> GetById(
        int courseId,
        int id,
        CancellationToken ct)
    {
        var enrollment = await _service.GetByIdAsync(courseId, id, ct);

        if (enrollment is null)
            return NotFound();

        return Ok(enrollment);
    }

    [HttpPost]
    public async Task<ActionResult<EnrollmentResponseDto>> Create(
        int courseId,
        EnrollStudentRequest request,
        CancellationToken ct)
    {
        var enrollment =
            await _service.CreateAsync(courseId, request, ct);

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                courseId,
                id = enrollment.Id
            },
            enrollment);
    }
}