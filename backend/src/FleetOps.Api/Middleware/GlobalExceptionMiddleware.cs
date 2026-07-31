using System.Diagnostics;
using FleetOps.Application.Common;
using FleetOps.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace FleetOps.Api.Middleware;

/// <summary>
/// Translates exceptions into RFC 7807 ProblemDetails. Domain and application exceptions
/// map to specific status codes; anything else is logged and returned as an opaque 500,
/// so internal details never leak to the client.
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var id)
            ? id?.ToString()
            : null;

        var (status, title, detail) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", exception.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            ValidationFailedException => (StatusCodes.Status400BadRequest, "Validation failed", exception.Message),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", exception.Message),
            OperationCanceledException => (HttpStatus.ClientClosedRequest, "Request cancelled", "The client cancelled the request."),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error",
                  "An unexpected error occurred. Quote the correlation id when reporting this."),
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);
        }
        else
        {
            logger.LogWarning("Request failed with {Status}: {Message}. CorrelationId={CorrelationId}",
                status, exception.Message, correlationId);
        }

        if (context.Response.HasStarted)
        {
            // Too late to rewrite the response; the log above is all we can do.
            return;
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
        };

        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        if (exception is ValidationFailedException validation)
        {
            problem.Extensions["errors"] = validation.Errors;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}

internal static class HttpStatus
{
    /// <summary>Not present in <see cref="StatusCodes"/>; nginx's "client closed request".</summary>
    public const int ClientClosedRequest = 499;
}
