using MediatR;

namespace AccountingInventory.Application.Tenants.Commands.ApproveTenant;

public sealed record ApproveTenantCommand(Guid TenantId) : IRequest<ApproveTenantResult>;

public sealed record ApproveTenantResult(
    Guid TenantId,
    string Name,
    string Slug,
    string SchemaName,
    string Status,
    string OwnerUsername,
    string Message);

