using FCG.IntegrationEvents.V1;

namespace FCG.Users.Domain.Abstractions;

public interface IIntegrationEventOutbox
{
    Task AddAsync(
        UserCreatedEvent message,
        CancellationToken cancellationToken = default);
}
