using MediatR;

namespace AccountingInventory.Application.Vendors.Commands.UpdateVendor;

/// <summary>Self-service vendor update — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record UpdateVendorCommand(Guid Id, string Name, string Code, string Mobile, string Email, bool IsActive, string Description, string Address) : IRequest<UpdateVendorResult>;

public sealed record UpdateVendorResult(Guid Id, string Name, string Code, string Mobile, string Email, bool IsActive, string Description, string Address);
