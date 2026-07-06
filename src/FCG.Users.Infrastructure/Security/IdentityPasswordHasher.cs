using FCG.Users.Domain.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace FCG.Users.Infrastructure.Security;

public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private static readonly object Marker = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) =>
        _hasher.HashPassword(Marker, password);

    public bool Verify(string password, string passwordHash) =>
        _hasher.VerifyHashedPassword(Marker, passwordHash, password)
        is not PasswordVerificationResult.Failed;
}
