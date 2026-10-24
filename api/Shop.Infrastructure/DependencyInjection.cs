using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shop.Infrastructure.Data;
using Shop.Infrastructure.Seed;
using Shop.Infrastructure.Tenancy;

namespace Shop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not set.");

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<StoreResolver>();
        services.AddScoped<TenantContext>();
        services.AddDbContext<ShopDbContext>(o => ConfigureDb(o, connectionString));
        services.Configure<SeedOptions>(config.GetSection("Seed"));
        services.AddScoped<ShopSeeder>();
        return services;
    }

    internal static void ConfigureDb(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
}
