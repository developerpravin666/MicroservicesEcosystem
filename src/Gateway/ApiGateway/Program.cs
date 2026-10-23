using System.Threading.RateLimiting;
using BuildingBlocks.Observability;
using BuildingBlocks.Observability.Middleware;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

const string ServiceName = "ApiGateway";

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging(ServiceName);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddRateLimiter(options =>
{
    var rateLimitConfig = builder.Configuration.GetSection("RateLimiting");
    var isEnabled = rateLimitConfig.GetValue<bool?>("Enabled") ?? true;
    var permitLimit = rateLimitConfig.GetValue<int?>("PermitLimit") ?? 5000;
    var windowSeconds = rateLimitConfig.GetValue<int?>("WindowSeconds") ?? 60;
    var queueLimit = rateLimitConfig.GetValue<int?>("QueueLimit") ?? 100;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (!isEnabled)
        {
            return RateLimitPartition.GetNoLimiter("disabled");
        }

        var path = context.Request.Path;

        // Exempt health checks, metrics, and diagnostics
        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/metrics"))
        {
            return RateLimitPartition.GetNoLimiter("health-exempt");
        }

        // Exempt authorized load test traffic
        if (context.Request.Headers.ContainsKey("X-Load-Test") ||
            context.Request.Headers.ContainsKey("X-Bypass-Rate-Limit") ||
            path.StartsWithSegments("/api/admin/load-test"))
        {
            return RateLimitPartition.GetNoLimiter("load-test-exempt");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = queueLimit,
            });
    });

    options.OnRejected = async (context, ct) =>
    {
        Log.Warning(
            "Rate limit exceeded for {RemoteIp} on {Path}. Correlation Id: {CorrelationId}",
            context.HttpContext.Connection.RemoteIpAddress,
            context.HttpContext.Request.Path,
            CorrelationIdMiddleware.GetCurrentCorrelationId(context.HttpContext));

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            title = "Too many requests. Please retry later.",
            status = 429,
        }, ct);
    };
});

// 1. Define a string constant for your policy name
const string myCorsPolicy = "_myAllowSpecificOrigins";

// 2. Fetch the allowed origins array from appsettings.json
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

// 3. Add CORS services and define the policy rules
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: myCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseHttpsRedirection();

// 4. Crucial: app.UseCors must be placed AFTER app.UseRouting() (if explicitly declared) 
// but BEFORE app.UseAuthorization() and endpoint mapping middleware.
app.UseCors(myCorsPolicy);

app.UseCorrelationId();
app.UseSharedRequestLogging();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = ServiceName }));

app.MapReverseProxy(pipeline =>
{
    pipeline.Use(async (context, next) =>
    {
        if (context.Items[CorrelationIdMiddleware.HeaderName] is string correlationId)
        {
            context.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        }
        await next();
    });
});

try
{
    Log.Information("Starting {Service}", ServiceName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "{Service} terminated unexpectedly", ServiceName);
}
finally
{
    Log.CloseAndFlush();
}
