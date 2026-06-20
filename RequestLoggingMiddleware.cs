using System.Diagnostics;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation($"START Request {context.Request.Method} {context.Request.Path} | ID: {correlationId}");

        await _next(context);

        stopwatch.Stop();

        _logger.LogInformation($"END Request {context.Request.Method} {context.Request.Path} | ID: {correlationId} | Status: {context.Response.StatusCode} | Time: {stopwatch.ElapsedMilliseconds}ms");
    }
}