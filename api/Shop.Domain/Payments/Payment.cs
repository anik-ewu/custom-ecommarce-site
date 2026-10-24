namespace Shop.Domain.Payments;

public class Payment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public required string Provider { get; set; }
    public string? ProviderRef { get; set; }
    public PaymentStatus Status { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}
