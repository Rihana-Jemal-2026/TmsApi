using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/v2/transcripts")]
public class TranscriptsController : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("transcripts")]
    public async Task<IActionResult> RequestTranscript(
        [FromBody] object? request)
    {
        Console.WriteLine("Transcript START");

        // simulate long transcript job
        await Task.Delay(10000);

        Console.WriteLine("Transcript END");

        return Ok();
    }
}