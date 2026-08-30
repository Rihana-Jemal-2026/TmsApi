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
using TmsApi.Domain.Entities;
using TmsApi.Application.Interfaces;
using FluentValidation;
using TmsApi.Application.Enrollments.Commands;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Api.RateLimiting;
using Microsoft.AspNetCore.Identity;
using TmsApi.Api.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using TmsApi.Api.Authorization;

var builder = WebApplication.CreateBuilder(args);



// ===============================
// CORS Policy
// ===============================

var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("TmsClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});



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

    options.AddFixedWindowLimiter("AuthLimiter", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
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

builder.Services.AddScoped<CryptoDemoService>();
builder.Services.AddScoped<TokenService>();

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
// ASP.NET Core Identity
// ===============================

builder.Services.AddIdentityCore<TmsUser>(options =>
{
    // Enterprise Password Policy
    options.Password.RequiredLength = 12;
    options.Password.RequireUppercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;
    // Brute-Force Lockout Protection
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<TmsDbContext>();



// ===============================
// Authentication
// ===============================

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});


builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CanEditCourse", policy =>
        policy.Requirements.Add(new CourseInstructorRequirement()));

builder.Services.AddSingleton<IAuthorizationHandler, CourseInstructorHandler>();

// ===============================
// Antiforgery (XSRF Protection)
// ===============================

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
});

builder.Services.AddSignalR();





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
// Middleware Pipeline
// ===============================

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';");
    await next();
});


app.UseMiddleware<RequestLoggingMiddleware>();


app.UseExceptionHandler();

app.UseStatusCodePages();



app.UseMiddleware<V1DeprecationMiddleware>();


app.UseRouting();

app.UseCors("TmsClient");



// Rate limiter must be after routing
app.UseRateLimiter();


app.UseAuthentication();

app.UseAuthorization();

// Issue readable XSRF-TOKEN cookie for authenticated/session users
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true || context.Request.Cookies.ContainsKey("tms_auth"))
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, // MUST be false so Angular JavaScript can read it!
            Secure = !app.Environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict
        });
    }
    await next(context);
});




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

app.MapHub<TmsHub>("/hubs/tms").RequireCors("TmsClient");




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

    var context =
        scope.ServiceProvider.GetRequiredService<TmsDbContext>();

    await DataSeeder.SeedAsync(context);


    var dbName =
        context.Database.GetDbConnection().Database;


    var courseCount =
        await context.Courses.CountAsync();


    Console.WriteLine($"DATABASE: {dbName}");

    Console.WriteLine($"COURSES: {courseCount}");
}



app.Run();