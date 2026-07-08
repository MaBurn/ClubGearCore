using System.Net;
using System.Text.Json;
using ClubGear.Services;
using ClubGear.Services.Abstractions;

namespace ClubGear.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IEventLogService eventLogService)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unbehandelte Exception in Request-Pipeline");

            await eventLogService.LogErrorAsync(
                category: "UnhandledException",
                message: ex.Message,
                details: new
                {
                    exceptionType = ex.GetType().Name,
                    stackTrace = ex.StackTrace,
                    inner = ex.InnerException?.Message
                },
                requestId: context.TraceIdentifier,
                path: context.Request.Path,
                method: context.Request.Method,
                userName: context.User.Identity?.Name);

            await WriteErrorResponseAsync(context, ex);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, Exception ex)
    {
        var isApiRequest = context.Request.Path.StartsWithSegments("/api") ||
                           context.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

        var (statusCode, message) = MapException(ex);
        context.Response.StatusCode = statusCode;

        if (isApiRequest)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = new
                {
                    message,
                    statusCode,
                    requestId = context.TraceIdentifier,
                    timestamp = DateTime.UtcNow
                }
            }));
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        var html = $"<!DOCTYPE html><html lang='de'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Fehler</title><link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet'></head><body><div class='container py-5'><div class='alert alert-danger'><h1 class='h4 mb-3'>Es ist ein Fehler aufgetreten</h1><p>{WebUtility.HtmlEncode(message)}</p><p class='small text-muted mb-0'>Request-ID: {WebUtility.HtmlEncode(context.TraceIdentifier)}</p></div><a href='/' class='btn btn-primary'>Zur Startseite</a></div></body></html>";
        await context.Response.WriteAsync(html);
    }

    private static (int statusCode, string message) MapException(Exception ex)
    {
        return ex switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, ex.Message),
            BusinessLogicException => (StatusCodes.Status400BadRequest, ex.Message),
            NotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            UserFriendlyException => (StatusCodes.Status400BadRequest, ex.Message),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Zugriff verweigert."),
            TimeoutException => (StatusCodes.Status408RequestTimeout, "Zeitueberschreitung bei der Anfrage."),
            _ => (StatusCodes.Status500InternalServerError, "Ein unerwarteter Fehler ist aufgetreten.")
        };
    }
}
