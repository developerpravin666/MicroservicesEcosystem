using AccountingInventory.Application.Vendors.Commands.AddVendor;
using AccountingInventory.Application.Vendors.Commands.DeleteVendor;
using AccountingInventory.Application.Vendors.Commands.UpdateVendor;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Vendors.Queries.GetVendorById;
using AccountingInventory.Application.Vendors.Queries.GetVendors;
using AccountingInventory.Application.GeneralLedger;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using AccountingInventory.Application.Vendors.Queries.GetVendorsForDDL;

namespace AccountingInventory.Api.Endpoints;

public static class VendorEndpoints
{
    public static RouteGroupBuilder MapVendorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vendors")
            .WithTags("Vendors")
            .RequireAuthorization("AuthenticatedUser")
            // Tenant-specific vendor data always requires X-Tenant-Id. The filter
            // resolves its schema from the tenant registry before MediatR creates a
            // tenant-scoped DbContext.
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        // The tenant is selected by X-Tenant-Id and resolved server-side by the
        // group filter. The header must match the authenticated user's tenant.
        group.MapPost("/", async (AddVendorCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return Results.Created($"/api/vendors", result);
            })
            .WithName("AddVendor")
            .Produces<AddVendorResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/all", async (ISender sender,
           CancellationToken ct) =>
           Results.Ok(await sender.Send(new GetVendorsForDDL(), ct)))
           .WithName("GetVendorsForDDL")
           .Produces<IEnumerable<GetVendorsForDDLSummary>>();

        group.MapGet("/", async (
             [FromQuery] int? page,
             [FromQuery] int? pageSize,
             [FromQuery] string? sortBy,
             [FromQuery] string? sortDirection,
             [FromQuery] string? search,
             ISender sender,
             CancellationToken ct) =>
             Results.Ok(await sender.Send(new GetVendorsQuery(
                 page ?? 1,
                 pageSize ?? 20,
                 sortBy,
                 sortDirection ?? "asc",
                 search), ct)))
         .WithName("GetVendors")
         .Produces<PagedResult<Application.Vendors.Queries.GetVendors.VendorSummary>>();

        group.MapGet("/{id}", async (Guid id, ISender sender, CancellationToken ct) =>
                 Results.Ok(await sender.Send(new GetVendorByIdQuery(id), ct)))
            .WithName("GetVendorById")
            //.Produces<Application.Vendors.Queries.GetVendorById.VendorSummary>()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);


        group.MapPut("/{id:guid}", async (Guid id, UpdateVendorCommand command, ISender sender, CancellationToken ct) =>
        {           
            if (id != command.Id)
            {
                return Results.BadRequest("ID in route does not match ID in body.");
            }

            var result = await sender.Send(command, ct);

            // 2. Return 200 OK or 204 No Content for a successful update
            return Results.Ok(result);
        })
            .WithName("UpdateVendor")
            .Produces<UpdateVendorResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteVendorCommand(id), ct);
                return Results.NoContent();
            })
            .WithName("DeleteVendor")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/statement", async (Guid id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
                ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetPartyStatementQuery("Vendor", id, from, to), ct)))
            .WithName("GetVendorStatement")
            .Produces<PartyStatementOfAccountDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    
        return group;
    }
}
