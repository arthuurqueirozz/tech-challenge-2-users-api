using FCG.IntegrationEvents.V1;

namespace FCG.Users.Api.Messaging;

public interface IUserCreatedPublisher
{
    Task PublishAsync(
        UserCreatedEvent message,
        CancellationToken cancellationToken);
}
