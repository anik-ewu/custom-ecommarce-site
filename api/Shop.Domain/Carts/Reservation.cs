namespace Shop.Domain.Carts;

/// <summary>A 15-minute hold on a unique product. ProductId is unique, so a piece has at most one hold.</summary>
public class Reservation
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid CartId { get; set; }
    public int Quantity { get; set; }
    public DateTime ExpiresAt { get; set; }
}
