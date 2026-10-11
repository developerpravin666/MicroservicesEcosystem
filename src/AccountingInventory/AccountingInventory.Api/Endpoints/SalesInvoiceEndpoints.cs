using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Sales.Invoices;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class SalesInvoiceEndpoints
{
    public static RouteGroupBuilder MapSalesInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sales/invoices")
            .WithTags("Sales Invoices")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        group.MapPost("/", async (CreateSalesInvoiceCommand command, ISender sender, CancellationToken ct) =>
            Results.Created("/api/sales/invoices", await sender.Send(command, ct)))
            .WithName("CreateSalesInvoice")
            .Produces<SalesInvoiceDetailsDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async ([FromQuery] int? page, [FromQuery] int? pageSize,
            [FromQuery] string? search, [FromQuery] string? sortBy, [FromQuery] string? sortDirection, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSalesInvoicesQuery(page ?? 1, pageSize ?? 20, search, sortBy, sortDirection), ct)))
            .WithName("GetSalesInvoices")
            .Produces<PagedResult<SalesInvoiceSummaryDto>>();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSalesInvoiceDetailsQuery(id), ct)))
            .WithName("GetSalesInvoiceById")
            .Produces<SalesInvoiceDetailsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/payments", async (Guid id, RecordPaymentRequest request, ISender sender, CancellationToken ct) =>
            Results.Created($"/api/sales/invoices/{id}/payments",
                await sender.Send(new RecordSalesInvoicePaymentCommand(
                    id, request.Amount, request.PaymentMode, request.PaymentDate, request.ReferenceNumber, request.Note), ct)))
            .WithName("RecordSalesInvoicePayment")
            .Produces<SalesInvoiceReceiptDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/unpaid-by-customer/{customerId:guid}", async (Guid customerId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetCustomerUnpaidInvoicesQuery(customerId), ct)))
            .WithName("GetCustomerUnpaidInvoices")
            .Produces<CustomerUnpaidInvoicesSummaryDto>(StatusCodes.Status200OK);

        group.MapPost("/allocate-payment", async (AllocateCustomerPaymentCommand command, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(command, ct)))
            .WithName("AllocateCustomerPayment")
            .Produces<PaymentAllocationResultDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/advances/{customerId:guid}", async (Guid customerId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetCustomerAdvancesQuery(customerId), ct)));
        group.MapPost("/advances/apply", async (ApplyCustomerAdvanceCommand q, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(q, ct)));
        group.MapPost("/advances/refund", async (RefundCustomerAdvanceCommand q, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(q, ct)));

        group.MapGet("/gst-states", () =>
            Results.Ok(GstStates.StateMap.Select(kv => new { Code = kv.Key, Name = kv.Value }).OrderBy(s => s.Code)))
            .WithName("GetGstStates");

        return group;
    }

    public sealed record RecordPaymentRequest(
        decimal Amount,
        string PaymentMode,
        DateOnly PaymentDate,
        string? ReferenceNumber = null,
        string? Note = null);
}

