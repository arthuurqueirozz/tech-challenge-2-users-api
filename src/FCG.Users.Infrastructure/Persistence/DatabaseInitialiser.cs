using FCG.Users.Domain.Abstractions;
using FCG.Users.Domain.Entities;
using FCG.Users.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FCG.Users.Infrastructure.Persistence;

public static class DatabaseInitialiser
{
    public static async Task InitialiseUsersDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var context = provider.GetRequiredService<UsersDbContext>();

        await context.Database.MigrateAsync(cancellationToken);

        var options = provider
            .GetRequiredService<IOptions<AdminSeedOptions>>()
            .Value;

        if (!options.Enabled)
        {
            return;
        }

        var repository = provider.GetRequiredService<IUserRepository>();
        var email = Email.Create(options.Email);

        if (await repository.GetByEmailAsync(email, cancellationToken) is not null)
        {
            return;
        }

        PasswordPolicy.ValidateOrThrow(options.Password);

        var hasher = provider.GetRequiredService<IPasswordHasher>();
        var timeProvider = provider.GetRequiredService<TimeProvider>();
        var admin = User.Create(
            options.Name,
            email,
            hasher.Hash(options.Password),
            UserRole.Admin,
            timeProvider.GetUtcNow().UtcDateTime);

        await repository.AddAsync(admin, cancellationToken);
        await provider
            .GetRequiredService<IUnitOfWork>()
            .SaveChangesAsync(cancellationToken);

        provider
            .GetRequiredService<ILogger<UsersDbContext>>()
            .LogInformation(
                "Admin seed created. UserId: {UserId}, Email: {Email}",
                admin.Id,
                admin.Email.Value);
    }
}
