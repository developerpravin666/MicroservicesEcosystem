using BuildingBlocks.Application.Exceptions;
using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Tenants.Commands.ReactivateTenant;

public sealed class ReactivateTenantCommandHandler(
    ITenantDirectoryContext tenantDirectory, ITenantSchemaProvisioner schemaProvisioner, ILogger<ReactivateTenantCommandHandler> logger)
    : IRequestHandler<ReactivateTenantCommand, ReactivateTenantResult>
{
    public async Task<ReactivateTenantResult> Handle(ReactivateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantDirectory.Tenants.SingleOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            ?? throw new NotFoundException($"No tenant exists with id '{request.TenantId}'.");

        // Do not expose an outdated suspended schema to business requests.
        await schemaProvisioner.ProvisionAsync(tenant.Id, tenant.SchemaName, cancellationToken);
        tenant.Reactivate();
        await tenantDirectory.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Tenant {TenantId} ({Name}) reactivated.", tenant.Id, tenant.Name);

        return new ReactivateTenantResult(tenant.Id, tenant.Status.ToString());
    }
}
