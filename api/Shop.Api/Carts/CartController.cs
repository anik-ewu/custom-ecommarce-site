using Microsoft.AspNetCore.Mvc;
using Shop.Application.Carts;

namespace Shop.Api.Carts;

[ApiController]
[Route("api/cart")]
public sealed class CartController(CartService carts) : ControllerBase
{
    [HttpGet]
    public Task<CartDto> Get(CancellationToken ct) =>
        carts.GetAsync(CartOwnerResolver.Resolve(HttpContext, createAnonymous: false), ct);

    [HttpPost("items")]
    public Task<CartDto> Add(AddCartItemRequest request, CancellationToken ct) =>
        carts.AddAsync(CartOwnerResolver.Resolve(HttpContext, createAnonymous: true), request, ct);

    [HttpPatch("items/{id:guid}")]
    public Task<CartDto> Update(Guid id, UpdateCartItemRequest request, CancellationToken ct) =>
        carts.UpdateAsync(CartOwnerResolver.Resolve(HttpContext, createAnonymous: false), id, request, ct);

    [HttpDelete("items/{id:guid}")]
    public Task<CartDto> Remove(Guid id, CancellationToken ct) =>
        carts.RemoveAsync(CartOwnerResolver.Resolve(HttpContext, createAnonymous: false), id, ct);
}
