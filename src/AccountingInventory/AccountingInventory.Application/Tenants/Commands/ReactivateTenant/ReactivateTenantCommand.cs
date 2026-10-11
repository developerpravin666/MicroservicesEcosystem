using MediatR;

namespace AccountingInventory.Application.Tenants.Commands.ReactivateTenant;

public sealed record ReactivateTenantCommand(Guid TenantId) : IRequest<ReactivateTenantResult>;

public sealed record ReactivateTenantResult(Guid TenantId, string Status);
