using System.Net;
using System.Text.Json;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next; _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (Exception ex)
        {
            var (status, message) = ex switch
            {
                DomainException => (HttpStatusCode.BadRequest, ex.Message),
                NotFoundException => (HttpStatusCode.NotFound, ex.Message),
                UnauthorizedException => (HttpStatusCode.Forbidden, ex.Message),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
            };

            if (status == HttpStatusCode.InternalServerError)
                _logger.LogError(ex, "Unhandled exception");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)status;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
        }
    }
}
