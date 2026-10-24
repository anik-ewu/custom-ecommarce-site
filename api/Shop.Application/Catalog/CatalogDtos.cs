using System.ComponentModel.DataAnnotations;

namespace Shop.Application.Catalog;

public sealed record CategoryDto(Guid Id, string Name, string Slug);

public sealed record ProductQuery
{
    [MaxLength(100)] public string? Category { get; init; }
    [MaxLength(100)] public string? Q { get; init; }
    [RegularExpression("^(newest|price-asc|price-desc)$")] public string? Sort { get; init; }
    [Range(1, 1000)] public int Page { get; init; } = 1;
    [Range(1, 48)] public int PageSize { get; init; } = 12;
}

public sealed record ProductCardDto(
    Guid Id,
    string Slug,
    string Name,
    decimal Price,
    bool IsUnique,
    bool InStock,
    string? ImageUrl,
    string? ImageAlt,
    string CategorySlug,
    string? ArtisanName);

public sealed record ImageDto(string Url, string Alt);

public sealed record ArtisanSummaryDto(Guid Id, string Name, string Region, string Story, string? PhotoUrl);

public sealed record ProductDetailDto(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    string Materials,
    string Dimensions,
    decimal Price,
    bool IsUnique,
    int AvailableQuantity,
    IReadOnlyList<ImageDto> Images,
    CategoryDto Category,
    ArtisanSummaryDto? Artisan);
