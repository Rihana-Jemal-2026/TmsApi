using Microsoft.AspNetCore.Mvc;
using TmsApi.Dtos;
using TmsApi.Services;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/enrollments")]
[Tags("Enrollments")]
[Produces("application/json")]
public class EnrollmentsController(
    ICourseService courseService,
    IEnrollmentService enrollmentService) : ControllerBase
{

    // GET /api/courses/{courseId}/enrollments
    [HttpGet(Name = "ListCourseEnrollments")]
    public async Task<IActionResult> GetEnrollments(
        int courseId,
        CancellationToken ct)
    {
        var course = await courseService.GetByIdAsync(courseId, ct);

        if (course is null)
            return NotFound();

        var enrollments = await enrollmentService.GetByCourseAsync(courseId, ct);

        return Ok(enrollments);
    }


    // GET /api/courses/{courseId}/enrollments/{id}
    [HttpGet("{id:int}", Name = nameof(GetEnrollment))]
    public async Task<IActionResult> GetEnrollment(
        int courseId,
        int id,
        CancellationToken ct)
    {
        var enrollment = await enrollmentService.GetByIdAsync(
            courseId,
            id,
            ct);

        if (enrollment is null)
            return NotFound();

        return Ok(enrollment);
    }


    // POST /api/courses/{courseId}/enrollments
    [HttpPost]
    public async Task<IActionResult> EnrollStudent(
        int courseId,
        EnrollStudentRequest request,
        CancellationToken ct)
    {
        var course = await courseService.GetByIdAsync(courseId, ct);

        if (course is null)
            return NotFound();

        var result = await enrollmentService.CreateAsync(
            courseId,
            request,
            ct);

        return CreatedAtAction(
            nameof(GetEnrollment),
            new
            {
                courseId,
                id = result.Id
            },
            result);
    }
}