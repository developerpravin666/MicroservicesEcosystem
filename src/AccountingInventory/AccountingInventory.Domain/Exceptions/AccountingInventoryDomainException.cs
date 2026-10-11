using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Exceptions;

public sealed class AccountingInventoryDomainException(string message) : DomainException(message);
