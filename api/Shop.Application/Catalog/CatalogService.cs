using Microsoft.EntityFrameworkCore;
using Shop.Application.Common;
using Shop.Domain.Catalog;
using Shop.Infrastructure.Data;

namespace Shop.Application.Catalog;

public sealed class CatalogService(ShopDbContext db)
{
    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct) =>
        await db.Categories
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug))
            .ToListAsync(ct);

    public async Task<Paged<ProductCardDto>> GetProductsAsync(ProductQuery query, CancellationToken ct)
    {
        var products = db.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Category))
            products = products.Where(p => p.Category!.Slug == query.Category);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = $"%{EscapeLike(query.Q.Trim())}%";
            products = products.Where(p =>
                EF.Functions.ILike(p.Name, pattern, "\\")
                || EF.Functions.ILike(p.Description, pattern, "\\")
                || EF.Functions.ILike(p.Materials, pattern, "\\"));
        }

        products = query.Sort switch
        {
            "price-asc" => products.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "price-desc" => products.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            _ => products.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Name).ThenBy(p => p.Id),
        };

        var total = await products.CountAsync(ct);
        var items = await products
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductCardDto(
                p.Id,
                p.Slug,
                p.Name,
                p.Price,
                p.InventoryType == InventoryType.Unique,
                p.StockQuantity > 0,
                p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                p.Images.OrderBy(i => i.SortOrder).Select(i => i.Alt).FirstOrDefault(),
                p.Category!.Slug,
                p.Artisan != null ? p.Artisan.Name : null))
            .ToListAsync(ct);

        return new Paged<ProductCardDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<ProductDetailDto> GetProductAsync(string slug, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking()
            .Where(p => p.Slug == slug && p.IsActive)
            .Select(p => new ProductDetailDto(
                p.Id,
                p.Slug,
                p.Name,
                p.Description,
                p.Materials,
                p.Dimensions,
                p.Price,
                p.InventoryType == InventoryType.Unique,
                p.StockQuantity,
                p.Images.OrderBy(i => i.SortOrder).Select(i => new ImageDto(i.Url, i.Alt)).ToList(),
                new CategoryDto(p.Category!.Id, p.Category.Name, p.Category.Slug),
                p.Artisan == null
                    ? null
                    : new ArtisanSummaryDto(p.Artisan.Id, p.Artisan.Name, p.Artisan.Region, p.Artisan.Story, p.Artisan.PhotoUrl)))
            .FirstOrDefaultAsync(ct);

        return product ?? throw AppException.NotFound("We couldn't find that piece.");
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
