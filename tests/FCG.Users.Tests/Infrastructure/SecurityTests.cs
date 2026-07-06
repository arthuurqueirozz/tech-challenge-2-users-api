using System.IdentityModel.Tokens.Jwt;
using FCG.Users.Domain.Entities;
using FCG.Users.Infrastructure.Configuration;
using FCG.Users.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace FCG.Users.Tests.Infrastructure;

public sealed class SecurityTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PasswordHasher_RoundTripSucceeds()
    {
        var hasher = new IdentityPasswordHasher();

        var hash = hasher.Hash("ValidPass1!");

        Assert.True(hasher.Verify("ValidPass1!", hash));
        Assert.False(hasher.Verify("WrongPass1!", hash));
        Assert.NotEqual("ValidPass1!", hash);
    }

    [Fact]
    public void JwtTokenService_CreatesExpectedClaimsAndExpiration()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "FCG",
            Audience = "FCG",
            Key = "test-only-key-with-at-least-32-bytes!",
            AccessTokenMinutes = 120
        });
        var user = User.Create(
            "Ada Lovelace",
            Email.Create("ada@example.com"),
            "hash",
            UserRole.Admin,
            Now.UtcDateTime);
        var service = new JwtTokenService(
            options,
            new FixedTimeProvider(Now));

        var result = service.CreateToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            result.AccessToken);

        Assert.Equal(Now.AddMinutes(120).UtcDateTime, result.ExpiresAtUtc);
        Assert.Equal("FCG", token.Issuer);
        Assert.Contains("FCG", token.Audiences);
        Assert.Equal(
            user.Id.ToString(),
            token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(
            "ada@example.com",
            token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(
            "Admin",
            token.Claims.Single(claim => claim.Type == "role").Value);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
