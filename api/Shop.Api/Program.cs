using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Shop.Api.Auth;
using Shop.Api.Errors;
using Shop.Api.Tenancy;
using Shop.Application;
using Shop.Infrastructure;
using Shop.Infrastructure.Data;
using Shop.Infrastructure.Images;
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

// Local image uploads (development without Cloudinary). Served before tenancy/auth: they are public images.
// Never in production: uploads are refused there without Cloudinary, and the container's app folder isn't
// writable by the app user.
if (!app.Environment.IsProduction() && app.Services.GetService<LocalImageStorage>() is { } localImages)
{
    Directory.CreateDirectory(localImages.Folder);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(localImages.Folder),
        RequestPath = LocalImageStorage.RequestPath,
        // Defence in depth against image/HTML polyglots: never let a browser sniff an upload into a page.
        OnPrepareResponse = ctx => ctx.Context.Response.Headers.XContentTypeOptions = "nosniff",
    });
}

app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/api/health") && !ctx.Request.Path.StartsWithSegments("/api/ping"),
    branch => branch.UseMiddleware<TenantMiddleware>());

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory in Shop.Tests.
public partial class Program;
