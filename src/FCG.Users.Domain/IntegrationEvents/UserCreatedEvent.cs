namespace FCG.IntegrationEvents.V1;

public sealed record UserCreatedEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid UserId,
    string Name,
    string Email)
{
    public const string MessageType = "FCG.IntegrationEvents.V1.UserCreatedEvent";
}
