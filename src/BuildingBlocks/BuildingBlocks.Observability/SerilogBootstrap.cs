using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using BuildingBlocks.Observability.Middleware;

namespace BuildingBlocks.Observability;

/// <summary>
/// The one place structured logging is configured for the whole ecosystem. Every API
/// (ApiGateway, AccountingInventory.Api, InventoryService.Api) calls
/// <see cref="AddSharedLogging"/> from Program.cs instead of hand-rolling its own
/// Serilog pipeline — this keeps sinks, enrichers, and output format identical across
/// every service, which matters when you're correlating logs across a distributed
/// trace.
///
/// Output: structured JSON to the console (for container log collectors / OTel) AND a
/// daily rolling file under ./logs (for local debugging without a log aggregator).
/// Every log line is enriched with the service name, environment, machine name,
/// thread id, and — for anything logged during a request — the correlation id pushed
/// by <see cref="CorrelationIdMiddleware"/>.
/// </summary>
public static class SerilogBootstrap
{
    public static WebApplicationBuilder AddSharedLogging(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("Service", serviceName)
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Yarp", LogEventLevel.Warning)
                .WriteTo.Console(new CompactJsonFormatter())
                .WriteTo.File(
                    new CompactJsonFormatter(),
                    path: $"logs/{serviceName}-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    shared: true);
        });

        return builder;
    }

    /// <summary>
    /// Wires request logging (one structured line per HTTP request, including status
    /// code and elapsed time) on top of the correlation-id middleware. Call this after
    /// <see cref="CorrelationIdMiddlewareExtensions.UseCorrelationId"/> so the
    /// correlation id enrichment in LogContext is already active for each request's
    /// summary line.
    /// </summary>
    public static WebApplication UseSharedRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("CorrelationId", CorrelationIdMiddleware.GetCurrentCorrelationId(httpContext)!);
                diagnosticContext.Set("Host", httpContext.Request.Host.Value!);
                diagnosticContext.Set("Scheme", httpContext.Request.Scheme);

                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    diagnosticContext.Set("UserName", httpContext.User.Identity.Name ?? "unknown");
                }
            };
        });

        return app;
    }
}
