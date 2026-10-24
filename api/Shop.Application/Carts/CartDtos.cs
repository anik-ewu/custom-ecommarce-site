using System.ComponentModel.DataAnnotations;

namespace Shop.Application.Carts;

/// <summary>Who the cart belongs to: a signed-in user, or an anonymous visitor identified by a cookie.</summary>
public sealed record CartOwner(Guid? UserId, Guid? AnonymousId);

public sealed record AddCartItemRequest([Required] Guid ProductId, [Range(1, 20)] int Quantity = 1);

public sealed record UpdateCartItemRequest([Range(1, 20)] int Quantity);

public sealed record CartItemDto(
    Guid Id,
    Guid ProductId,
    string Slug,
    string Name,
    string? ImageUrl,
    decimal UnitPrice,
    decimal CurrentPrice,
    int Quantity,
    bool IsUnique,
    DateTime? ReservedUntil,
    bool Available);

public sealed record CartDto(IReadOnlyList<CartItemDto> Items, decimal Subtotal, int ItemCount);
