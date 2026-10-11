using AccountingInventory.Application.Taxes;
using AccountingInventory.Application.Taxes.Commands.AddTax;
using AccountingInventory.Application.Taxes.Commands.DeleteTax;
using AccountingInventory.Application.Taxes.Commands.UpdateTax;
using AccountingInventory.Application.Taxes.Queries.GetTaxById;
using AccountingInventory.Application.Taxes.Queries.GetTaxes;
using AccountingInventory.Application.Taxes.Queries.GetTaxesForDDL;
using AccountingInventory.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using BuildingBlocks.WebDefaults;

namespace AccountingInventory.Api.Endpoints;

public static class TaxEndpoints
{
    public static RouteGroupBuilder MapTaxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/taxes")
        .WithTags("Taxes").RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        group.MapPost("/", async (AddTaxCommand command, ISender sender, CancellationToken ct) =>
            Results.Created("/api/taxes", await sender.Send(command, ct))).WithName("AddTax");

        group.MapGet("/all", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetTaxesForDDLQuery(), ct)))
            .WithName("GetTaxesForDDL")
            .Produces<IEnumerable<TaxRateSummary>>();

        group.MapGet("/", async ([FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? sortBy, [FromQuery] string? sortDirection, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetTaxesQuery(page ?? 1, pageSize ?? 20, sortBy, sortDirection), ct))).WithName("GetTaxes")
            .Produces<PagedResult<TaxSummary>>();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetTaxByIdQuery(id), ct))).WithName("GetTaxById");

        group.MapPut("/{id:guid}", async (Guid id, UpdateTaxCommand command, ISender sender, CancellationToken ct) =>
        {
            if (id != command.Id) return Results.BadRequest("ID in route does not match ID in body.");
            return Results.Ok(await sender.Send(command, ct));
        }).WithName("UpdateTax");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        { await sender.Send(new DeleteTaxCommand(id), ct); return Results.NoContent(); }).WithName("DeleteTax");
        return group;
    }
}
