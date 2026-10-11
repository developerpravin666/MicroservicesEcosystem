using AccountingInventory.Application.Colors.Commands.AddColor;
using AccountingInventory.Application.Colors.Commands.DeleteColor;
using AccountingInventory.Application.Colors.Commands.UpdateColorCommand;
using AccountingInventory.Application.Colors.Queries.GetColorById;
using AccountingInventory.Application.Colors.Queries.GetColorsForDDL;
using AccountingInventory.Application.Common.Models;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class ColorEndpoints
{
    public static RouteGroupBuilder MapColorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/colors")
            .WithTags("Colors")
            .RequireAuthorization("AuthenticatedUser")
            // Tenant-specific color data always requires X-Tenant-Id. The filter
            // resolves its schema from the tenant registry before MediatR creates a
            // tenant-scoped DbContext.
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        // The tenant is selected by X-Tenant-Id and resolved server-side by the
        // group filter. The header must match the authenticated user's tenant.
        group.MapPost("/", async (AddColorCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return Results.Created($"/api/colors", result);
            })
            .WithName("AddColor")
            .Produces<AddColorResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/all", async (ISender sender,
             CancellationToken ct) =>
             Results.Ok(await sender.Send(new GetColorsForDDL(), ct)))
                 .WithName("GetColorsForDDL")
                 .Produces<IEnumerable<GetColorsForDDLSummary>>();

        group.MapGet("/", async (
             [FromQuery] int? page,
             [FromQuery] int? pageSize,
             [FromQuery] string? sortBy,
             [FromQuery] string? sortDirection,
             [FromQuery] string? search,
             ISender sender,
             CancellationToken ct) =>
             Results.Ok(await sender.Send(new Application.Colors.Queries.GetColors.GetColorsQuery(
                 page ?? 1,
                 pageSize ?? 20,
                 sortBy,
                 sortDirection ?? "asc",
                 search), ct)))
         .WithName("GetColors")
         .Produces<PagedResult<Application.Colors.Queries.GetColors.ColorSummary>>();

        group.MapGet("/{id}", async (Guid id, ISender sender, CancellationToken ct) =>
                 Results.Ok(await sender.Send(new GetColorByIdQuery(id), ct)))
            .WithName("GetColorById")
            //.Produces<Application.Colors.Queries.GetColorById.ColorSummary>()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);


        group.MapPut("/{id:guid}", async (Guid id, UpdateColorCommand command, ISender sender, CancellationToken ct) =>
        {           
            if (id != command.Id)
            {
                return Results.BadRequest("ID in route does not match ID in body.");
            }

            var result = await sender.Send(command, ct);

            // 2. Return 200 OK or 204 No Content for a successful update
            return Results.Ok(result);
        })
            .WithName("UpdateColor")
            .Produces<UpdateColorResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteColorCommand(id), ct);
                return Results.NoContent();
            })
            .WithName("DeleteColor")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    
        return group;
    }
}
