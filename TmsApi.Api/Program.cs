using TmsApi.Application.Interfaces.Repositories;
using TmsApi.Infrastructure.Repositories;
using TmsApi.Api;
using TmsApi.Api.ExceptionHandlers;
using TmsApi.Api.Middleware;
using Asp.Versioning;
using TmsApi.Data;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
using TmsApi.Infrastructure.ExternalServices;
using TmsApi.Domain.Entities;
using TmsApi.Application.Interfaces;
using FluentValidation;
using TmsApi.Application.Enrollments.Commands;
using System.Threading.RateLimiting;
using System.Threading.Channels;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthChecks.NpgSql;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TmsApi.Api.RateLimiting;
using TmsApi.Application.Transcripts;
using TmsApi.Infrastructure.Transcripts;
using TmsApi.Api.Hubs;
using TmsApi.Api.Workers;

var builder = WebApplication.CreateBuilder(args);

// ===============================
// Structured Logging (JSON)
// ===============================

builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.JsonWriterOptions = new() { Indented = false };
});

// ===============================
// OpenTelemetry
// ===============================

const string ServiceName = "tms-api";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: ServiceName,
        serviceVersion: "1.0.0"))
    .WithTracing(t => t
        .AddSource(ServiceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddMeter(ServiceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

// ===============================
// Polly v8 Resilience Pipeline
// ===============================

builder.Services.AddResiliencePipeline("certificate-api", pipeline =>
{
    pipeline
        // Outer: per-request hard timeout – protects against hangs
        .AddTimeout(TimeSpan.FromSeconds(5))
        // Middle: circuit breaker – protects against sustained outage
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(15),
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>(),
            OnOpened = args =>
            {
                Console.WriteLine("Circuit OPENED – stopping requests to certificate service");
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                Console.WriteLine("Circuit CLOSED – certificate service recovered");
                return ValueTask.CompletedTask;
            }
        })
        // Inner: retry with jitter – only for transient failures
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(500),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>(),
            OnRetry = args =>
            {
                Console.WriteLine(
                    $"Retry #{args.AttemptNumber} after {args.RetryDelay.TotalMilliseconds:F0}ms ({args.Outcome.Exception?.GetType().Name})");
                return ValueTask.CompletedTask;
            }
        });
});

builder.Services.AddHttpClient<ICertificateService, CertificateService>((sp, client) =>
{
    var baseUrl = sp.GetRequiredService<IConfiguration>().GetValue<string>("TmsApi:PublicBaseUrl")
        ?? "http://localhost:5000";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddHttpClient("SmsService", client =>
{
    client.BaseAddress = new Uri("https://sms.tms.internal");
})
.AddStandardResilienceHandler();

// ===============================
// Health Checks
// ===============================

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("alive"),
        tags: ["live"])
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("TmsDatabase")!,
        name: "postgres",
        tags: ["ready"]);

// ===============================
// Transcripts & Background Workers (Session 3)
// ===============================

builder.Services.AddSingleton<ITranscriptStatusStore, InMemoryTranscriptStatusStore>();

builder.Services.AddSingleton(Channel.CreateBounded<TranscriptRequest>(
    new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.Wait
    }));

builder.Services.AddHostedService<TranscriptWorker>();

// ===============================
// SignalR (Session 3)
// ===============================

builder.Services.AddSignalR();

