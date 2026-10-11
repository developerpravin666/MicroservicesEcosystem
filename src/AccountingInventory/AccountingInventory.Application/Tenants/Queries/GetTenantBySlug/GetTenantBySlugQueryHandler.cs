using BuildingBlocks.Application.Exceptions;
using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Tenants.Queries.GetTenantBySlug;

public sealed class GetTenantBySlugQueryHandler(ITenantDirectoryContext tenantDirectory)
    : IRequestHandler<GetTenantBySlugQuery, TenantSummary>
{
    public async Task<TenantSummary> Handle(GetTenantBySlugQuery request, CancellationToken cancellationToken)
    {
        var normalizedSlug = request.Slug.Trim().ToLowerInvariant();

        var tenant = await tenantDirectory.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Slug == normalizedSlug, cancellationToken)
            ?? throw new NotFoundException($"No tenant exists with slug '{normalizedSlug}'.");

        return new TenantSummary(tenant.Id, tenant.Name, tenant.Slug, tenant.Status.ToString());
    }
}
