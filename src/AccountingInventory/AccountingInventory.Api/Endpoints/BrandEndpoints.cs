using AccountingInventory.Application.Brands.Commands.AddBrand;
using AccountingInventory.Application.Brands.Commands.DeleteBrand;
using AccountingInventory.Application.Brands.Commands.UpdateBrandCommand;
using AccountingInventory.Application.Brands.Queries.GetBrandById;
using AccountingInventory.Application.Brands.Queries.GetBrandsForDDL;
using AccountingInventory.Application.Common.Models;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class BrandEndpoints
{
    public static RouteGroupBuilder MapBrandEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/brands")
            .WithTags("Brands")
            .RequireAuthorization("AuthenticatedUser")
            // Tenant-specific brand data always requires X-Tenant-Id. The filter
            // resolves its schema from the tenant registry before MediatR creates a
            // tenant-scoped DbContext.
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        // The tenant is selected by X-Tenant-Id and resolved server-side by the
        // group filter. The header must match the authenticated user's tenant.
        group.MapPost("/", async (AddBrandCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return Results.Created($"/api/brands", result);
            })
            .WithName("AddBrand")
            .Produces<AddBrandResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/all", async (ISender sender,
             CancellationToken ct) =>
             Results.Ok(await sender.Send(new GetBrandsForDDL(), ct)))
                 .WithName("GetBrandsForDDL")
                 .Produces<IEnumerable<GetBrandsForDDLSummary>>();

        group.MapGet("/", async (
             [FromQuery] int? page,
             [FromQuery] int? pageSize,
             [FromQuery] string? sortBy,
             [FromQuery] string? sortDirection,
             [FromQuery] string? search,
             ISender sender,
             CancellationToken ct) =>
             Results.Ok(await sender.Send(new Application.Brands.Queries.GetBrands.GetBrandsQuery(
                 page ?? 1,
                 pageSize ?? 20,
                 sortBy,
                 sortDirection ?? "asc",
                 search), ct)))
         .WithName("GetBrands")
         .Produces<PagedResult<Application.Brands.Queries.GetBrands.BrandSummary>>();

        group.MapGet("/{id}", async (Guid id, ISender sender, CancellationToken ct) =>
                 Results.Ok(await sender.Send(new GetBrandByIdQuery(id), ct)))
            .WithName("GetBrandById")
            //.Produces<Application.Brands.Queries.GetBrandById.BrandSummary>()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);


        group.MapPut("/{id:guid}", async (Guid id, UpdateBrandCommand command, ISender sender, CancellationToken ct) =>
        {           
            if (id != command.Id)
            {
                return Results.BadRequest("ID in route does not match ID in body.");
            }

            var result = await sender.Send(command, ct);

            // 2. Return 200 OK or 204 No Content for a successful update
            return Results.Ok(result);
        })
            .WithName("UpdateBrand")
            .Produces<UpdateBrandResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteBrandCommand(id), ct);
                return Results.NoContent();
            })
            .WithName("DeleteBrand")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    
        return group;
    }
}
