using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Shop.Infrastructure.Tenancy;

namespace Shop.Infrastructure.Data;

/// <summary>Used only by `dotnet ef migrations`; it never connects, so no credentials are needed.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ShopDbContext>
{
    public ShopDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ShopDbContext>();
        DependencyInjection.ConfigureDb(options, "Host=localhost;Database=shop");
        return new ShopDbContext(options.Options, new TenantContext());
    }
}
