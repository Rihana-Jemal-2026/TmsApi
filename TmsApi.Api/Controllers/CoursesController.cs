using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using Microsoft.AspNetCore.Routing;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Controllers;

[Authorize(Roles = "Instructor,Admin")]
[ApiController]
[Route("api/courses")]
[Tags("Courses")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;
    private readonly LinkGenerator _linkGenerator;
    private readonly TmsDbContext _context;
    private readonly IAuthorizationService _authorizationService;

    public CoursesController(
        ICourseService courseService,
        LinkGenerator linkGenerator,
        TmsDbContext context,
        IAuthorizationService authorizationService)
    {
        _courseService = courseService;
        _linkGenerator = linkGenerator;
        _context = context;
        _authorizationService = authorizationService;
    }
[HttpGet]
[AllowAnonymous]
[ProducesResponseType(typeof(PagedResponse<CourseResponseDto>), StatusCodes.Status200OK)]
[EndpointSummary("List courses with pagination")]
[EndpointDescription("Returns a paginated, optionally filtered list of TMS courses. PageSize is capped at 50.")]
public async Task<IActionResult> GetCourses(
        [FromQuery] PagedRequest request,
        CancellationToken ct)
    {
        var result = await _courseService.GetCoursesAsync(request, ct);
        return Ok(result);
    }

[HttpGet("{id:int}", Name = nameof(GetById))]
[AllowAnonymous]
[ProducesResponseType(typeof(CourseDetailDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[EndpointSummary("Get a course by ID")]
[EndpointDescription("Returns course details with HATEOAS links. Returns 404 if the course does not exist.")]
public async Task<IActionResult> GetById(
    int id,
    CancellationToken ct)
{
    var course = await _courseService.GetByIdAsync(id, ct);

    if (course is null)
        return NotFound();


    var enrollmentsLink = _linkGenerator.GetPathByAction(
        HttpContext,
        action: "GetEnrollments",
        controller: "Enrollments",
        values: new { courseId = id });


    var links = new List<LinkDto>
    {
        new(
            _linkGenerator.GetPathByName(
                HttpContext,
                nameof(GetById),
                new { id })!,
            "self",
            "GET"),

        new(
            _linkGenerator.GetPathByName(
                HttpContext,
                nameof(GetById),
                new { id })!,
            "update",
            "PUT"),

        new(
            _linkGenerator.GetPathByName(
                HttpContext,
                nameof(GetById),
                new { id })!,
            "delete",
            "DELETE"),

        new(
            enrollmentsLink!,
            "enrollments",
            "GET")
    };


    if (course.Enrollments.Count < course.MaxCapacity)
    {
        links.Add(
            new(
                enrollmentsLink!,
                "enroll",
                "POST"));
    }


    var result = new CourseDetailDto
    {
        Id = course.Id,
        Code = course.Code,
        Title = course.Title,
        MaxCapacity = course.MaxCapacity,
        EnrollmentCount = course.Enrollments.Count,
        Links = links
    };


    return Ok(result);
}

[HttpPost]
[ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
[EndpointSummary("Create a new course")]
[EndpointDescription("Creates a course with a unique code. Returns 409 if the course code already exists.")]
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

    public record UpdateCourseDto(string Title);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCourse(int id, [FromBody] UpdateCourseDto dto)
    {
        var course = await _context.Courses.FindAsync(id);
        if (course == null) return NotFound();

        var authResult = await _authorizationService.AuthorizeAsync(User, course, "CanEditCourse");
        if (!authResult.Succeeded)
        {
            return Forbid(); // 403 Forbidden when caller doesn't own the resource
        }

        course.Title = dto.Title;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}