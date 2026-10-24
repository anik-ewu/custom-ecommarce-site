using System.ComponentModel.DataAnnotations;
using Shop.Application.Orders;

namespace Shop.Application.Admin;

// --- Dashboard ----------------------------------------------------------------------------------------------

public sealed record LowStockDto(Guid Id, string Name, int StockQuantity);

public sealed record DashboardDto(
    decimal SalesToday,
    decimal SalesThisWeek,
    int OrdersToday,
    int PendingOrders,
    IReadOnlyList<LowStockDto> LowStock,
    IReadOnlyList<AdminOrderListItemDto> RecentOrders);

// --- Products -----------------------------------------------------------------------------------------------

public sealed record AdminProductQuery
{
    [MaxLength(100)] public string? Q { get; init; }
    [RegularExpression("^(active|archived)$")] public string? Status { get; init; }
    [Range(1, 1000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}

public sealed record AdminProductListItemDto(
    Guid Id,
    string Name,
    string Slug,
    decimal Price,
    string InventoryType,
    int StockQuantity,
    bool IsActive,
    bool IsReserved,
    string? ImageUrl,
    string CategoryName);

public sealed record AdminImageDto(Guid Id, string Url, string Alt, int SortOrder);

public sealed record AdminProductDto(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string Materials,
    string Dimensions,
    decimal Price,
    string InventoryType,
    int StockQuantity,
    bool IsActive,
    Guid CategoryId,
    Guid? ArtisanId,
    uint Version,
    IReadOnlyList<AdminImageDto> Images);

public sealed record ProductWriteRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(200), RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Use lowercase letters, numbers and dashes.")]
    string? Slug,
    [Required, MaxLength(4000)] string Description,
    [Required, MaxLength(500)] string Materials,
    [Required, MaxLength(500)] string Dimensions,
    [Range(1, 10_000_000)] decimal Price,
    [Required, RegularExpression("^(Unique|Standard)$")] string InventoryType,
    [Range(0, 100_000)] int StockQuantity,
    [Required] Guid CategoryId,
    Guid? ArtisanId,
    bool IsActive,
    // The Version (xmin) the form was loaded with; required on update so a stale form can't overwrite stock.
    uint? Version);

public sealed record AddImageRequest(
    [Required, MaxLength(500)] string Url,
    [Required, MaxLength(300)] string Alt);

public sealed record UploadResultDto(string Url);

// --- Orders -------------------------------------------------------------------------------------------------

public sealed record AdminOrderQuery
{
    [RegularExpression("^(Pending|Confirmed|Shipped|Delivered|Cancelled)$")] public string? Status { get; init; }
    [MaxLength(100)] public string? Q { get; init; }
    [Range(1, 1000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}

public sealed record AdminOrderListItemDto(
    string Number,
    string Status,
    DateTime CreatedAt,
    string CustomerName,
    string Phone,
    int ItemCount,
    decimal Total);

public sealed record AdminOrderDto(OrderDto Order, string CustomerEmail, IReadOnlyList<string> NextStatuses);

public sealed record UpdateOrderStatusRequest(
    [Required, RegularExpression("^(Confirmed|Shipped|Delivered|Cancelled)$")] string Status);

// --- Artisans -----------------------------------------------------------------------------------------------

public sealed record AdminArtisanDto(Guid Id, string Name, string Region, string Story, string? PhotoUrl, int ProductCount);

public sealed record ArtisanWriteRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(100)] string Region,
    [Required, MaxLength(4000)] string Story,
    [MaxLength(500)] string? PhotoUrl);
