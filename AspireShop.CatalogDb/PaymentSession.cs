namespace AspireShop.CatalogDb;

public class PaymentSession
{
    public string StripeSessionId { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }

    // Navigation
    public Order? Order { get; set; }
}
