using Microsoft.AspNetCore.Mvc;
using TmsApi.Dtos;
using TmsApi.Entities;
using TmsApi.Services;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses")]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;

    public CoursesController(ICourseService courseService)
    {
        _courseService = courseService;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CourseResponseDto>> GetById(
        int id,
        CancellationToken ct)
    {
        var course = await _courseService.GetByIdAsync(id, ct);

        if (course is null)
            return NotFound();

        return Ok(new CourseResponseDto
        {
            Id = course.Id,
            Code = course.Code,
            Title = course.Title,
            MaxCapacity = course.MaxCapacity,
            EnrollmentCount = course.Enrollments.Count
        });
    }

    [HttpPost]
public async Task<ActionResult<CourseResponseDto>> Create(
    CreateCourseRequestDto request,
    CancellationToken ct)
{
    // Check if a course with the same code already exists
    if (await _courseService.CodeExistsAsync(request.Code, ct))
    {
        return Conflict(new ProblemDetails
        {
            Title = "Course already exists",
            Detail = $"A course with code '{request.Code}' already exists.",
            Status = StatusCodes.Status409Conflict
        });
    }

    var course = new Course
    {
        Code = request.Code,
        Title = request.Title,
        MaxCapacity = request.MaxCapacity
    };

    var created = await _courseService.CreateAsync(course, ct);

    var response = new CourseResponseDto
    {
        Id = created.Id,
        Code = created.Code,
        Title = created.Title,
        MaxCapacity = created.MaxCapacity,
        EnrollmentCount = 0
    };

    return CreatedAtAction(
        nameof(GetById),
        new { id = created.Id },
        response);
}
}