namespace Shop.Domain.Catalog;

public class Artisan : IStoreOwned
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public required string Name { get; set; }
    public required string Region { get; set; }
    public required string Story { get; set; }
    public string? PhotoUrl { get; set; }
}
