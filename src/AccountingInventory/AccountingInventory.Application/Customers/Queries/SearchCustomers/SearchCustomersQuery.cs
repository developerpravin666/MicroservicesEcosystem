using MediatR;

namespace AccountingInventory.Application.Customers.Queries.SearchCustomers;

public sealed record SearchCustomersQuery(string? Search, int Limit = 10)
    : IRequest<IReadOnlyList<CustomerLookupSummary>>;
