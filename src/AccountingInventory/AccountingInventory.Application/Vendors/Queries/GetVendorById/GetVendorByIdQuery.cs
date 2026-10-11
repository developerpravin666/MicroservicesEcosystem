using MediatR;

namespace AccountingInventory.Application.Vendors.Queries.GetVendorById;

/// <summary>Used by a signup/login UI to check slug availability or display vendor
/// info before authentication.</summary>
public sealed record GetVendorByIdQuery(Guid Id) : IRequest<VendorSummary>;

public sealed record VendorSummary(Guid Id, string Name, string Code, string Mobile, string Email, string? Description, string? Address, bool IsActive);

