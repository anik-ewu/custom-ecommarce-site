using Microsoft.EntityFrameworkCore;
using Shop.Api.Tenancy;
using Shop.Infrastructure;
using Shop.Infrastructure.Data;
using Shop.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Database.MigrateAsync();
    if (app.Configuration.GetValue("Seed:Enabled", true))
        await scope.ServiceProvider.GetRequiredService<ShopSeeder>().SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/api/health"),
    branch => branch.UseMiddleware<TenantMiddleware>());

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory in Shop.Tests.
public partial class Program;
