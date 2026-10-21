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
        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.ToLower();
            query = query.Where(customer => customer.Name.ToLower().Contains(normalized)
                || customer.Mobile.ToLower().Contains(normalized)
                || (customer.Email != null && customer.Email.ToLower().Contains(normalized)));
        }

        var limit = Math.Clamp(request.Limit, 1, 200);
        return await query
            .OrderBy(customer => customer.Name)
            .Take(limit)
            .Select(customer => new CustomerLookupSummary(
                customer.Id, customer.Name, customer.Mobile, customer.Address, customer.Email, customer.Gstin, customer.StateCode, customer.StateName))
            .ToListAsync(cancellationToken);
    }
}
