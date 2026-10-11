namespace BuildingBlocks.Contracts;

/// <summary>
/// Published by TenantService via RabbitMQ (MassTransit) the moment a new tenant is
/// created. Every tenant-scoped service (AccountingInventory, InventoryService, and any
/// future Gold-Master clone) consumes this to provision its own PostgreSQL schema for
/// the new tenant — see each service's TenantProvisioningConsumer — and, in
/// AccountingInventory's case, to also seed the tenant's default roles.
///
/// This is deliberately the ONLY type any other service is allowed to reference from
/// TenantService — it lives in this small, dependency-free shared contracts library
/// instead of TenantService.Application, so consuming services never take a project
/// (or even a conceptual) dependency on TenantService's internals, only on the wire
/// contract they've agreed to interoperate on.
/// </summary>
public sealed record TenantCreatedIntegrationEvent(
    Guid TenantId,
    string Name,
    string Slug,
    string SchemaName,
    DateTimeOffset OccurredOnUtc);
