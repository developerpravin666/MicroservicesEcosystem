using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Purchases.Accounting;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class PurchaseAccountingEndpoints
{
    public static RouteGroupBuilder MapPurchaseAccountingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/purchases")
            .WithTags("Purchase Accounting")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        group.MapGet("/bills", async ([FromQuery] int? page, [FromQuery] int? pageSize,
            [FromQuery] string? search, [FromQuery] string? sortBy, [FromQuery] string? sortDirection, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetPurchaseBillsQuery(page ?? 1, pageSize ?? 20, search, sortBy, sortDirection), ct)))
            .WithName("GetPurchaseBills")
            .Produces<PagedResult<PurchaseBillSummary>>();

        group.MapGet("/bills/details", async ([FromQuery] Guid vendorId, [FromQuery] string billNumber,
            ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetPurchaseBillDetailsQuery(vendorId, billNumber), ct)))
            .WithName("GetPurchaseBillDetails")
            .Produces<PurchaseBillDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/payments", async (RecordPurchasePaymentCommand command, ISender sender, CancellationToken ct) =>
            Results.Created("/api/purchases/bills", await sender.Send(command, ct)))
            .WithName("RecordPurchasePayment")
            .Produces<PurchasePaymentSummary>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }
}
