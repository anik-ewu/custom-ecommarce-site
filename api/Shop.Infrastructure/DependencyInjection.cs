using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shop.Infrastructure.Data;
using Shop.Infrastructure.Images;
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

        var images = config.GetSection("Images");
        services.Configure<ImageStorageOptions>(images);
        if (images.Get<ImageStorageOptions>()?.UseCloudinary == true)
            services.AddHttpClient<IImageStorage, CloudinaryImageStorage>();
        else
            services.AddSingleton<LocalImageStorage>().AddSingleton<IImageStorage>(sp => sp.GetRequiredService<LocalImageStorage>());
        return services;
    }

    internal static void ConfigureDb(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
}
