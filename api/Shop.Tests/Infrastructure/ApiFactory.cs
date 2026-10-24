using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shop.Infrastructure.Data;
using Shop.Infrastructure.Tenancy;
using Testcontainers.PostgreSql;

namespace Shop.Tests.Infrastructure;

/// <summary>Runs the real API against a throwaway Postgres container, migrated and seeded on startup.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminPassword = "Admin-test-pass-1";
    public const string DemoPassword = "Demo-test-pass-1";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17").Build();

    public Task InitializeAsync() => _db.StartAsync();

    public new Task DisposeAsync() => _db.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Seed:DemoPassword", DemoPassword);
    }

    /// <summary>A DbContext scoped to the given store (default: the seeded one), for arranging and asserting.</summary>
    public async Task<(AsyncServiceScope Scope, ShopDbContext Db)> CreateDbAsync(Guid? storeId = null)
    {
        var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenant.StoreId = storeId
            ?? (await scope.ServiceProvider.GetRequiredService<StoreResolver>().ResolveAsync(db, "kinbo", default))!.Value;
        return (scope, db);
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
