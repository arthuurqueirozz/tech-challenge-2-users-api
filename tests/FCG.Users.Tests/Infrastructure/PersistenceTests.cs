using FCG.Users.Domain.Entities;
using FCG.Users.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FCG.Users.Tests.Infrastructure;

public sealed class PersistenceTests
{
    [Fact]
    public async Task Repository_PersistsAndFindsNormalizedEmail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync(cancellationToken);
        var repository = new UserRepository(context);
        var user = CreateUser("ada@example.com");

        await repository.AddAsync(user, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var found = await repository.GetByEmailAsync(
            Email.Create("ADA@EXAMPLE.COM"),
            cancellationToken);

        Assert.NotNull(found);
        Assert.Equal(user.Id, found.Id);
        Assert.Equal("ada@example.com", found.Email.Value);
    }

    [Fact]
    public async Task Database_EnforcesUniqueEmail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync(cancellationToken);
        context.Users.Add(CreateUser("ada@example.com"));
        context.Users.Add(CreateUser("ADA@example.com"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(cancellationToken));
    }

    private static UsersDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseSqlite(connection)
            .Options;

        return new UsersDbContext(options);
    }

    private static User CreateUser(string email) =>
        User.Create(
            "Ada Lovelace",
            Email.Create(email),
            "hash",
            UserRole.User,
            new DateTime(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc));
}
