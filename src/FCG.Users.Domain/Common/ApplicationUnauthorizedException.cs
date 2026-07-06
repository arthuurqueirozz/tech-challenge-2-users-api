namespace FCG.Users.Domain.Common;

public sealed class ApplicationUnauthorizedException()
    : Exception("Invalid credentials.");
