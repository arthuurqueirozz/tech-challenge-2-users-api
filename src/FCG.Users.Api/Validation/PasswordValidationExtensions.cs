using FCG.Users.Domain.Entities;
using FluentValidation;

namespace FCG.Users.Api.Validation;

public static class PasswordValidationExtensions
{
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(
        this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .Must(PasswordPolicy.IsStrong)
            .WithMessage(
                "Password must be at least 8 characters and contain letters, numbers, and special characters.");
}
