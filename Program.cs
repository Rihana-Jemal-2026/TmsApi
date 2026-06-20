var builder = WebApplication.CreateBuilder(args);

// 1. Enable controllers
builder.Services.AddControllers();

// 2. Add authentication + authorization (IMPORTANT for M4)
builder.Services.AddAuthentication("TestScheme")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
               TestAuthHandler>("TestScheme", options => { });
builder.Services.AddAuthorization();

var app = builder.Build();

// 3. Middleware pipeline (ORDER MATTERS)

// Routing (maps request paths)
app.UseRouting();

// Authentication (WHO ARE YOU?)
app.UseAuthentication();

// Authorization (ARE YOU ALLOWED?)
app.UseAuthorization();

// Map controllers (endpoints)
app.MapControllers();

app.Run();