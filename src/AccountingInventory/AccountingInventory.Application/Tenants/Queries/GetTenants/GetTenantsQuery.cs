using MediatR;
using AccountingInventory.Application.Tenants.Queries.GetTenantBySlug;

namespace AccountingInventory.Application.Tenants.Queries.GetTenants;

public sealed record GetTenantsQuery : IRequest<IReadOnlyList<TenantSummary>>;
