using FCG.Users.Api.Contracts.Auth;
using FCG.Users.Domain.Abstractions;

namespace FCG.Users.Api.Services;

public sealed class UserProfileService(IUserRepository userRepository)
    : IUserProfileService
{
    public async Task<UserResponse?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(
            userId,
            cancellationToken);

        return user is null ? null : UserResponse.From(user);
    }
}
