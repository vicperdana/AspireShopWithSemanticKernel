namespace AspireShop.CatalogDb;

public class Order
{
    public Guid OrderId { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string CustomerEmail { get; set; } = string.Empty;
    public PaymentStatus PaymentStatus { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    // Navigation
    public PaymentSession? PaymentSession { get; set; }
}
