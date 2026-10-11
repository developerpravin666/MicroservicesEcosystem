using AccountingInventory.Application.Common.Models;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using AccountingInventory.Application.ProductModels.Commands.AddProductModel;
using AccountingInventory.Application.ProductModels.Queries.GetProductModelsForDDL;
using AccountingInventory.Application.ProductModels.Commands.UpdateProductModelCommand;
using AccountingInventory.Application.ProductModels.Queries.GetProductModelById;
using AccountingInventory.Application.ProductModels.Commands.DeleteProductModel;

namespace AccountingInventory.Api.Endpoints;

public static class ProductModelEndpoints
{
    public static RouteGroupBuilder MapProductModelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/product-models")
            .WithTags("ProductModels")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        group.MapPost("/", async (AddProductModelCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return Results.Created($"/api/product-models", result);
            })
            .WithName("AddProductModel")
            .Produces<AddProductModelResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/all", async (ISender sender,
          CancellationToken ct) =>
          Results.Ok(await sender.Send(new GetProductModelsForDDL(), ct)))
              .WithName("GetProductModelsForDDL")
              .Produces<IEnumerable<GetProductModelsForDDLSummary>>();

        group.MapGet("/", async (
             [FromQuery] int? page,
             [FromQuery] int? pageSize,
             [FromQuery] string? sortBy,
             [FromQuery] string? sortDirection,
             [FromQuery] string? search,
             ISender sender,
             CancellationToken ct) =>
             Results.Ok(await sender.Send(new Application.ProductModels.Queries.GetProductModels.GetProductModelsQuery(
                 page ?? 1,
                 pageSize ?? 20,
                 sortBy,
                 sortDirection ?? "asc",
                 search), ct)))
         .WithName("GetProductModels")
         .Produces<PagedResult<Application.ProductModels.Queries.GetProductModels.ProductModelSummary>>();

        group.MapGet("/{id}", async (Guid id, ISender sender, CancellationToken ct) =>
                 Results.Ok(await sender.Send(new GetProductModelByIdQuery(id), ct)))
            .WithName("GetProductModelById")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);


        group.MapPut("/{id:guid}", async (Guid id, UpdateProductModelCommand command, ISender sender, CancellationToken ct) =>
        {
            if (id != command.Id)
            {
                return Results.BadRequest("ID in route does not match ID in body.");
            }

            var result = await sender.Send(command, ct);

            return Results.Ok(result);
        })
            .WithName("UpdateProductModel")
            .Produces<UpdateProductModelResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteProductModelCommand(id), ct);
                return Results.NoContent();
            })
            .WithName("DeleteProductModel")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
