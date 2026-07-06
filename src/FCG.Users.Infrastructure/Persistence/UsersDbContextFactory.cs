using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FCG.Users.Infrastructure.Persistence;

public sealed class UsersDbContextFactory
    : IDesignTimeDbContextFactory<UsersDbContext>
{
    public UsersDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=FcgUsersDb;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new UsersDbContext(options);
    }
}
