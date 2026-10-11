using MediatR;

namespace AccountingInventory.Application.Tenants.Commands.RegisterTenant;

/// <summary>Self-service tenant signup command with store and owner contact details.</summary>
public sealed record RegisterTenantCommand(
    string Name,
    string Slug,
    string? OwnerName = null,
    string? OwnerEmail = null,
    string? OwnerMobile = null,
    string? Password = null,
    string? StateCode = null,
    string? Gstin = null,
    string? Address = null) : IRequest<RegisterTenantResult>;

public sealed record RegisterTenantResult(
    Guid TenantId,
    string Name,
    string Slug,
    string SchemaName,
    string Status,
    string Message);
