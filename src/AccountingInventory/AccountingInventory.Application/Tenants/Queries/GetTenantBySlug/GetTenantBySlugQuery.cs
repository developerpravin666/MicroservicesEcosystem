using MediatR;

namespace AccountingInventory.Application.Tenants.Queries.GetTenantBySlug;

/// <summary>Used by a signup/login UI to check slug availability or display tenant
/// info before authentication.</summary>
public sealed record GetTenantBySlugQuery(string Slug) : IRequest<TenantSummary>;

public sealed record TenantSummary(Guid TenantId, string Name, string Slug, string Status);
