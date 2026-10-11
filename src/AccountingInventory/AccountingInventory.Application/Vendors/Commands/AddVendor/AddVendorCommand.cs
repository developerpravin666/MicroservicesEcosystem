using MediatR;

namespace AccountingInventory.Application.Vendors.Commands.AddVendor;

/// <summary>Self-service vendor add — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record AddVendorCommand(string Name, string Code, string Mobile, string Email, string Description, string Address) : IRequest<AddVendorResult>;

public sealed record AddVendorResult(Guid Id, string Name, string Code, string Mobile, string Email, string Description, string Address);
