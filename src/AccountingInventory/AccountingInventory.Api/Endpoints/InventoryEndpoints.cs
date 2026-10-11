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
        return group;
    }
}
