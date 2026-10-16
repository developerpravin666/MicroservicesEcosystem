using AccountingInventory.Application.Purchases.Products.Commands.CreateProduct;
using AccountingInventory.Application.Purchases.Products.Commands.BulkCreateProducts;
using AccountingInventory.Application.Purchases.Products.Commands.BulkUpdateProducts;
using AccountingInventory.Application.Purchases.Products.Commands.DeleteProduct;
using AccountingInventory.Application.Purchases.Products.Commands.UpdateProduct;
using AccountingInventory.Application.Purchases.Products.Commands.ActivateProduct;
using AccountingInventory.Application.Purchases.Products.Queries.GetProductById;
using AccountingInventory.Application.Purchases.Products.Queries.GetProducts;
using AccountingInventory.Application.Purchases.Products.Queries.GetProductsForDDL;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new BuildingBlocks.WebDefaults.RequiresTenantIdHeaderAttribute());

        group.MapPost("/", async (CreateProductCommand command, ISender sender, CancellationToken ct) =>
            Results.Created("/api/products", await sender.Send(command, ct)))
            .WithName("CreateProduct");

        group.MapPost("/bulk", async (BulkCreateProductsCommand command, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(command, ct)))
            .WithName("BulkCreateProducts");

        group.MapPut("/bulk", async (BulkUpdateProductsCommand command, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(command, ct)))
            .WithName("BulkUpdateProducts");

        group.MapGet("/all", async ([FromQuery] Guid? currentSaleId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetProductsForDDLQuery(currentSaleId), ct)))
            .WithName("GetProductsForDDL")
            .Produces<IEnumerable<GetProductsForDDLSummary>>();

        group.MapGet("/", async ([FromQuery] int? page, [FromQuery] int? pageSize,
            [FromQuery] string? search, [FromQuery] string? sortBy, [FromQuery] string? sortDirection, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetProductsQuery(page ?? 1, pageSize ?? 20, search, sortBy, sortDirection), ct)))
            .WithName("GetProducts");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetProductByIdQuery(id), ct)))
            .WithName("GetProductById");

        group.MapPut("/{id:guid}", async (Guid id, UpdateProductCommand command, ISender sender, CancellationToken ct) =>
        {
            if (id != command.Id) return Results.BadRequest("ID in route does not match ID in body.");
            return Results.Ok(await sender.Send(command, ct));
        })
            .WithName("UpdateProduct");

        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ActivateProductCommand(id, true), ct);
            return Results.Ok(new { success = true, message = "Product activated successfully." });
        }).WithName("ActivateProduct");

        group.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ActivateProductCommand(id, false), ct);
            return Results.Ok(new { success = true, message = "Product deactivated successfully." });
        }).WithName("DeactivateProduct");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteProductCommand(id), ct);
            return Results.NoContent();
        }).WithName("DeleteProduct");

        return group;
    }
}
