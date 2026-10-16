using AccountingInventory.Application.Compliance.EWayBills.Commands.GenerateEWayBill;
using AccountingInventory.Application.Compliance.EWayBills.Commands.CancelEWayBill;
using AccountingInventory.Application.Compliance.EWayBills.Queries.GetEWayBills;
using AccountingInventory.Application.Compliance.EWayBills.Queries.GetEWayBillByInvoiceId;
using AccountingInventory.Application.Compliance.EWayBills.Queries.GetEWayBillJson;
using AccountingInventory.Application.Compliance.EWayBills.Models;
using AccountingInventory.Application.Compliance.EInvoices.Commands.GenerateEInvoice;
using AccountingInventory.Application.Compliance.EInvoices.Commands.CancelEInvoice;
using AccountingInventory.Application.Compliance.EInvoices.Queries.GetEInvoices;
using AccountingInventory.Application.Compliance.EInvoices.Queries.GetEInvoiceByInvoiceId;
using AccountingInventory.Application.Compliance.EInvoices.Queries.GetEInvoiceJson;
using AccountingInventory.Application.Compliance.EInvoices.Models;
using AccountingInventory.Application.Compliance.Rcm.Commands.CreateRcmSelfInvoice;
using AccountingInventory.Application.Compliance.Rcm.Commands.CancelRcmSelfInvoice;
using AccountingInventory.Application.Compliance.Rcm.Queries.GetRcmSelfInvoices;
using AccountingInventory.Application.Compliance.Rcm.Queries.GetRcmSelfInvoiceById;
using AccountingInventory.Application.Compliance.Rcm.Models;
using AccountingInventory.Application.Common.Models;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class ComplianceEndpoints
{
    public static RouteGroupBuilder MapComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/compliance")
            .WithTags("Compliance - E-Way Bill & E-Invoice")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        // --- E-Way Bills ---
        group.MapPost("/eway-bill/generate", async (
            GenerateEWayBillCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/compliance/eway-bill/{id}", new { id });
        }).WithName("GenerateEWayBill");

        group.MapPost("/eway-bill/{id:guid}/cancel", async (
            Guid id,
            [FromBody] CancelEWayBillRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new CancelEWayBillCommand(id, request.Reason), ct);
            return Results.NoContent();
        }).WithName("CancelEWayBill");

        group.MapGet("/eway-bill", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] string? status,
            [FromQuery] string? search,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetEWayBillsQuery(
                page ?? 1,
                pageSize ?? 20,
                fromDate,
                toDate,
                status,
                search), ct);
            return Results.Ok(result);
        }).WithName("GetEWayBills").Produces<PagedResult<EWayBillDto>>();

        group.MapGet("/eway-bill/invoice/{invoiceId:guid}", async (
            Guid invoiceId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetEWayBillByInvoiceIdQuery(invoiceId), ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        }).WithName("GetEWayBillByInvoiceId").Produces<EWayBillDto>();

        group.MapGet("/eway-bill/{id:guid}/json", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var json = await sender.Send(new GetEWayBillJsonQuery(id), ct);
            return Results.Content(json, "application/json");
        }).WithName("GetEWayBillJson");

        // --- E-Invoicing ---
        group.MapPost("/e-invoice/generate", async (
            GenerateEInvoiceCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/compliance/e-invoice/{id}", new { id });
        }).WithName("GenerateEInvoice");

        group.MapPost("/e-invoice/{id:guid}/cancel", async (
            Guid id,
            [FromBody] CancelEInvoiceRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new CancelEInvoiceCommand(id, request.Reason), ct);
            return Results.NoContent();
        }).WithName("CancelEInvoice");

        group.MapGet("/e-invoice", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] string? status,
            [FromQuery] string? search,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetEInvoicesQuery(
                page ?? 1,
                pageSize ?? 20,
                fromDate,
                toDate,
                status,
                search), ct);
            return Results.Ok(result);
        }).WithName("GetEInvoices").Produces<PagedResult<EInvoiceDto>>();

        group.MapGet("/e-invoice/invoice/{invoiceId:guid}", async (
            Guid invoiceId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetEInvoiceByInvoiceIdQuery(invoiceId), ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        }).WithName("GetEInvoiceByInvoiceId").Produces<EInvoiceDto>();

        group.MapGet("/e-invoice/{id:guid}/json", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var json = await sender.Send(new GetEInvoiceJsonQuery(id), ct);
            return Results.Content(json, "application/json");
        }).WithName("GetEInvoiceJson");

        // --- Reverse Charge Mechanism (RCM) ---
        group.MapPost("/rcm", async (
            CreateRcmSelfInvoiceCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/compliance/rcm/{id}", new { id });
        }).WithName("CreateRcmSelfInvoice");

        group.MapPost("/rcm/{id:guid}/cancel", async (
            Guid id,
            [FromBody] CancelRcmRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new CancelRcmSelfInvoiceCommand(id, request.Reason), ct);
            return Results.NoContent();
        }).WithName("CancelRcmSelfInvoice");

        group.MapGet("/rcm", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] string? section,
            [FromQuery] string? status,
            [FromQuery] string? search,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRcmSelfInvoicesQuery(
                page ?? 1,
                pageSize ?? 20,
                fromDate,
                toDate,
                section,
                status,
                search), ct);
            return Results.Ok(result);
        }).WithName("GetRcmSelfInvoices").Produces<PagedResult<RcmSelfInvoiceDto>>();

        group.MapGet("/rcm/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRcmSelfInvoiceByIdQuery(id), ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        }).WithName("GetRcmSelfInvoiceById").Produces<RcmSelfInvoiceDto>();

        return group;
    }
}

public sealed record CancelEWayBillRequest(string Reason);
public sealed record CancelEInvoiceRequest(string Reason);
public sealed record CancelRcmRequest(string Reason);


