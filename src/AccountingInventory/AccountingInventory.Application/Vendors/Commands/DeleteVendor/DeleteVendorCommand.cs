using MediatR;

namespace AccountingInventory.Application.Vendors.Commands.DeleteVendor;

/// <summary>Self-service vendor update — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record DeleteVendorCommand(Guid Id) : IRequest;
