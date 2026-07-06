using FCG.Users.Domain.Common;
using FCG.Users.Domain.Entities;

namespace FCG.Users.Tests.Domain;

public sealed class EmailTests
{
    [Fact]
    public void Create_NormalizesEmail()
    {
        var email = Email.Create("  USER@Example.COM  ");

        Assert.Equal("user@example.com", email.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("missing-domain@")]
    public void Create_RejectsInvalidEmail(string value)
    {
        Assert.Throws<DomainValidationException>(() => Email.Create(value));
    }
}
