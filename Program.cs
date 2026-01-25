using DatingApp.Shared.Middleware;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SwipeService.Data;
using SwipeService.Extensions;
using SwipeService.Services;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Swipe Service API",
        Version = "v1",
        Description = "API documentation for the Swipe Service."
    });
    // var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    // var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    // c.IncludeXmlComments(xmlPath); // Disabled to prevent crash if XML file is missing
});

// Configure MySQL database
builder.Services.AddDbContext<SwipeContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 32))
    ));

// Register SwipeService
builder.Services.AddScoped<SwipeService.Services.SwipeService>();

// Register rate limiting service and configuration
var swipeLimitsConfig = new SwipeService.Models.SwipeLimitsConfiguration();
builder.Configuration.GetSection("SwipeLimits").Bind(swipeLimitsConfig);
builder.Services.AddSingleton(swipeLimitsConfig);
builder.Services.AddScoped<SwipeService.Services.IRateLimitService, SwipeService.Services.RateLimitService>();

// Register MatchmakingNotifier
builder.Services.AddHttpClient<MatchmakingNotifier>();

// Add CQRS with MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// Add FluentValidation
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

builder.Services.AddKeycloakAuthentication(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();
builder.Services.AddCorrelationIds();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Swipe Service API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

app.UseCorrelationIds();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();
