namespace Shop.Domain.Orders;

public class Order : IStoreOwned
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid UserId { get; set; }
    public required string Number { get; set; }
    public OrderStatus Status { get; set; }
    public required ShippingAddress ShippingAddress { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}
