using System.ComponentModel.DataAnnotations;

namespace Shop.Application.Orders;

public sealed record ShippingAddressDto(
    [Required, MaxLength(100)] string FullName,
    // Bangladeshi mobile numbers: 01XXXXXXXXX, optionally with +880 / 880.
    [Required, RegularExpression(@"^(\+?880|0)1[3-9]\d{8}$", ErrorMessage = "Enter a Bangladeshi mobile number, e.g. 01712345678.")]
    string Phone,
    [Required, MaxLength(200)] string Line1,
    [MaxLength(200)] string? Line2,
    [Required, MaxLength(100)] string City,
    [MaxLength(20)] string? PostalCode);

public sealed record CheckoutRequest(
    [Required] ShippingAddressDto ShippingAddress,
    [Required, RegularExpression("^(CashOnDelivery)$", ErrorMessage = "Card payments are coming soon. Please choose cash on delivery.")]
    string PaymentMethod);

public sealed record CheckoutSummaryDto(decimal Subtotal, decimal ShippingFee, decimal Total, decimal FreeShippingThreshold, int ItemCount);

public sealed record OrderHistoryQuery
{
    [Range(1, 1000)] public int Page { get; init; } = 1;
    [Range(1, 50)] public int PageSize { get; init; } = 10;
}

/// <summary>One row in the account's order history: enough to recognise the order without opening it.</summary>
public sealed record OrderSummaryDto(
    string Number,
    string Status,
    DateTime CreatedAt,
    decimal Total,
    int ItemCount,
    string FirstItemName,
    string? FirstItemImageUrl);

public sealed record OrderItemDto(Guid ProductId, string Name, decimal Price, int Quantity, decimal LineTotal);

public sealed record OrderDto(
    string Number,
    string Status,
    DateTime CreatedAt,
    string PaymentMethod,
    ShippingAddressDto ShippingAddress,
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    decimal ShippingFee,
    decimal Total);
