namespace Shop.Application.Orders;

public sealed class ShopOptions
{
    public string Name { get; set; } = "Nokshi Kathar Math";
    public decimal ShippingFee { get; set; } = 120;
    public decimal FreeShippingThreshold { get; set; } = 3000;

    /// <summary>The shop's business day for reporting. Bangladesh is UTC+6 all year.</summary>
    public int UtcOffsetHours { get; set; } = 6;

    public decimal ShippingFor(decimal subtotal) => subtotal >= FreeShippingThreshold || subtotal == 0 ? 0 : ShippingFee;
}
