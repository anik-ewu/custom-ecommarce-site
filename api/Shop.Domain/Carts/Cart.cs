namespace Shop.Domain.Carts;

public class Cart : IStoreOwned
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? AnonymousId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CartItem> Items { get; set; } = [];
}
