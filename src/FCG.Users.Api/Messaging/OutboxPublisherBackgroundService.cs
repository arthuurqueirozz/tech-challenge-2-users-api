using System.Text.Json;
using FCG.IntegrationEvents.V1;
using FCG.Users.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FCG.Users.Api.Messaging;

public sealed class OutboxPublisherBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<OutboxPublisherBackgroundService> logger)
    : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unexpected failure while publishing outbox messages.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task PublishPendingMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IUserCreatedPublisher>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var pendingMessages = await context.IntegrationOutboxMessages
            .Where(message =>
                message.ProcessedAtUtc == null
                && message.NextAttemptAtUtc <= now)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var outboxMessage in pendingMessages)
        {
            try
            {
                if (outboxMessage.MessageType != UserCreatedEvent.MessageType)
                {
                    throw new InvalidOperationException(
                        $"Unsupported outbox message type '{outboxMessage.MessageType}'.");
                }

                var message = JsonSerializer.Deserialize<UserCreatedEvent>(
                    outboxMessage.Payload,
                    JsonOptions);

                if (message is null)
                {
                    throw new InvalidOperationException(
                        "Outbox UserCreatedEvent payload could not be deserialized.");
                }

                await publisher.PublishAsync(message, cancellationToken);
                outboxMessage.MarkProcessed(
                    timeProvider.GetUtcNow().UtcDateTime);

                logger.LogInformation(
                    "UserCreatedEvent published from outbox. UserId: {UserId}, EventId: {EventId}",
                    message.UserId,
                    message.EventId);
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException)
            {
                outboxMessage.MarkFailed(
                    exception.Message,
                    timeProvider.GetUtcNow().UtcDateTime);

                logger.LogWarning(
                    exception,
                    "Failed to publish outbox message. MessageId: {MessageId}, AttemptCount: {AttemptCount}",
                    outboxMessage.Id,
                    outboxMessage.AttemptCount);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
