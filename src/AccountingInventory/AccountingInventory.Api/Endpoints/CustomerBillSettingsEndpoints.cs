using AccountingInventory.Application.Customers;
using BuildingBlocks.WebDefaults;
using MediatR;

namespace AccountingInventory.Api.Endpoints;

public static class CustomerBillSettingsEndpoints
{
    public static RouteGroupBuilder MapCustomerBillSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings/customer-bill")
            .WithTags("Customer Bill Settings")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetCustomerBillSettingsQuery(), cancellationToken)))
            .WithName("GetCustomerBillSettings")
            .Produces<CustomerBillTemplate>();

        group.MapPut("/", async (UpdateCustomerBillSettingsCommand command,
                ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(command, cancellationToken)))
            .WithName("UpdateCustomerBillSettings")
            .Produces<CustomerBillTemplate>()
            .ProducesValidationProblem();

        return group;
    }
}
