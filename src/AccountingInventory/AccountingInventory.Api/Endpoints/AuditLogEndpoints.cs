using AccountingInventory.Application.AuditLogs;
using AccountingInventory.Application.Common.Models;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class AuditLogEndpoints
{
    public static RouteGroupBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit-logs")
            .WithTags("Audit Logs")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        group.MapGet("/modules", async (ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetAuditLogModulesQuery(), cancellationToken)))
            .WithName("GetAuditLogModules")
            .Produces<IReadOnlyList<AuditLogModule>>();

        group.MapGet("/", async ([FromQuery] int? page, [FromQuery] int? pageSize,
                [FromQuery] string? search, [FromQuery] string? tableName, [FromQuery] string? action,
                [FromQuery] Guid? changedBy, [FromQuery] string? recordId, [FromQuery] DateOnly? fromDate,
                [FromQuery] DateOnly? toDate, [FromQuery] string? sortBy, [FromQuery] string? sortDirection,
                ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetAuditLogsQuery(page ?? 1, pageSize ?? 20,
                    search, tableName, action, changedBy, recordId, fromDate, toDate, sortBy,
                    sortDirection ?? "desc"), cancellationToken)))
            .WithName("GetAuditLogs")
            .Produces<PagedResult<AuditLogSummary>>();

        return group;
    }
}
