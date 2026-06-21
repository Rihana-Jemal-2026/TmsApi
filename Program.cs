var builder = WebApplication.CreateBuilder(args);

// 1. Controllers
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<EnrollmentWorker>();
builder.Services.AddSingleton<IEnrollmentService, EnrollmentService>();

// 2. Authentication + Authorization
builder.Services.AddAuthentication("TestScheme")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
               TestAuthHandler>("TestScheme", options => { });

builder.Services.AddAuthorization();
builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();
var app = builder.Build();


// 3. OUTER LOGGING MIDDLEWARE (must be first)
app.UseMiddleware<RequestLoggingMiddleware>();

// 4. Exception handler (required by lab)
app.UseExceptionHandler("/error");

// 5. Routing
app.UseRouting();

// 6. Auth
app.UseAuthentication();
app.UseAuthorization();
app.UseExceptionHandler();

// 7. Endpoints
app.MapControllers();

app.MapGet("/api/error", () =>
{
    throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
});

app.Run();