namespace FCG.Users.Infrastructure.Persistence.Outbox;

public sealed class IntegrationOutboxMessage
{
    private IntegrationOutboxMessage()
    {
    }

    public Guid Id { get; private set; }
    public string MessageType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public static IntegrationOutboxMessage Create(
        Guid id,
        string messageType,
        string payload,
        DateTime occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return new IntegrationOutboxMessage
        {
            Id = id,
            MessageType = messageType,
            Payload = payload,
            OccurredAtUtc = occurredAtUtc,
            NextAttemptAtUtc = occurredAtUtc
        };
    }

    public void MarkProcessed(DateTime processedAtUtc)
    {
        ProcessedAtUtc = processedAtUtc;
        LastError = null;
    }

    public void MarkFailed(
        string error,
        DateTime failedAtUtc)
    {
        AttemptCount++;
        LastError = error.Length > 2_048
            ? error[..2_048]
            : error;

        var retryDelaySeconds = Math.Min(
            300,
            Math.Pow(2, Math.Min(AttemptCount, 8)));

        NextAttemptAtUtc = failedAtUtc.AddSeconds(retryDelaySeconds);
    }
}
