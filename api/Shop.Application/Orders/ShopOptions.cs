namespace Shop.Application.Orders;

public sealed class ShopOptions
{
    public string Name { get; set; } = "Kinbo";
    public decimal ShippingFee { get; set; } = 120;
    public decimal FreeShippingThreshold { get; set; } = 3000;

    public decimal ShippingFor(decimal subtotal) => subtotal >= FreeShippingThreshold || subtotal == 0 ? 0 : ShippingFee;
}
