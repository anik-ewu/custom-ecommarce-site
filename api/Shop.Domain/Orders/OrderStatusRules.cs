namespace Shop.Domain.Orders;

/// <summary>Pending → Confirmed → Shipped → Delivered, or Cancelled from Pending/Confirmed (spec §6).</summary>
public static class OrderStatusRules
{
    public static IReadOnlyList<OrderStatus> NextFrom(OrderStatus current) => current switch
    {
        OrderStatus.Pending => [OrderStatus.Confirmed, OrderStatus.Cancelled],
        OrderStatus.Confirmed => [OrderStatus.Shipped, OrderStatus.Cancelled],
        OrderStatus.Shipped => [OrderStatus.Delivered],
        _ => [],
    };

    public static bool CanMove(OrderStatus from, OrderStatus to) => NextFrom(from).Contains(to);
}
