using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Sales.Accounting;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class SalesAccountingEndpoints
{
    public static RouteGroupBuilder MapSalesAccountingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sales/accounting")
            .WithTags("Sales Accounting")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        group.MapGet("/bills", async ([FromQuery] int? page, [FromQuery] int? pageSize,
            [FromQuery] string? search, [FromQuery] string? sortBy, [FromQuery] string? sortDirection, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSalesBillsQuery(page ?? 1, pageSize ?? 20, search, sortBy, sortDirection), ct)))
            .WithName("GetSalesBills")
            .Produces<PagedResult<SalesBillSummary>>();

        group.MapGet("/bills/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSalesBillDetailsQuery(id), ct)))
            .WithName("GetSalesBillDetails")
            .Produces<SalesBillDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/bills/{id:guid}/payments", async (Guid id, RecordSalesReceiptCommand command,
            ISender sender, CancellationToken ct) =>
        {
            if (id != command.SalesProductId)
                return Results.BadRequest("Sales bill ID in route does not match the payment request.");
            return Results.Created($"/api/sales/accounting/bills/{id}", await sender.Send(command, ct));
        })
            .WithName("RecordSalesReceipt")
            .Produces<SalesReceiptSummary>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }
}
