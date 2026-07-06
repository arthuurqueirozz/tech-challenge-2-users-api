using FCG.Users.Domain.Entities;

namespace FCG.Users.Domain.Abstractions;

public sealed record JwtTokenResult(
    string AccessToken,
    DateTime ExpiresAtUtc);

public interface IJwtTokenService
{
    JwtTokenResult CreateToken(User user);
}
