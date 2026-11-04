using OpenIddict.EntityFrameworkCore.Models;

namespace HouseBroker.Auth.Data;

/// <summary>Application user.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Gets or sets the full name.</summary>
    public string? FullName { get; set; }
}

/// <summary>SSO database context.</summary>
public sealed class AuthDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IDataProtectionKeyContext
{
    /// <summary>Initializes the context.</summary>
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    /// <summary>Gets OpenIddict applications.</summary>
    public DbSet<OpenIddictEntityFrameworkCoreApplication> Applications => Set<OpenIddictEntityFrameworkCoreApplication>();

    /// <summary>Gets OpenIddict authorizations.</summary>
    public DbSet<OpenIddictEntityFrameworkCoreAuthorization> Authorizations => Set<OpenIddictEntityFrameworkCoreAuthorization>();

    /// <summary>Gets OpenIddict scopes.</summary>
    public DbSet<OpenIddictEntityFrameworkCoreScope> Scopes => Set<OpenIddictEntityFrameworkCoreScope>();

    /// <summary>Gets OpenIddict tokens.</summary>
    public DbSet<OpenIddictEntityFrameworkCoreToken> Tokens => Set<OpenIddictEntityFrameworkCoreToken>();

    /// <summary>Gets Data Protection keys.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("identity");
        builder.UseOpenIddict();
    }
}
