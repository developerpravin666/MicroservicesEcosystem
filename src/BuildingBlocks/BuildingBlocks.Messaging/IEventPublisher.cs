namespace BuildingBlocks.Messaging;

/// <summary>
/// Dependency-inversion seam over the message broker, shared by every service's
/// Application layer so business handlers depend only on this narrow contract rather
/// than MassTransit's full surface area. Implemented once, here, wrapping
/// MassTransit's IPublishEndpoint.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : class;
}
