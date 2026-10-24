using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shop.Infrastructure.Data;
using Shop.Infrastructure.Tenancy;
using Testcontainers.PostgreSql;

namespace Shop.Tests.Infrastructure;

/// <summary>Runs the real API against a throwaway Postgres container, migrated and seeded on startup.</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminPassword = "Admin-test-pass-1";
    public const string DemoPassword = "Demo-test-pass-1";

    /// <summary>Local image uploads go to a throwaway folder, never into the repo.</summary>
    public static readonly string UploadsPath = Path.Combine(Path.GetTempPath(), $"kinbo-test-uploads-{Guid.NewGuid():N}");

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17").Build();

    public Task InitializeAsync() => _db.StartAsync();

    public new Task DisposeAsync() => _db.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Seed:DemoPassword", DemoPassword);
        builder.UseSetting("Auth:SigningKey", "test-signing-key-that-is-at-least-32-bytes-long");
        builder.UseSetting("Images:LocalPath", UploadsPath);
    }

    /// <summary>
    /// An HTTPS client (so Secure cookies round-trip) with its own fake client IP, so per-IP rate limits
    /// in one test never affect another.
    /// </summary>
    public HttpClient CreateApiClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var ip = $"10.{Random.Shared.Next(256)}.{Random.Shared.Next(256)}.{Random.Shared.Next(1, 255)}";
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return client;
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
