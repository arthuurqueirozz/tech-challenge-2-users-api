using FCG.Users.Domain.Common;

namespace FCG.Users.Domain.Entities;

public sealed class User
{
    public const int MaxNameLength = 200;

    private User()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static User Create(
        string name,
        Email email,
        string passwordHash,
        UserRole role,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Name is required.");
        }

        var normalizedName = name.Trim();
        if (normalizedName.Length > MaxNameLength)
        {
            throw new DomainValidationException(
                $"Name must be at most {MaxNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainValidationException("Password hash is required.");
        }

        if (createdAtUtc == default)
        {
            throw new DomainValidationException("Creation date is required.");
        }

        return new User
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
        };
    }
}
