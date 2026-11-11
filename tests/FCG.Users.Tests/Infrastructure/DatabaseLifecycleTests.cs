using FCG.Users.Api;
using FCG.Users.Domain.Common;
using FCG.Users.Domain.Entities;
using FCG.Users.Infrastructure;
using FCG.Users.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FCG.Users.Tests.Infrastructure;

public sealed class DatabaseLifecycleTests
{
    [Theory]
    [InlineData("Admin", "invalid-email", "StrongPass1!", typeof(DomainValidationException))]
    [InlineData("", "admin@example.com", "StrongPass1!", typeof(OptionsValidationException))]
    [InlineData("Admin", "admin@example.com", "weak", typeof(OptionsValidationException))]
    public async Task Initialiser_RejectsInvalidSeedBeforeResolvingDatabase(
        string name, string email, string password, Type exceptionType)
    {
        var configuration = Configuration(name, email, password);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddUsersInfrastructure(configuration);
        services.AddScoped<UsersDbContext>(_ => throw new InvalidOperationException("Database must not be resolved."));
        await using var provider = services.BuildServiceProvider();

        var exception = await Record.ExceptionAsync(() =>
            provider.InitialiseUsersDatabaseAsync(TestContext.Current.CancellationToken));

        Assert.IsType(exceptionType, exception);
    }

    [Fact]
    public async Task Initialiser_RejectsOverlongSeedNameBeforeResolvingDatabase()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddUsersInfrastructure(Configuration(new string('a', User.MaxNameLength + 1)));
        services.AddScoped<UsersDbContext>(_ => throw new InvalidOperationException("Database must not be resolved."));
        await using var provider = services.BuildServiceProvider();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            provider.InitialiseUsersDatabaseAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Initialiser_RepeatedSeedKeepsOriginalAccountAndPassword()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using var provider = SeedProvider(connection, Configuration());
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync(cancellationToken);

        await provider.InitialiseUsersDatabaseAsync(cancellationToken);
        var original = await context.Users.AsNoTracking().SingleAsync(cancellationToken);
        await using var repeated = SeedProvider(connection,
            Configuration("Changed Admin", " ADMIN@EXAMPLE.COM ", "DifferentPass2!"));
        await repeated.InitialiseUsersDatabaseAsync(cancellationToken);
        var stored = await context.Users.AsNoTracking().SingleAsync(cancellationToken);

        Assert.Equal(original.Id, stored.Id);
        Assert.Equal("Admin", stored.Name);
        Assert.Equal("admin@example.com", stored.Email.Value);
        Assert.Equal(UserRole.Admin, stored.Role);
        Assert.Equal(original.PasswordHash, stored.PasswordHash);
        Assert.Empty(await context.IntegrationOutboxMessages.ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task Initialiser_DoesNotPromoteOrChangeExistingUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using var provider = SeedProvider(connection, Configuration());
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync(cancellationToken);
        var original = User.Create("Existing User", Email.Create("admin@example.com"), "original-hash",
            UserRole.User, new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc));
        context.Users.Add(original);
        await context.SaveChangesAsync(cancellationToken);

        await provider.InitialiseUsersDatabaseAsync(cancellationToken);
        var stored = await context.Users.AsNoTracking().SingleAsync(cancellationToken);

        Assert.Equal(original.Id, stored.Id);
        Assert.Equal("Existing User", stored.Name);
        Assert.Equal(UserRole.User, stored.Role);
        Assert.Equal("original-hash", stored.PasswordHash);
    }

    [Theory]
    [InlineData(false, false, HealthStatus.Unhealthy)]
    [InlineData(true, false, HealthStatus.Healthy)]
    [InlineData(true, true, HealthStatus.Healthy)]
    public async Task Readiness_RequiresKnownMigrationsWithoutWritingSchema(
        bool migrated, bool futureMigration, HealthStatus expected)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using var context = CreateContext(connection);
        if (migrated)
        {
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TABLE __EFMigrationsHistory (MigrationId TEXT PRIMARY KEY, ProductVersion TEXT NOT NULL)", cancellationToken);
            foreach (var migration in context.Database.GetMigrations())
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO __EFMigrationsHistory VALUES ({migration}, {"10.0.14"})", cancellationToken);
            if (futureMigration)
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO __EFMigrationsHistory VALUES ('20990101000000_FutureCompatible', '10.0.14')", cancellationToken);
        }
        var before = await SchemaAsync(connection, cancellationToken);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUsersApi(Configuration());
        services.AddScoped(_ => CreateContext(connection));
        await using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(registration => registration.Name == "users-sqlserver", cancellationToken);

        Assert.Equal(expected, result.Status);
        Assert.Contains("ready", result.Entries["users-sqlserver"].Tags);
        Assert.Equal(before, await SchemaAsync(connection, cancellationToken));
    }

    private static IConfiguration Configuration(string name = "Admin", string email = "admin@example.com",
        string password = "StrongPass1!") => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["ConnectionStrings:UsersDatabase"] = "not-a-valid-connection-string",
            ["AdminSeed:Enabled"] = "true",
            ["AdminSeed:Name"] = name,
            ["AdminSeed:Email"] = email,
            ["AdminSeed:Password"] = password
        }).Build();

    private static ServiceProvider SeedProvider(SqliteConnection connection, IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddUsersInfrastructure(configuration);
        services.AddScoped(_ => CreateContext(connection));
        return services.BuildServiceProvider();
    }

    private static UsersDbContext CreateContext(SqliteConnection connection) => new(
        new DbContextOptionsBuilder<UsersDbContext>().UseSqlite(connection)
            .ReplaceService<IMigrator, NoOpMigrator>().Options);

    private static async Task<string> SchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT group_concat(sql, ';') FROM (SELECT sql FROM sqlite_master ORDER BY name)";
        return (await command.ExecuteScalarAsync(cancellationToken))?.ToString() ?? string.Empty;
    }

    // These seed tests use an existing SQLite schema; SQL Server migrations are validated separately.
    private sealed class NoOpMigrator : IMigrator
    {
        public NoOpMigrator() { }
        public void Migrate(string? targetMigration = null) { }
        public Task MigrateAsync(string? targetMigration = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public string GenerateScript(string? fromMigration = null, string? toMigration = null,
            MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default) => throw new NotSupportedException();
        public bool HasPendingModelChanges() => throw new NotSupportedException();
    }
}
