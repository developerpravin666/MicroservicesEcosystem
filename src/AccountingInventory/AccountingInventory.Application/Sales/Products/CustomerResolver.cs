using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Sales.Products;

internal static class CustomerResolver
{
    public static async Task<Customer> GetOrCreateAsync(
        IAccountingInventoryDbContext db,
        string name,
        string mobile,
        string address,
        string? email,
        CancellationToken cancellationToken,
        string? stateCode = null,
        string? stateName = null,
        string? gstin = null)
    {
        var normalizedMobile = mobile.Trim();
        var customer = await db.Customers.SingleOrDefaultAsync(
            item => item.Mobile == normalizedMobile, cancellationToken);
        if (customer is null)
        {
            customer = Customer.Create(name, normalizedMobile, address, email, stateCode, stateName, gstin);
            db.Customers.Add(customer);
        }
        else
        {
            customer.UpdateContactDetails(name, normalizedMobile, address, email, stateCode, stateName, gstin);
        }

        return customer;
    }
}
