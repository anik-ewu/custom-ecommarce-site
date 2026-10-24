using Shop.Domain.Catalog;

namespace Shop.Domain.Carts;

public class CartItem
{
    public Guid Id { get; set; }
    public Guid CartId { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }

    /// <summary>Price when added, so checkout can tell the shopper if it changed (PRICE_CHANGED).</summary>
    public decimal UnitPrice { get; set; }

    public DateTime AddedAt { get; set; }
}
