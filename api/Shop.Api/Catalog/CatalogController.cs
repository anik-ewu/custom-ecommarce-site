using Microsoft.AspNetCore.Mvc;
using Shop.Application.Catalog;
using Shop.Application.Common;

namespace Shop.Api.Catalog;

[ApiController]
[Route("api")]
public sealed class CatalogController(CatalogService catalog) : ControllerBase
{
    [HttpGet("categories")]
    public Task<IReadOnlyList<CategoryDto>> Categories(CancellationToken ct) => catalog.GetCategoriesAsync(ct);

    [HttpGet("products")]
    public Task<Paged<ProductCardDto>> Products([FromQuery] ProductQuery query, CancellationToken ct) =>
        catalog.GetProductsAsync(query, ct);

    [HttpGet("products/{slug}")]
    public Task<ProductDetailDto> Product(string slug, CancellationToken ct) => catalog.GetProductAsync(slug, ct);
}
