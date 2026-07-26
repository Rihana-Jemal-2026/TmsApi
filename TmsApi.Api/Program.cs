using TmsApi.Application.Interfaces.Repositories;
using TmsApi.Infrastructure.Repositories;
using TmsApi.Api;
using TmsApi.Api.ExceptionHandlers;
using TmsApi.Api.Middleware;
using Asp.Versioning;
using TmsApi.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
using TmsApi.Domain.Entities;
using TmsApi.Application.Interfaces;
using FluentValidation;
using TmsApi.Application.Enrollments.Commands;

var builder = WebApplication.CreateBuilder(args);

// Controllers with audit filter
builder.Services.AddControllers(options =>
{
    options.Filters.Add<TmsApi.Api.Filters.AuditLogFilter>();
});
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ProblemDetails
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// OpenAPI
builder.Services.AddOpenApi();

// Services
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<ICourseService, CourseService>();

// CQRS Repositories
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();

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


builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(
        typeof(EnrollStudentCommand).Assembly);

    // Logging FIRST
    cfg.AddOpenBehavior(
        typeof(TmsApi.Application.Behaviors.LoggingBehavior<,>));

    // Validation SECOND
    cfg.AddOpenBehavior(
        typeof(TmsApi.Application.Behaviors.ValidationBehavior<,>));
});


builder.Services.AddValidatorsFromAssembly(
    typeof(EnrollStudentValidator).Assembly);


var app = builder.Build();

// Logging middleware (FIRST)
app.UseMiddleware<RequestLoggingMiddleware>();

// Exception handling
// Exception handling
app.UseExceptionHandler();
// V1 deprecation headers
app.UseMiddleware<V1DeprecationMiddleware>();

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