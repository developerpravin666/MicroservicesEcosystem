using AccountingInventory.Application.Customers;
using AccountingInventory.Application.Customers.Queries.SearchCustomers;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.GeneralLedger;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class CustomerEndpoints
{
    public static RouteGroupBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        group.MapGet("/", async ([FromQuery] string? search, [FromQuery] int? limit,
                [FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? sortBy,
                [FromQuery] string? sortDirection, ISender sender, CancellationToken cancellationToken) =>
            {
                if (page.HasValue || pageSize.HasValue)
                    return (IResult)Results.Ok(await sender.Send(new GetCustomersQuery(page ?? 1,
                        pageSize ?? 20, search, sortBy, sortDirection), cancellationToken));
                return Results.Ok(await sender.Send(new SearchCustomersQuery(search, limit ?? 10), cancellationToken));
            })
            .WithName("SearchCustomers")
            .Produces<IReadOnlyList<CustomerLookupSummary>>(StatusCodes.Status200OK)
            .Produces<PagedResult<CustomerRecord>>(StatusCodes.Status200OK);

        group.MapPost("/", async (CreateCustomerCommand command, ISender sender, CancellationToken cancellationToken) =>
                Results.Created("/api/customers", await sender.Send(command, cancellationToken)))
            .WithName("CreateCustomer")
            .Produces<CustomerRecord>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetCustomerByIdQuery(id), cancellationToken)))
            .WithName("GetCustomerById")
            .Produces<CustomerRecord>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (Guid id, UpdateCustomerCommand command,
                ISender sender, CancellationToken cancellationToken) =>
            {
                if (id != command.Id) return Results.BadRequest("ID in route does not match ID in body.");
                return Results.Ok(await sender.Send(command, cancellationToken));
            })
            .WithName("UpdateCustomer")
            .Produces<CustomerRecord>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteCustomerCommand(id), cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteCustomer")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/history", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetCustomerHistoryQuery(id), cancellationToken)))
            .WithName("GetCustomerHistory")
            .Produces<CustomerHistory>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/statement", async (Guid id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
                ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetPartyStatementQuery("Customer", id, from, to), cancellationToken)))
            .WithName("GetCustomerStatement")
            .Produces<PartyStatementOfAccountDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
