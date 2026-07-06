using FCG.Users.Domain.Entities;

namespace FCG.Users.Api.Contracts.Auth;

public sealed record UserResponse(
    Guid Id,
    string Name,
    string Email,
    string Role)
{
    public static UserResponse From(User user) =>
        new(
            user.Id,
            user.Name,
            user.Email.Value,
            user.Role.ToString());
}
