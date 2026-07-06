using FCG.Users.Domain.Common;

namespace FCG.Users.Domain.Entities;

public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    public static bool IsStrong(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
        {
            return false;
        }

        var hasLetter = password.Any(char.IsLetter);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(character => !char.IsLetterOrDigit(character));

        return hasLetter && hasDigit && hasSpecial;
    }

    public static void ValidateOrThrow(string password)
    {
        if (!IsStrong(password))
        {
            throw new DomainValidationException(
                "Password must be at least 8 characters and contain letters, numbers, and special characters.");
        }
    }
}
