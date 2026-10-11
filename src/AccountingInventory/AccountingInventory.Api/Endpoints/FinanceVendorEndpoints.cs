using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.FinanceVendors;
using AccountingInventory.Application.FinanceVendors.Commands.AddFinanceVendor;
using AccountingInventory.Application.FinanceVendors.Commands.DeleteFinanceVendor;
using AccountingInventory.Application.FinanceVendors.Commands.UpdateFinanceVendor;
using AccountingInventory.Application.FinanceVendors.Queries.GetFinanceVendorById;
using AccountingInventory.Application.FinanceVendors.Queries.GetFinanceVendors;
using AccountingInventory.Application.FinanceVendors.Queries.GetFinanceVendorsForDDL;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class FinanceVendorEndpoints
{
    public static RouteGroupBuilder MapFinanceVendorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/finance-vendors")
            .WithTags("Finance Vendors")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        group.MapPost("/", async (AddFinanceVendorCommand command, ISender sender, CancellationToken ct) =>
            Results.Created("/api/finance-vendors", await sender.Send(command, ct)))
            .WithName("AddFinanceVendor")
            .Produces<FinanceVendorSummary>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/all", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetFinanceVendorsForDDLQuery(), ct)))
            .WithName("GetFinanceVendorsForDDL")
            .Produces<IEnumerable<GetFinanceVendorsForDDLSummary>>();

        group.MapGet("/", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDirection,
            [FromQuery] string? search,
            ISender sender,
            CancellationToken ct) => Results.Ok(await sender.Send(new GetFinanceVendorsQuery(
                page ?? 1, pageSize ?? 20, sortBy, sortDirection ?? "asc", search), ct)))
            .WithName("GetFinanceVendors")
            .Produces<PagedResult<FinanceVendorSummary>>();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetFinanceVendorByIdQuery(id), ct)))
            .WithName("GetFinanceVendorById")
            .Produces<FinanceVendorSummary>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (Guid id, UpdateFinanceVendorCommand command, ISender sender, CancellationToken ct) =>
        {
            if (id != command.Id) return Results.BadRequest("ID in route does not match ID in body.");
            return Results.Ok(await sender.Send(command, ct));
        })
            .WithName("UpdateFinanceVendor")
            .Produces<FinanceVendorSummary>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteFinanceVendorCommand(id), ct);
            return Results.NoContent();
        })
            .WithName("DeleteFinanceVendor")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
