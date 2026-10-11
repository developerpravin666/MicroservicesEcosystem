using MediatR;

namespace AccountingInventory.Application.Tenants.Commands.RejectTenant;

public sealed record RejectTenantCommand(Guid TenantId, string? Reason = null) : IRequest<RejectTenantResult>;

public sealed record RejectTenantResult(
    Guid TenantId,
    string Name,
    string Slug,
    string Status,
    string? RejectionReason,
    string Message);

