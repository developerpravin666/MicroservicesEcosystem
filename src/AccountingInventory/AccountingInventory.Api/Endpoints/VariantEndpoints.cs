using AccountingInventory.Application.Variants.Commands.AddVariant;
using AccountingInventory.Application.Variants.Commands.DeleteVariant;
using AccountingInventory.Application.Variants.Commands.UpdateVariantCommand;
using AccountingInventory.Application.Variants.Queries.GetVariantById;
using AccountingInventory.Application.Variants.Queries.GetVariantsForDDL;
using AccountingInventory.Application.Common.Models;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class VariantEndpoints
{
    public static RouteGroupBuilder MapVariantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/variants")
            .WithTags("Variants")
            .RequireAuthorization("AuthenticatedUser")
            // Tenant-specific variant data always requires X-Tenant-Id. The filter
            // resolves its schema from the tenant registry before MediatR creates a
            // tenant-scoped DbContext.
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        // The tenant is selected by X-Tenant-Id and resolved server-side by the
        // group filter. The header must match the authenticated user's tenant.
        group.MapPost("/", async (AddVariantCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return Results.Created($"/api/variants", result);
            })
            .WithName("AddVariant")
            .Produces<AddVariantResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/all", async (ISender sender,
             CancellationToken ct) =>
             Results.Ok(await sender.Send(new GetVariantsForDDL(), ct)))
                 .WithName("GetVariantsForDDL")
                 .Produces<IEnumerable<GetVariantsForDDLSummary>>();

        group.MapGet("/", async (
             [FromQuery] int? page,
             [FromQuery] int? pageSize,
             [FromQuery] string? sortBy,
             [FromQuery] string? sortDirection,
             [FromQuery] string? search,
             ISender sender,
             CancellationToken ct) =>
             Results.Ok(await sender.Send(new Application.Variants.Queries.GetVariants.GetVariantsQuery(
                 page ?? 1,
                 pageSize ?? 20,
                 sortBy,
                 sortDirection ?? "asc",
                 search), ct)))
         .WithName("GetVariants")
         .Produces<PagedResult<Application.Variants.Queries.GetVariants.VariantSummary>>();

        group.MapGet("/{id}", async (Guid id, ISender sender, CancellationToken ct) =>
                 Results.Ok(await sender.Send(new GetVariantByIdQuery(id), ct)))
            .WithName("GetVariantById")
            //.Produces<Application.Variants.Queries.GetVariantById.VariantSummary>()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);


        group.MapPut("/{id:guid}", async (Guid id, UpdateVariantCommand command, ISender sender, CancellationToken ct) =>
        {           
            if (id != command.Id)
            {
                return Results.BadRequest("ID in route does not match ID in body.");
            }

            var result = await sender.Send(command, ct);

            // 2. Return 200 OK or 204 No Content for a successful update
            return Results.Ok(result);
        })
            .WithName("UpdateVariant")
            .Produces<UpdateVariantResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteVariantCommand(id), ct);
                return Results.NoContent();
            })
            .WithName("DeleteVariant")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    
        return group;
    }
}
