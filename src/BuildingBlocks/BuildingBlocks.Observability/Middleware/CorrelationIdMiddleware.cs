using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace BuildingBlocks.Observability.Middleware;

/// <summary>
/// Captures or generates the X-Correlation-ID header for every inbound request,
/// pushes it into Serilog's LogContext so every log line written during that request
/// carries it, and stashes it on HttpContext.Items so it can be propagated further
/// downstream — onto outbound HttpClient calls, YARP-proxied requests, or MassTransit
/// message headers.
///
/// This is the single implementation shared by ApiGateway, AccountingInventory.Api, and
/// InventoryService.Api (and any new service cloned from the Gold Master) via a
/// project reference to BuildingBlocks.Observability — it is written once here rather
/// than copy-pasted per service.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[HeaderName] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", context.TraceIdentifier))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var existing) &&
            !string.IsNullOrWhiteSpace(existing))
        {
            return existing.ToString();
        }

        var generated = Guid.NewGuid().ToString("N");
        context.Request.Headers[HeaderName] = generated;
        return generated;
    }

    /// <summary>
    /// Reads the correlation ID stashed on HttpContext.Items by this middleware. Used
    /// by outbound integrations (e.g. InventoryService.Infrastructure's MassTransit
    /// publish filter) that need to forward it without depending on this middleware
    /// type directly.
    /// </summary>
    public static string? GetCurrentCorrelationId(HttpContext? context)
        => context?.Items[HeaderName] as string;
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
