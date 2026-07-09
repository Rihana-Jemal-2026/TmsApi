using TmsApi.Services;
using TmsApi.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/enrollments")]
public class EnrollmentsController : ControllerBase
{
   private readonly IEnrollmentService_M4 _service;

   public EnrollmentsController(IEnrollmentService_M4 service)
{
    _service = service;
}

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEnrollmentRequest request)
    {
        var result = await _service.EnrollAsync(request.StudentId, request.CourseCode);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }

    // ADD THIS HERE
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await _service.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}

public record CreateEnrollmentRequest(string StudentId, string CourseCode);