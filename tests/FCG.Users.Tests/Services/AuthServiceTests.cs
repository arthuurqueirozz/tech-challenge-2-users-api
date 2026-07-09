using FCG.IntegrationEvents.V1;
using FCG.Users.Api.Contracts.Auth;
using FCG.Users.Api.Services;
using FCG.Users.Domain.Abstractions;
using FCG.Users.Domain.Common;
using FCG.Users.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace FCG.Users.Tests.Services;

public sealed class AuthServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RegisterAsync_PersistsUserAndOutboxMessageInSingleSave()
    {
        var repository = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var outbox = new FakeIntegrationEventOutbox(() => unitOfWork.WasSaved);
        var service = CreateService(repository, unitOfWork, outbox);
        var request = new RegisterRequest(
            "  Ada Lovelace  ",
            "ADA@Example.com",
            "ValidPass1!");

        var response = await service.RegisterAsync(
            request,
            TestContext.Current.CancellationToken);

        var stored = Assert.Single(repository.Users);
        var message = Assert.Single(outbox.Messages);
        Assert.True(unitOfWork.WasSaved);
        Assert.False(outbox.PersistenceWasCompletedWhenEnqueued);
        Assert.Equal(stored.Id, message.UserId);
        Assert.Equal(stored.CreatedAtUtc, message.OccurredAtUtc);
        Assert.Equal("Ada Lovelace", message.Name);
        Assert.Equal("ada@example.com", message.Email);
        Assert.NotEqual(Guid.Empty, message.EventId);
        Assert.Equal(stored.Id, response.User.Id);
        Assert.Equal("generated-token", response.AccessToken);
        Assert.Equal("User", response.User.Role);
    }

    [Fact]
    public async Task RegisterAsync_RejectsDuplicateEmail()
    {
        var repository = new FakeUserRepository();
        repository.Users.Add(CreateUser());
        var service = CreateService(
            repository,
            new FakeUnitOfWork(),
            new FakeIntegrationEventOutbox(() => false));

        await Assert.ThrowsAsync<DomainConflictException>(
            () => service.RegisterAsync(
                new RegisterRequest(
                    "Another User",
                    "ADA@example.com",
                    "ValidPass1!"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoginAsync_ReturnsTokenForValidCredentials()
    {
        var repository = new FakeUserRepository();
        repository.Users.Add(CreateUser());
        var service = CreateService(
            repository,
            new FakeUnitOfWork(),
            new FakeIntegrationEventOutbox(() => false));

        var response = await service.LoginAsync(
            new LoginRequest("ada@example.com", "ValidPass1!"),
            TestContext.Current.CancellationToken);

        Assert.Equal("generated-token", response.AccessToken);
        Assert.Equal("ada@example.com", response.User.Email);
    }

    [Fact]
    public async Task LoginAsync_RejectsInvalidCredentials()
    {
        var repository = new FakeUserRepository();
        repository.Users.Add(CreateUser());
        var service = CreateService(
            repository,
            new FakeUnitOfWork(),
            new FakeIntegrationEventOutbox(() => false));

        await Assert.ThrowsAsync<ApplicationUnauthorizedException>(
            () => service.LoginAsync(
                new LoginRequest("ada@example.com", "wrong"),
                TestContext.Current.CancellationToken));
    }

    private static AuthService CreateService(
        FakeUserRepository repository,
        FakeUnitOfWork unitOfWork,
        FakeIntegrationEventOutbox outbox) =>
        new(
            repository,
            unitOfWork,
            new FakePasswordHasher(),
            new FakeJwtTokenService(),
            outbox,
            new FixedTimeProvider(Now),
            NullLogger<AuthService>.Instance);

    private static User CreateUser() =>
        User.Create(
            "Ada Lovelace",
            Email.Create("ada@example.com"),
            "hash:ValidPass1!",
            UserRole.User,
            Now.UtcDateTime);

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];

        public Task<User?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.SingleOrDefault(user => user.Id == id));

        public Task<User?> GetByEmailAsync(
            Email email,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Users.SingleOrDefault(user => user.Email == email));

        public Task AddAsync(
            User user,
            CancellationToken cancellationToken = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool WasSaved { get; private set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            WasSaved = true;
            return Task.FromResult(1);
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";

        public bool Verify(string password, string passwordHash) =>
            passwordHash == $"hash:{password}";
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public JwtTokenResult CreateToken(User user) =>
            new("generated-token", Now.AddHours(2).UtcDateTime);
    }

    private sealed class FakeIntegrationEventOutbox(Func<bool> persistenceWasCompleted)
        : IIntegrationEventOutbox
    {
        public List<UserCreatedEvent> Messages { get; } = [];
        public bool PersistenceWasCompletedWhenEnqueued { get; private set; }

        public Task AddAsync(
            UserCreatedEvent message,
            CancellationToken cancellationToken = default)
        {
            PersistenceWasCompletedWhenEnqueued = persistenceWasCompleted();
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
