using MassTransit;

namespace BuildingBlocks.Messaging;

public sealed class EventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : class
        => publishEndpoint.Publish(integrationEvent, cancellationToken);
}
