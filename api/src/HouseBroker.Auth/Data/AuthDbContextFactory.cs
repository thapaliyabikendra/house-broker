using Microsoft.EntityFrameworkCore.Design;

namespace HouseBroker.Auth.Data;

/// <summary>Design-time factory for migrations.</summary>
public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    /// <inheritdoc />
    public AuthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=HouseBroker;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new AuthDbContext(options);
    }
}
