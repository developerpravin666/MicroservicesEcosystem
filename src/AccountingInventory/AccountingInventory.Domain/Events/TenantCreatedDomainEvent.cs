using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Events;

/// <summary>
/// Raised in-process when a new Tenant aggregate is created. CreateTenantCommandHandler
/// translates this, after AccountingInventory has provisioned its OWN schema and seeded
/// default roles synchronously, into the outbound
/// BuildingBlocks.Contracts.TenantCreatedIntegrationEvent published via MassTransit —
/// the signal every OTHER tenant-scoped service (InventoryService, and any future
/// Gold-Master clone) uses to provision its own schema for this tenant.
/// </summary>
public sealed record TenantCreatedDomainEvent(
    Guid TenantId,
    string Name,
    string Slug,
    string SchemaName,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;
