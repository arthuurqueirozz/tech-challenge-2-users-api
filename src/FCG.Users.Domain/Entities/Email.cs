using System.Text.RegularExpressions;
using FCG.Users.Domain.Common;

namespace FCG.Users.Domain.Entities;

public sealed class Email : IEquatable<Email>
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Email Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainValidationException("Email is required.");
        }

        var normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length > 320 || !EmailRegex.IsMatch(normalized))
        {
            throw new DomainValidationException("Email format is invalid.");
        }

        return new Email(normalized);
    }

    public bool Equals(Email? other) =>
        other is not null && Value == other.Value;

    public override bool Equals(object? obj) =>
        obj is Email email && Equals(email);

    public override int GetHashCode() =>
        Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;

    public static bool operator ==(Email? left, Email? right) =>
        ReferenceEquals(left, right)
        || (left is not null && left.Equals(right));

    public static bool operator !=(Email? left, Email? right) =>
        !(left == right);
}
