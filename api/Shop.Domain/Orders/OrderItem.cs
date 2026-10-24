namespace Shop.Domain.Orders;

public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public required string NameSnapshot { get; set; }
    public decimal PriceSnapshot { get; set; }
    public int Quantity { get; set; }
}