// ===============================
// Rate Limiting
// ===============================

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter =
        PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var (partitionKey, tier) =
                ApiKeyResolver.Resolve(httpContext);

            return tier switch
            {
                ApiKeyTier.Paid =>
                    RateLimitPartition.GetTokenBucketLimiter(
                        $"paid:{partitionKey}",
                        _ => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = 200,
                            TokensPerPeriod = 100,
                            ReplenishmentPeriod =
                                TimeSpan.FromSeconds(10),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }),

                ApiKeyTier.Free =>
                    RateLimitPartition.GetTokenBucketLimiter(
                        $"free:{partitionKey}",
                        _ => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = 30,
                            TokensPerPeriod = 10,
                            ReplenishmentPeriod =
                                TimeSpan.FromSeconds(10),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }),

                _ =>
                    RateLimitPartition.GetTokenBucketLimiter(
                        $"anon:{partitionKey}",
                        _ => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = 10,
                            TokensPerPeriod = 5,
                            ReplenishmentPeriod =
                                TimeSpan.FromSeconds(10),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        })
            };
        });

    // ===============================
    // Transcript Concurrency Limiter
    // ===============================

    options.AddConcurrencyLimiter("transcripts", opt =>
    {
        opt.PermitLimit = 5;
        opt.QueueLimit = 20;
        opt.QueueProcessingOrder =
            QueueProcessingOrder.OldestFirst;
    });

    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType =
            "application/problem+json";

        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title = "Rate limit exceeded",
                Detail = "Too many requests.",
                Status = 429
            },
            ct);
    };
});

// ===============================
// Hybrid Cache
// ===============================

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };
});

// ===============================
// Controllers
// ===============================

builder.Services.AddControllers(options =>
{
    options.Filters.Add<TmsApi.Api.Filters.AuditLogFilter>();
});

// ===============================
// API Versioning
// ===============================

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;

    options.ApiVersionReader =
        new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ===============================
// Problem Details
// ===============================

builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ===============================
// OpenAPI
// ===============================

builder.Services.AddOpenApi();

// ===============================
// Services
// ===============================

builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ICachedCourseService, CachedCourseService>();

// ===============================
// Repositories
// ===============================

builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();

// ===============================
// Database
// ===============================

builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("TmsDatabase"))
    .LogTo(Console.WriteLine, LogLevel.Information)
    .EnableSensitiveDataLogging());

// ===============================
// Authentication
// ===============================

builder.Services
    .AddAuthentication("TestScheme")
    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
        "TestScheme",
        options => { });

builder.Services.AddAuthorization();

// ===============================
// Options
// ===============================

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ===============================
// MediatR
// ===============================

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(
        typeof(EnrollStudentCommand).Assembly);

    cfg.AddOpenBehavior(
        typeof(TmsApi.Application.Behaviors.LoggingBehavior<,>));

    cfg.AddOpenBehavior(
        typeof(TmsApi.Application.Behaviors.ValidationBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(
    typeof(EnrollStudentValidator).Assembly);

var app = builder.Build();

// ===============================
// SignalR Hub Endpoint (Session 3)
// ===============================

app.MapHub<TmsHub>("/hubs/tms");

// ===============================
// Health Check Endpoints
// ===============================

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
}).DisableRateLimiting();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).DisableRateLimiting();

// ===============================
// Lab-only Fake Certificate Service
// ===============================

var attempts = 0;
app.MapPost("/fake/certificates", async () =>
{
    var n = Interlocked.Increment(ref attempts);

    if (n % 7 == 0)
    {
        // Hang – simulates a downstream that accepted the request and never responded.
        await Task.Delay(TimeSpan.FromSeconds(20));
        return Results.Ok(new { Status = "issued", Attempt = n });
    }
    if (n % 3 != 0)
    {
        // Transient: 503 Service Unavailable
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    if (n % 11 == 0)
    {
        // Non-transient: 400 – Polly must NOT retry this
        return Results.BadRequest(new { error = "validation_failed" });
    }
    return Results.Ok(new { Status = "issued", Attempt = n });
}).WithTags("lab-fixtures");

// ===============================
// Middleware Pipeline
// ===============================

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseExceptionHandler();

app.UseMiddleware<V1DeprecationMiddleware>();

app.UseRouting();

// Rate limiter must be after routing
app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

// ===============================
// Development Tools
// ===============================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// ===============================
// Controllers
// ===============================

app.MapControllers();

// ===============================
// Error Test Route
// ===============================

app.MapGet("/api/error", () =>
{
    throw new TmsDatabaseException(
        "Simulated database failure for ProblemDetails testing");
});

// ===============================
// Database Seed
// ===============================

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