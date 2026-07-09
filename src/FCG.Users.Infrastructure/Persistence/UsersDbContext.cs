using FCG.Users.Domain.Abstractions;
using FCG.Users.Domain.Entities;
using FCG.Users.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace FCG.Users.Infrastructure.Persistence;

public sealed class UsersDbContext(
    DbContextOptions<UsersDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<IntegrationOutboxMessage> IntegrationOutboxMessages =>
        Set<IntegrationOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(UsersDbContext).Assembly);
    }
}
