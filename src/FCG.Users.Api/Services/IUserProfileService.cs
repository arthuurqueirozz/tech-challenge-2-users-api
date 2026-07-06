using FCG.Users.Api.Contracts.Auth;

namespace FCG.Users.Api.Services;

public interface IUserProfileService
{
    Task<UserResponse?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
