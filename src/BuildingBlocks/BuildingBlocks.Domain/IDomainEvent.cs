namespace BuildingBlocks.Domain;

/// <summary>
/// Marker for internal domain events raised by aggregates during a business
/// operation, distinct from the integration events published to RabbitMQ via
/// MassTransit. A domain event stays in-process until the Infrastructure layer
/// decides to translate it into an outbound integration event after SaveChanges
/// succeeds (see each service's *CommandHandler for that translation step).
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredOnUtc { get; }
}

/// <summary>
/// Base type for any aggregate root that raises domain events. Shared across every
/// service so aggregates in Identity, Inventory, and Tenant all get identical,
/// well-tested domain-event bookkeeping without copy-pasting it.
/// </summary>
public abstract class AggregateRoot : AuditableEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
