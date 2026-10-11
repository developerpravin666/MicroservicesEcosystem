using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Customers.Queries.SearchCustomers;

public sealed class SearchCustomersQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<SearchCustomersQuery, IReadOnlyList<CustomerLookupSummary>>
{
    public async Task<IReadOnlyList<CustomerLookupSummary>> Handle(
        SearchCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var search = request.Search?.Trim();
        if (string.IsNullOrWhiteSpace(search) || search.Length < 2)
            return [];

        var normalized = search.ToLower();
        return await db.Customers.AsNoTracking()
            .Where(customer => customer.Name.ToLower().Contains(normalized)
                || customer.Mobile.ToLower().Contains(normalized))
            .OrderBy(customer => customer.Name)
            .Take(Math.Clamp(request.Limit, 1, 20))
            .Select(customer => new CustomerLookupSummary(
                customer.Id, customer.Name, customer.Mobile, customer.Address, customer.Email, customer.Gstin, customer.StateCode, customer.StateName))
            .ToListAsync(cancellationToken);
    }
}
