using FCG.IntegrationEvents.V1;
using MassTransit;

namespace FCG.Users.Api.Messaging;

public sealed class MassTransitUserCreatedPublisher(
    IPublishEndpoint publishEndpoint)
    : IUserCreatedPublisher
{
    public Task PublishAsync(
        UserCreatedEvent message,
        CancellationToken cancellationToken) =>
        publishEndpoint.Publish(message, cancellationToken);
}
