using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Common;
using Shop.Domain.Catalog;
using Shop.Infrastructure.Data;

namespace Shop.Application.Admin;

public sealed partial class AdminProductService(ShopDbContext db, TimeProvider clock)
{
    public async Task<Paged<AdminProductListItemDto>> ListAsync(AdminProductQuery query, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var products = db.Products.AsNoTracking();
        if (query.Status == "active") products = products.Where(p => p.IsActive);
        if (query.Status == "archived") products = products.Where(p => !p.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = $"%{query.Q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            products = products.Where(p => EF.Functions.ILike(p.Name, pattern, "\\") || EF.Functions.ILike(p.Slug, pattern, "\\"));
        }

        var total = await products.CountAsync(ct);
        var items = await products
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(p => new AdminProductListItemDto(
                p.Id, p.Name, p.Slug, p.Price, p.InventoryType.ToString(), p.StockQuantity, p.IsActive,
                db.Reservations.Any(r => r.ProductId == p.Id && r.ExpiresAt > now),
                p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                p.Category!.Name))
            .ToListAsync(ct);
        return new Paged<AdminProductListItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<AdminProductDto> GetAsync(Guid id, CancellationToken ct) =>
        ToDto(await LoadAsync(id, ct));

    public async Task<AdminProductDto> CreateAsync(ProductWriteRequest request, CancellationToken ct)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "",
            Slug = "",
            Description = "",
            Materials = "",
            Dimensions = "",
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        await ApplyAsync(product, request, ct);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    public async Task<AdminProductDto> UpdateAsync(Guid id, ProductWriteRequest request, CancellationToken ct)
    {
        if (request.Version is null)
            throw Validation("version", "Reload the product before saving.");

        var product = await LoadAsync(id, ct);
        // Optimistic concurrency on xmin: if a checkout (or another admin) changed the row since the form loaded,
        // SaveChanges affects zero rows and we refuse instead of overwriting the stock.
        db.Entry(product).Property(p => p.Version).OriginalValue = request.Version.Value;
        await ApplyAsync(product, request, ct);
        await SaveOrConflictAsync(ct);
        return ToDto(product);
    }

    /// <summary>Products are archived, not deleted: past orders and carts still point at them.</summary>
    public async Task ArchiveAsync(Guid id, CancellationToken ct)
    {
        var product = await LoadAsync(id, ct);
        product.IsActive = false;
        await SaveOrConflictAsync(ct);
    }

    public async Task<AdminProductDto> AddImageAsync(Guid id, AddImageRequest request, CancellationToken ct)
    {
        var url = request.Url.Trim();
        // Only images we stored ourselves (or another https host, e.g. Cloudinary): never script or data URLs.
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("/api/uploads/", StringComparison.Ordinal))
            throw Validation("url", "Upload the image first, then add it.");

        var product = await LoadAsync(id, ct);
        // Added through the DbSet: attached only via product.Images, a preset key would be sent as an UPDATE.
        db.ProductImages.Add(new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Url = url,
            Alt = request.Alt.Trim(),
            SortOrder = product.Images.Count == 0 ? 0 : product.Images.Max(i => i.SortOrder) + 1,
        });
        await db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    public async Task<AdminProductDto> RemoveImageAsync(Guid id, Guid imageId, CancellationToken ct)
    {
        var product = await LoadAsync(id, ct);
        var image = product.Images.FirstOrDefault(i => i.Id == imageId)
            ?? throw AppException.NotFound("That image is not on this product.");
        product.Images.Remove(image);
        await db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    private async Task ApplyAsync(Product product, ProductWriteRequest request, CancellationToken ct)
    {
        var type = Enum.Parse<InventoryType>(request.InventoryType);
        if (type == InventoryType.Unique && request.StockQuantity > 1)
            throw Validation("stockQuantity", "A one-of-a-kind piece has a stock of 0 or 1.");
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
            throw Validation("categoryId", "Choose a category.");
        if (request.ArtisanId is { } artisanId && !await db.Artisans.AnyAsync(a => a.Id == artisanId, ct))
            throw Validation("artisanId", "Choose an artisan, or none.");

        var slug = string.IsNullOrWhiteSpace(request.Slug) ? Slugify(request.Name) : request.Slug;
        if (await db.Products.AnyAsync(p => p.Slug == slug && p.Id != product.Id, ct))
        {
            throw AppException.Conflict(ErrorCodes.Conflict, "Another product already uses this web address.",
                new Dictionary<string, string[]> { ["slug"] = ["Another product already uses this web address."] });
        }

        product.Name = request.Name.Trim();
        product.Slug = slug;
        product.Description = request.Description.Trim();
        product.Materials = request.Materials.Trim();
        product.Dimensions = request.Dimensions.Trim();
        product.Price = request.Price;
        product.InventoryType = type;
        product.StockQuantity = request.StockQuantity;
        product.CategoryId = request.CategoryId;
        product.ArtisanId = request.ArtisanId;
        product.IsActive = request.IsActive;
    }

    private async Task SaveOrConflictAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw AppException.Conflict(ErrorCodes.Conflict,
                "This product changed since you opened it (perhaps an order just came in). Reload and try again.");
        }
    }

    private async Task<Product> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw AppException.NotFound("We couldn't find that product.");

    private static AdminProductDto ToDto(Product p) => new(
        p.Id, p.Name, p.Slug, p.Description, p.Materials, p.Dimensions, p.Price, p.InventoryType.ToString(),
        p.StockQuantity, p.IsActive, p.CategoryId, p.ArtisanId, p.Version,
        p.Images.OrderBy(i => i.SortOrder).Select(i => new AdminImageDto(i.Id, i.Url, i.Alt, i.SortOrder)).ToList());

    internal static string Slugify(string name)
    {
        var slug = NonAlphanumeric().Replace(name.Trim().ToLowerInvariant(), "-").Trim('-');
        return slug.Length == 0 ? "product" : slug[..Math.Min(slug.Length, 200)];
    }

    internal static AppException Validation(string field, string message) =>
        new(400, ErrorCodes.ValidationFailed, message, new Dictionary<string, string[]> { [field] = [message] });

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
