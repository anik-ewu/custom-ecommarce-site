namespace Shop.Domain.Orders;

public class ShippingAddress
{
    public required string FullName { get; set; }
    public required string Phone { get; set; }
    public required string Line1 { get; set; }
    public string? Line2 { get; set; }
    public required string City { get; set; }
    public string? PostalCode { get; set; }
}
