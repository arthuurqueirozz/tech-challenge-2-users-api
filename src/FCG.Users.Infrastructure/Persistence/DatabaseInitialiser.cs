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
        var options = provider
            .GetRequiredService<IOptions<AdminSeedOptions>>()
            .Value;

        User? admin = null;
        if (options.Enabled)
        {
            // Validate the complete seed before making any schema changes.
            PasswordPolicy.ValidateOrThrow(options.Password);
            var email = Email.Create(options.Email);
            var hasher = provider.GetRequiredService<IPasswordHasher>();
            var timeProvider = provider.GetRequiredService<TimeProvider>();
            admin = User.Create(
                options.Name,
                email,
                hasher.Hash(options.Password),
                UserRole.Admin,
                timeProvider.GetUtcNow().UtcDateTime);
        }

        var context = provider.GetRequiredService<UsersDbContext>();
        await context.Database.MigrateAsync(cancellationToken);

        if (admin is null)
        {
            return;
        }

        var repository = provider.GetRequiredService<IUserRepository>();
        if (await repository.GetByEmailAsync(admin.Email, cancellationToken) is not null)
        {
            return;
        }

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
