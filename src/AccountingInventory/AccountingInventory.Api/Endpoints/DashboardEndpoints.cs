using AccountingInventory.Application.Dashboard;
using BuildingBlocks.WebDefaults;
using MediatR;

namespace AccountingInventory.Api.Endpoints;

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        group.MapGet("/summary", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetDashboardSummaryQuery(), ct)))
            .WithName("GetDashboardSummary")
            .Produces<DashboardSummaryDto>();

        return group;
    }
}

