using Shop.Infrastructure.Data;
using Shop.Infrastructure.Tenancy;

namespace Shop.Api.Tenancy;

/// <summary>Resolves the request's store once (single store for now, slug from config) before any query runs.</summary>
public sealed class TenantMiddleware(RequestDelegate next, IConfiguration config)
{
    private readonly string _slug = config["Shop:StoreSlug"] ?? "kinbo";

    public async Task InvokeAsync(HttpContext http, TenantContext tenant, StoreResolver resolver, ShopDbContext db)
    {
        var storeId = await resolver.ResolveAsync(db, _slug, http.RequestAborted);
        if (storeId is null)
        {
            await Results.Problem(
                title: "Store is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                extensions: new Dictionary<string, object?> { ["code"] = "STORE_NOT_CONFIGURED" }).ExecuteAsync(http);
            return;
        }

        tenant.StoreId = storeId.Value;
        await next(http);
    }
}
