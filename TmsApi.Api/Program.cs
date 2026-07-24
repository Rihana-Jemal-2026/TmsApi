using TmsApi.Api;
using TmsApi.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
using TmsApi.Domain.Entities;
using TmsApi.Application.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Controllers with audit filter
builder.Services.AddControllers(options =>
{
    options.Filters.Add<TmsApi.Api.Filters.AuditLogFilter>();
});

// ProblemDetails
builder.Services.AddProblemDetails();

// OpenAPI
builder.Services.AddOpenApi();

// Services
builder.Services.AddSingleton<EnrollmentWorker>();
builder.Services.AddSingleton<IEnrollmentService_M4, EnrollmentService_M4>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();


builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("TmsDatabase"))
    .LogTo(Console.WriteLine, LogLevel.Information)
    .EnableSensitiveDataLogging());
// Authentication
builder.Services
    .AddAuthentication("TestScheme")
    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
        "TestScheme",
        options => { });

builder.Services.AddAuthorization();

// Options
builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddScoped<ICourseService, CourseService>();

var app = builder.Build();

// Logging middleware (FIRST)
app.UseMiddleware<RequestLoggingMiddleware>();

// Exception handling
app.UseExceptionHandler();

// Routing
app.UseRouting();

// Auth
app.UseAuthentication();
app.UseAuthorization();

// Development tools
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Controllers
app.MapControllers();

// Test error route
app.MapGet("/api/error", () =>
{
    throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
});

// Seed data in Development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    await DataSeeder.SeedAsync(context);
    var dbName = context.Database.GetDbConnection().Database;
var courseCount = await context.Courses.CountAsync();

Console.WriteLine($"DATABASE: {dbName}");
Console.WriteLine($"COURSES: {courseCount}");
}

app.Run();