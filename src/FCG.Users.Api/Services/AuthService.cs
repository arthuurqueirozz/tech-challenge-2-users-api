using FCG.IntegrationEvents.V1;
using FCG.Users.Api.Contracts.Auth;
using FCG.Users.Domain.Abstractions;
using FCG.Users.Domain.Common;
using FCG.Users.Domain.Entities;

namespace FCG.Users.Api.Services;

public sealed class AuthService(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IIntegrationEventOutbox integrationEventOutbox,
    TimeProvider timeProvider,
    ILogger<AuthService> logger)
    : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);
        if (await userRepository.GetByEmailAsync(email, cancellationToken) is not null)
        {
            throw new DomainConflictException("Email is already registered.");
        }

        PasswordPolicy.ValidateOrThrow(request.Password);

        var user = User.Create(
            request.Name,
            email,
            passwordHasher.Hash(request.Password),
            UserRole.User,
            timeProvider.GetUtcNow().UtcDateTime);

        await userRepository.AddAsync(user, cancellationToken);

        var message = new UserCreatedEvent(
            Guid.NewGuid(),
            user.CreatedAtUtc,
            user.Id,
            user.Name,
            user.Email.Value);

        await integrationEventOutbox.AddAsync(message, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User registered and UserCreatedEvent enqueued. UserId: {UserId}, EventId: {EventId}",
            user.Id,
            message.EventId);

        return CreateAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);
        var user = await userRepository.GetByEmailAsync(
            email,
            cancellationToken);

        if (user is null
            || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new ApplicationUnauthorizedException();
        }

        return CreateAuthResponse(user);
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var token = jwtTokenService.CreateToken(user);

        return new AuthResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            UserResponse.From(user));
    }
}
