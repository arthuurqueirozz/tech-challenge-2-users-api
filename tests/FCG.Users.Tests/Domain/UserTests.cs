using FCG.Users.Domain.Common;
using FCG.Users.Domain.Entities;

namespace FCG.Users.Tests.Domain;

public sealed class UserTests
{
    private static readonly DateTime CreatedAtUtc =
        new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_NormalizesNameAndPreservesRequiredData()
    {
        var user = User.Create(
            "  Ada Lovelace  ",
            Email.Create("ada@example.com"),
            "password-hash",
            UserRole.User,
            CreatedAtUtc);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("Ada Lovelace", user.Name);
        Assert.Equal("ada@example.com", user.Email.Value);
        Assert.Equal("password-hash", user.PasswordHash);
        Assert.Equal(UserRole.User, user.Role);
        Assert.Equal(CreatedAtUtc, user.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, user.CreatedAtUtc.Kind);
    }

    [Fact]
    public void Create_RejectsMissingName()
    {
        Assert.Throws<DomainValidationException>(
            () => User.Create(
                " ",
                Email.Create("ada@example.com"),
                "hash",
                UserRole.User,
                CreatedAtUtc));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("withletters1")]
    [InlineData("without-number!")]
    [InlineData("12345678!")]
    public void PasswordPolicy_RejectsWeakPassword(string password)
    {
        Assert.False(PasswordPolicy.IsStrong(password));
    }

    [Fact]
    public void PasswordPolicy_AcceptsStrongPassword()
    {
        Assert.True(PasswordPolicy.IsStrong("ValidPass1!"));
    }
}
