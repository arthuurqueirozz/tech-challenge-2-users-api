namespace FCG.Users.Domain.Common;

public sealed class DomainConflictException(string message)
    : Exception(message);
