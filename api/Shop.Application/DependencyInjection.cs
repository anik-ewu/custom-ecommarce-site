using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shop.Application.Auth;
using Shop.Application.Catalog;

namespace Shop.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<AuthOptions>(config.GetSection("Auth"));
        services.AddSingleton<TokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<CatalogService>();
        return services;
    }
}
