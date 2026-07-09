using System.Text.Json;
using FCG.IntegrationEvents.V1;
using FCG.Users.Domain.Abstractions;

namespace FCG.Users.Infrastructure.Persistence.Outbox;

public sealed class IntegrationEventOutbox(UsersDbContext context)
    : IIntegrationEventOutbox
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task AddAsync(
        UserCreatedEvent message,
        CancellationToken cancellationToken = default)
    {
        var outboxMessage = IntegrationOutboxMessage.Create(
            message.EventId,
            UserCreatedEvent.MessageType,
            JsonSerializer.Serialize(message, JsonOptions),
            message.OccurredAtUtc);

        await context.IntegrationOutboxMessages.AddAsync(
            outboxMessage,
            cancellationToken);
    }
}
