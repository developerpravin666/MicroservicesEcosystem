using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Tenants.Queries.GetPendingTenants;

public sealed class GetPendingTenantsQueryHandler(ITenantDirectoryContext tenantDirectory)
    : IRequestHandler<GetPendingTenantsQuery, IReadOnlyList<PendingTenantDto>>
{
    public async Task<IReadOnlyList<PendingTenantDto>> Handle(GetPendingTenantsQuery request, CancellationToken cancellationToken)
    {
        return await tenantDirectory.Tenants
            .AsNoTracking()
            .Where(t => t.Status == TenantStatus.PendingApproval || t.Status == TenantStatus.PendingProvisioning)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new PendingTenantDto(
                t.Id,
                t.Name,
                t.Slug,
                t.SchemaName,
                t.Status.ToString(),
                t.OwnerName,
                t.OwnerEmail,
                t.OwnerMobile,
                t.StateCode,
                t.Gstin,
                t.Address,
                t.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

