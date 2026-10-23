using AccountingInventory.Api.LoadTesting;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class LoadTestEndpoints
{
    public static RouteGroupBuilder MapLoadTestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/load-test")
            .WithTags("Load Testing & Benchmarking")
            .AllowAnonymous();

        // 1. Get predefined scenarios
        group.MapGet("/scenarios", (ILoadTestRunner runner) =>
            Results.Ok(runner.GetScenarios()))
            .WithName("GetLoadTestScenarios")
            .Produces<IReadOnlyList<LoadTestScenario>>();

        // 2. Get current active test telemetry
        group.MapGet("/current", (ILoadTestRunner runner) =>
        {
            var current = runner.GetCurrentTest();
            return Results.Ok(new
            {
                hasActiveTest = current is not null && current.Status == LoadTestStatus.Running,
                current
            });
        })
        .WithName("GetCurrentLoadTest")
        .Produces<object>();

        // 3. Start a new load test
        group.MapPost("/start", async (
            [FromBody] LoadTestConfig config,
            ILoadTestRunner runner,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            // If user did not provide an explicit auth token, try borrowing Bearer token from the incoming admin request
            if (string.IsNullOrWhiteSpace(config.AuthToken))
            {
                var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    config.AuthToken = authHeader.Substring(7).Trim();
                }
            }

            try
            {
                var summary = await runner.StartTestAsync(config, ct);
                return Results.Ok(new
                {
                    success = true,
                    message = $"Load test '{summary.ScenarioName}' initiated successfully.",
                    runId = summary.RunId,
                    summary
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Failed to start load test",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("StartLoadTest")
        .Produces<object>()
        .ProducesProblem(StatusCodes.Status409Conflict);

        // 4. Stop / Abort running load test
        group.MapPost("/stop", (ILoadTestRunner runner) =>
        {
            var stopped = runner.StopActiveTest();
            return Results.Ok(new
            {
                success = stopped,
                message = stopped ? "Load test aborted successfully." : "No active load test was running."
            });
        })
        .WithName("StopLoadTest")
        .Produces<object>();

        // 5. Get historical test runs
        group.MapGet("/history", (ILoadTestRunner runner) =>
            Results.Ok(runner.GetHistory()))
            .WithName("GetLoadTestHistory")
            .Produces<IReadOnlyList<LoadTestRunSummary>>();

        // 6. Get single historical test run by id
        group.MapGet("/history/{id:guid}", (Guid id, ILoadTestRunner runner) =>
        {
            var item = runner.GetHistoryById(id);
            return item is not null ? Results.Ok(item) : Results.NotFound(new { message = $"Test run {id} not found." });
        })
        .WithName("GetLoadTestHistoryById")
        .Produces<LoadTestRunSummary>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 7. Delete single history run
        group.MapDelete("/history/{id:guid}", (Guid id, ILoadTestRunner runner) =>
        {
            var deleted = runner.DeleteHistory(id);
            return deleted ? Results.Ok(new { success = true }) : Results.NotFound(new { message = "Run not found" });
        })
        .WithName("DeleteLoadTestHistory")
        .Produces<object>();

        // 8. Clear all history
        group.MapDelete("/history", (ILoadTestRunner runner) =>
        {
            runner.ClearHistory();
            return Results.Ok(new { success = true, message = "History cleared." });
        })
        .WithName("ClearAllLoadTestHistory")
        .Produces<object>();

        // 9. Quick Ping latency probe
        group.MapPost("/quick-ping", async (
            [FromBody] QuickPingRequest request,
            ILoadTestRunner runner,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.AuthToken))
            {
                var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    request.AuthToken = authHeader.Substring(7).Trim();
                }
            }

            var result = await runner.QuickPingAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("QuickPing")
        .Produces<QuickPingResult>();

        return group;
    }
}

