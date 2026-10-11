using BuildingBlocks.Application.Exceptions;
using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Tenants.Commands.SuspendTenant;

public sealed class SuspendTenantCommandHandler(
    ITenantDirectoryContext tenantDirectory, ILogger<SuspendTenantCommandHandler> logger)
    : IRequestHandler<SuspendTenantCommand, SuspendTenantResult>
{
    public async Task<SuspendTenantResult> Handle(SuspendTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantDirectory.Tenants.SingleOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            ?? throw new NotFoundException($"No tenant exists with id '{request.TenantId}'.");

        tenant.Suspend();
        await tenantDirectory.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Tenant {TenantId} ({Name}) suspended.", tenant.Id, tenant.Name);

        return new SuspendTenantResult(tenant.Id, tenant.Status.ToString());
    }
}
