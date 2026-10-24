using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Admin;
using Shop.Application.Common;

namespace Shop.Api.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/products")]
public sealed class AdminProductsController(AdminProductService products) : ControllerBase
{
    [HttpGet]
    public Task<Paged<AdminProductListItemDto>> List([FromQuery] AdminProductQuery query, CancellationToken ct) =>
        products.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<AdminProductDto> Get(Guid id, CancellationToken ct) => products.GetAsync(id, ct);

    [HttpPost]
    public Task<AdminProductDto> Create(ProductWriteRequest request, CancellationToken ct) => products.CreateAsync(request, ct);

    [HttpPut("{id:guid}")]
    public Task<AdminProductDto> Update(Guid id, ProductWriteRequest request, CancellationToken ct) =>
        products.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await products.ArchiveAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/images")]
    public Task<AdminProductDto> AddImage(Guid id, AddImageRequest request, CancellationToken ct) =>
        products.AddImageAsync(id, request, ct);

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public Task<AdminProductDto> RemoveImage(Guid id, Guid imageId, CancellationToken ct) =>
        products.RemoveImageAsync(id, imageId, ct);
}
