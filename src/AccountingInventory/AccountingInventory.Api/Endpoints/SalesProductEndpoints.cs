using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Sales.Products;
using AccountingInventory.Application.Sales.Products.Commands.CreateSalesProduct;
using AccountingInventory.Application.Sales.Products.Commands.DeleteSalesProduct;
using AccountingInventory.Application.Sales.Products.Commands.UpdateSalesProduct;
using AccountingInventory.Application.Sales.Products.Queries.GetSalesProductById;
using AccountingInventory.Application.Sales.Products.Queries.GetSalesProducts;
using AccountingInventory.Application.Sales.Payments.Commands.RecordSalesPayment;
using AccountingInventory.Application.Sales.Payments;
using AccountingInventory.Application.Sales.Payments.Queries.GetSalesPayment;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class SalesProductEndpoints
{
    public static RouteGroupBuilder MapSalesProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sales/products")
            .WithTags("Sales Products")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        group.MapPost("/", async (CreateSalesProductCommand command, ISender sender, CancellationToken ct) =>
            Results.Created("/api/sales/products", await sender.Send(command, ct)))
            .WithName("CreateSalesProduct")
            .Produces<SalesProductSummary>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async ([FromQuery] int? page, [FromQuery] int? pageSize,
            [FromQuery] string? search, [FromQuery] string? sortBy, [FromQuery] string? sortDirection, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSalesProductsQuery(page ?? 1, pageSize ?? 20, search, sortBy, sortDirection), ct)))
            .WithName("GetSalesProducts")
            .Produces<PagedResult<SalesProductSummary>>();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSalesProductByIdQuery(id), ct)))
            .WithName("GetSalesProductById")
            .Produces<SalesProductSummary>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/payment", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSalesPaymentQuery(id), ct)))
            .WithName("GetSalesPayment")
            .Produces<SalesPaymentSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/payment", async (Guid id, RecordSalesPaymentCommand command,
            ISender sender, CancellationToken ct) =>
        {
            if (id != command.SalesProductId)
                return Results.BadRequest("Sale ID in route does not match ID in body.");
            return Results.Ok(await sender.Send(command, ct));
        })
            .WithName("RecordSalesPayment")
            .Produces<SalesPaymentSummary>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (Guid id, UpdateSalesProductCommand command, ISender sender, CancellationToken ct) =>
        {
            if (id != command.Id) return Results.BadRequest("ID in route does not match ID in body.");
            return Results.Ok(await sender.Send(command, ct));
        })
            .WithName("UpdateSalesProduct")
            .Produces<SalesProductSummary>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteSalesProductCommand(id), ct);
            return Results.NoContent();
        })
            .WithName("DeleteSalesProduct")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
