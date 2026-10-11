using AccountingInventory.Application.ProductTypes.Commands.AddProductType;
using AccountingInventory.Application.ProductTypes.Commands.DeleteProductType;
using AccountingInventory.Application.ProductTypes.Commands.UpdateProductTypeCommand;
using AccountingInventory.Application.ProductTypes.Queries.GetProductTypeById;
using AccountingInventory.Application.ProductTypes.Queries.GetProductTypesForDDL;
using AccountingInventory.Application.Common.Models;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class ProductTypeEndpoints
{
    public static RouteGroupBuilder MapProductTypeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/product-types")
            .WithTags("ProductTypes")
            .RequireAuthorization("AuthenticatedUser")
            // Tenant-specific product type data always requires X-Tenant-Id. The filter
            // resolves its schema from the tenant registry before MediatR creates a
            // tenant-scoped DbContext.
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        // The tenant is selected by X-Tenant-Id and resolved server-side by the
        // group filter. The header must match the authenticated user's tenant.
        group.MapPost("/", async (AddProductTypeCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return Results.Created($"/api/product-types", result);
            })
            .WithName("AddProductType")
            .Produces<AddProductTypeResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/all", async (ISender sender,
          CancellationToken ct) =>
          Results.Ok(await sender.Send(new GetProductTypesForDDL(), ct)))
              .WithName("GetProductTypesForDDL")
              .Produces<IEnumerable<GetProductTypesForDDLSummary>>();

        group.MapGet("/", async (
             [FromQuery] int? page,
             [FromQuery] int? pageSize,
             [FromQuery] string? sortBy,
             [FromQuery] string? sortDirection,
             [FromQuery] string? search,
             ISender sender,
             CancellationToken ct) =>
             Results.Ok(await sender.Send(new Application.ProductTypes.Queries.GetProductTypes.GetProductTypesQuery(
                 page ?? 1,
                 pageSize ?? 20,
                 sortBy,
                 sortDirection ?? "asc",
                 search), ct)))
         .WithName("GetProductTypes")
         .Produces<PagedResult<Application.ProductTypes.Queries.GetProductTypes.ProductTypeSummary>>();

        group.MapGet("/{id}", async (Guid id, ISender sender, CancellationToken ct) =>
                 Results.Ok(await sender.Send(new GetProductTypeByIdQuery(id), ct)))
            .WithName("GetProductTypeById")
            //.Produces<Application.ProductTypes.Queries.GetProductTypeById.ProductTypeSummary>()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);


        group.MapPut("/{id:guid}", async (Guid id, UpdateProductTypeCommand command, ISender sender, CancellationToken ct) =>
        {           
            if (id != command.Id)
            {
                return Results.BadRequest("ID in route does not match ID in body.");
            }

            var result = await sender.Send(command, ct);

            // 2. Return 200 OK or 204 No Content for a successful update
            return Results.Ok(result);
        })
            .WithName("UpdateProductType")
            .Produces<UpdateProductTypeResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteProductTypeCommand(id), ct);
                return Results.NoContent();
            })
            .WithName("DeleteProductType")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    
        return group;
    }
}
