using Serilog.Context;

namespace FleetOps.Api.Middleware;

/// <summary>
/// Assigns every request a correlation id, echoes it back on the response, and pushes it
/// into the Serilog context so a single id ties together API logs, background-worker logs
/// and Lambda logs for the same operation.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var supplied)
                            && !string.IsNullOrWhiteSpace(supplied)
            ? supplied.ToString()
            : Guid.NewGuid().ToString("N");

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            logger.LogDebug("Handling {Method} {Path}", context.Request.Method, context.Request.Path);
            await next(context);
        }
    }
}
