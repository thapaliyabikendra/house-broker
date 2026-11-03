using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HouseBroker.Infrastructure.Persistence;

/// <summary>Design-time factory for migrations.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <inheritdoc />
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=HouseBroker;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new AppDbContext(options);
    }
}