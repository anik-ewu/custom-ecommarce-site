namespace Shop.Domain.Payments;

/// <summary>One row per handled webhook event; the unique (Provider, EventId) index makes handlers idempotent.</summary>
public class ProcessedWebhookEvent
{
    public Guid Id { get; set; }
    public required string Provider { get; set; }
    public required string EventId { get; set; }
    public DateTime ProcessedAt { get; set; }
}
