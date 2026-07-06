using FCG.Users.Api.Contracts.Auth;
using FCG.Users.Domain.Entities;
using FluentValidation;

namespace FCG.Users.Api.Validation;

public sealed class RegisterRequestValidator
    : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(User.MaxNameLength);

        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(request => request.Password)
            .StrongPassword();
    }
}
