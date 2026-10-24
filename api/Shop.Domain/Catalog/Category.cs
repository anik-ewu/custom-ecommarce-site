namespace Shop.Domain.Catalog;

public class Category : IStoreOwned
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public int SortOrder { get; set; }
}
