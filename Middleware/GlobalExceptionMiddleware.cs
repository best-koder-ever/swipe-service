using System.Net;
using System.Text.Json;
using SwipeService.Common;

namespace SwipeService.Middleware;

/// <summary>
/// Global exception handling middleware.
/// Catches all unhandled exceptions and returns structured ProblemDetails JSON.
/// Prevents stack trace leaks in production.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.TraceIdentifier;

        var (statusCode, errorCode, message) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, "NOT_FOUND", exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "UNAUTHORIZED", "Authentication required"),
            ArgumentException => (HttpStatusCode.BadRequest, "BAD_REQUEST", exception.Message),
            FluentValidation.ValidationException vex => (HttpStatusCode.BadRequest, "VALIDATION_ERROR",
                string.Join("; ", vex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))),
            InvalidOperationException => (HttpStatusCode.Conflict, "CONFLICT", exception.Message),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred")
        };

        _logger.LogError(exception,
            "Unhandled exception [{ErrorCode}] CorrelationId={CorrelationId} Path={Path} Method={Method}",
            errorCode, correlationId, context.Request.Path, context.Request.Method);

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var problemDetails = new ProblemDetailsResponse
        {
            Type = $"https://httpstatuses.com/{(int)statusCode}",
            Title = errorCode,
            Status = (int)statusCode,
            Detail = _env.IsDevelopment() ? exception.ToString() : message,
            Instance = context.Request.Path,
            CorrelationId = correlationId
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, options));
    }
}

/// <summary>
/// RFC 7807 Problem Details response with correlation ID extension.
/// </summary>
public class ProblemDetailsResponse
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string Instance { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// Extension method for clean middleware registration.
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
