using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
namespace TmsApi.Controllers;

[ApiController]
[Route("api/assessments")]
public class AssessmentsController : ControllerBase
{
    [Authorize]
    [HttpGet("results")]
    public IActionResult GetResults()
    {
        return Ok(new
        {
            courseCode = "CS-101",
            studentId = "S-001",
            letterGrade = "A"
        });
    }
}