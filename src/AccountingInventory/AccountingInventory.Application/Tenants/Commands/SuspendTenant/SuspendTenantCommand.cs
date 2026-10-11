using MediatR;

namespace AccountingInventory.Application.Tenants.Commands.SuspendTenant;

public sealed record SuspendTenantCommand(Guid TenantId) : IRequest<SuspendTenantResult>;

public sealed record SuspendTenantResult(Guid TenantId, string Status);
