namespace Shop.Domain.Catalog;

public class Product : IStoreOwned
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public Guid? ArtisanId { get; set; }
    public Artisan? Artisan { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string Description { get; set; }
    public required string Materials { get; set; }
    public required string Dimensions { get; set; }
    public decimal Price { get; set; }
    public InventoryType InventoryType { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Postgres xmin, used as the optimistic concurrency token for stock changes.</summary>
    public uint Version { get; set; }

    public DateTime CreatedAt { get; set; }
    public List<ProductImage> Images { get; set; } = [];
}
