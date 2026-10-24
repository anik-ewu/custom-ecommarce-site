using Microsoft.EntityFrameworkCore;
using Shop.Api.Auth;
using Shop.Api.Errors;
using Shop.Api.Tenancy;
using Shop.Application;
using Shop.Infrastructure;
using Shop.Infrastructure.Data;
using Shop.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddShopProblemDetails();
builder.Services.AddShopAuth(builder.Configuration);
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

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/api/health"),
    branch => branch.UseMiddleware<TenantMiddleware>());

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory in Shop.Tests.
public partial class Program;
