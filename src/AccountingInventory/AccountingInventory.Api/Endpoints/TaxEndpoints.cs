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

        // GSTR-1 Endpoints
        var gstr1Group = app.MapGroup("/api/tax/gstr1")
            .WithTags("GST Returns")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        gstr1Group.MapGet("/summary", async (
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new AccountingInventory.Application.Taxes.Queries.GetGstr1Summary.GetGstr1SummaryQuery(from, to), ct);
            return Results.Ok(result);
        }).WithName("GetGstr1Summary").Produces<AccountingInventory.Application.Taxes.Queries.GetGstr1Summary.Gstr1SummaryDto>();

        gstr1Group.MapGet("/export-json", async (
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new AccountingInventory.Application.Taxes.Queries.ExportGstr1Json.ExportGstr1JsonQuery(from, to), ct);
            return Results.Content(result.JsonPayload, result.ContentType);
        }).WithName("ExportGstr1Json");

        gstr1Group.MapGet("/export-excel", async (
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new AccountingInventory.Application.Taxes.Queries.ExportGstr1Excel.ExportGstr1ExcelQuery(from, to), ct);
            return Results.File(result.FileBytes, result.ContentType, result.FileName);
        }).WithName("ExportGstr1Excel");

        // GSTR-3B Endpoints
        var gstr3bGroup = app.MapGroup("/api/tax/gstr3b")
            .WithTags("GST Returns")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        gstr3bGroup.MapGet("/summary", async (
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new AccountingInventory.Application.Taxes.Queries.GetGstr3bSummary.GetGstr3bSummaryQuery(from, to), ct);
            return Results.Ok(result);
        }).WithName("GetGstr3bSummary").Produces<AccountingInventory.Application.Taxes.Queries.GetGstr3bSummary.Gstr3bSummaryDto>();

        return group;
    }
}
