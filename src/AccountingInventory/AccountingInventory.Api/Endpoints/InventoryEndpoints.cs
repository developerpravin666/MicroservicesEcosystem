using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Inventory.Queries.GetStock;
using AccountingInventory.Application.Inventory;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class InventoryEndpoints
{
    public static RouteGroupBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory")
            .WithTags("Inventory")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        group.MapGet("/stock", async ([FromQuery] int? page, [FromQuery] int? pageSize,
            [FromQuery] string? search, [FromQuery] string? sortBy, [FromQuery] string? sortDirection, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetStockQuery(page ?? 1, pageSize ?? 20, search, sortBy, sortDirection), cancellationToken)))
            .WithName("GetStockInventory")
            .Produces<PagedResult<StockGroupSummary>>();

        group.MapGet("/stock/summary", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetStockSummaryQuery(), cancellationToken)))
            .WithName("GetStockInventorySummary")
            .Produces<StockTotalsSummary>();

        group.MapGet("/stock/products", async ([FromQuery] Guid brandId, [FromQuery] Guid productModelId,
            [FromQuery] Guid variantId, [FromQuery] int? page, [FromQuery] int? pageSize,
            [FromQuery] string? search, [FromQuery] string? sortBy, [FromQuery] string? sortDirection, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetAvailableStockProductsQuery(brandId, productModelId,
                variantId, page ?? 1, pageSize ?? 20, search, sortBy, sortDirection), cancellationToken)))
            .WithName("GetAvailableStockProducts")
            .Produces<PagedResult<AvailableStockProductSummary>>();

        group.MapGet("/movements", async ([FromQuery] int? page, [FromQuery] int? pageSize,
            [FromQuery] string? search, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
            ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetStockMovementsQuery(page ?? 1, pageSize ?? 20, search, fromDate, toDate), cancellationToken)))
            .WithName("GetStockMovements").Produces<PagedResult<StockMovementSummary>>();

        group.MapPost("/adjustments/write-off", async (WriteOffInventoryCommand command, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(new { id = await sender.Send(command, cancellationToken) }))
            .WithName("WriteOffInventory");

        group.MapPost("/skus/opening-stock", async (StageOpeningSkuStockCommand q,ISender sender,CancellationToken ct) => Results.Ok(new {id=await sender.Send(q,ct)}));
        group.MapGet("/skus/purchases", async (ISender sender,CancellationToken ct) => Results.Ok(await sender.Send(new GetSkuPurchasesQuery(),ct)));
        group.MapGet("/skus", async (ISender sender,CancellationToken ct) => Results.Ok(await sender.Send(new GetStockSkusQuery(),ct)));
        group.MapPost("/skus", async (CreateStockSkuCommand q,ISender sender,CancellationToken ct) => Results.Ok(new {id=await sender.Send(q,ct)}));
        group.MapPost("/skus/receipts", async (ReceiveSkuStockCommand q,ISender sender,CancellationToken ct) => Results.Ok(new {id=await sender.Send(q,ct)}));
        group.MapPost("/skus/write-off", async (WriteOffSkuStockCommand q,ISender sender,CancellationToken ct) => Results.Ok(new {id=await sender.Send(q,ct)}));

        // Phase 4: Multi-Godown Architecture
        group.MapGet("/godowns", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Application.Inventory.Godowns.Queries.GetGodowns.GetGodownsQuery(), ct)))
            .WithName("GetGodowns");

        group.MapPost("/godowns", async (Application.Inventory.Godowns.Commands.CreateGodown.CreateGodownCommand command, ISender sender, CancellationToken ct) =>
            Results.Ok(new { id = await sender.Send(command, ct) }))
            .WithName("CreateGodown");

        group.MapPut("/godowns/{id:guid}", async (Guid id, Application.Inventory.Godowns.Commands.UpdateGodown.UpdateGodownCommand command, ISender sender, CancellationToken ct) =>
        {
            if (id != command.Id) return Results.BadRequest("ID in route does not match command payload.");
            await sender.Send(command, ct);
            return Results.NoContent();
        }).WithName("UpdateGodown");

        group.MapGet("/godowns/{id:guid}/stock", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Application.Inventory.Godowns.Queries.GetGodownStock.GetGodownStockQuery(id), ct)))
            .WithName("GetGodownStock");

        // Phase 4: Stock Transfer Journal
        group.MapGet("/transfers", async ([FromQuery] Guid? godownId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Application.Inventory.Transfers.Queries.GetStockTransfers.GetStockTransfersQuery(godownId, fromDate, toDate), ct)))
            .WithName("GetStockTransfers");

        group.MapGet("/transfers/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Application.Inventory.Transfers.Queries.GetStockTransferById.GetStockTransferByIdQuery(id), ct)))
            .WithName("GetStockTransferById");

        group.MapPost("/transfers", async (Application.Inventory.Transfers.Commands.CreateStockTransfer.CreateStockTransferCommand command, ISender sender, CancellationToken ct) =>
            Results.Ok(new { id = await sender.Send(command, ct) }))
            .WithName("CreateStockTransfer");

        // Phase 4: Physical Stock Verification & Discrepancy Adjustment
        group.MapGet("/audit", async ([FromQuery] Guid godownId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Application.Inventory.Adjustments.Queries.GetPhysicalStockAudit.GetPhysicalStockAuditQuery(godownId), ct)))
            .WithName("GetPhysicalStockAudit");

        group.MapGet("/adjustments", async ([FromQuery] Guid? godownId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Application.Inventory.Adjustments.Queries.GetStockAdjustments.GetStockAdjustmentsQuery(godownId, fromDate, toDate), ct)))
            .WithName("GetStockAdjustments");

        group.MapPost("/adjustments", async (Application.Inventory.Adjustments.Commands.CreateStockAdjustment.CreateStockAdjustmentCommand command, ISender sender, CancellationToken ct) =>
            Results.Ok(new { id = await sender.Send(command, ct) }))
            .WithName("CreateStockAdjustment");

        return group;
    }
}
