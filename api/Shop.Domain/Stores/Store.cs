namespace Shop.Domain.Stores;

public class Store
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
}
