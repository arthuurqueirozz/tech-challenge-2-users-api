using System.Text;
using FCG.Users.Domain.Abstractions;
using FCG.Users.Domain.Entities;
using FCG.Users.Infrastructure.Configuration;
using FCG.Users.Infrastructure.Persistence;
using FCG.Users.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Users.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration
            .GetConnectionString("UsersDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'UsersDatabase' is not configured.");

        services.AddDbContext<UsersDbContext>(options =>
            options.UseSqlServer(connectionString));

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => Encoding.UTF8.GetByteCount(options.Key) >= 32,
                "Jwt:Key must contain at least 32 bytes.")
            .ValidateOnStart();

        services
            .AddOptions<AdminSeedOptions>()
            .Bind(configuration.GetSection(AdminSeedOptions.SectionName))
            .Validate(
                options => !options.Enabled
                    || (!string.IsNullOrWhiteSpace(options.Name)
                        && !string.IsNullOrWhiteSpace(options.Email)
                        && PasswordPolicy.IsStrong(options.Password)),
                "Enabled AdminSeed requires Name, Email, and a strong Password.")
            .ValidateOnStart();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork>(
            provider => provider.GetRequiredService<UsersDbContext>());
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        return services;
    }
}
