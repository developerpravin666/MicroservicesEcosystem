using MediatR;

namespace AccountingInventory.Application.Tenants.Queries.GetPendingTenants;

public sealed record PendingTenantDto(
    Guid Id,
    string Name,
    string Slug,
    string SchemaName,
    string Status,
    string? OwnerName,
    string? OwnerEmail,
    string? OwnerMobile,
    string? StateCode,
    string? Gstin,
    string? Address,
    DateTimeOffset CreatedAt,
    string? SubscribedModules = null);

public sealed record GetPendingTenantsQuery() : IRequest<IReadOnlyList<PendingTenantDto>>;

