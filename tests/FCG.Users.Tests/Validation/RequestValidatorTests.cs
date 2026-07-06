using FCG.Users.Api.Contracts.Auth;
using FCG.Users.Api.Validation;

namespace FCG.Users.Tests.Validation;

public sealed class RequestValidatorTests
{
    [Fact]
    public void RegisterValidator_AcceptsValidRequest()
    {
        var request = new RegisterRequest(
            "Ada Lovelace",
            "ada@example.com",
            "ValidPass1!");

        var result = new RegisterRequestValidator().Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RegisterValidator_RejectsInvalidFields()
    {
        var request = new RegisterRequest("", "invalid", "weak");

        var result = new RegisterRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        Assert.Contains(result.Errors, error => error.PropertyName == "Email");
        Assert.Contains(result.Errors, error => error.PropertyName == "Password");
    }

    [Fact]
    public void LoginValidator_RequiresEmailAndPassword()
    {
        var result = new LoginRequestValidator().Validate(
            new LoginRequest("", ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Email");
        Assert.Contains(result.Errors, error => error.PropertyName == "Password");
    }
}
