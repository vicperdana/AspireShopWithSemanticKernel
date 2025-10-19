using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AspireShop.CatalogDb.Repositories;

/// <summary>
/// Repository for Order entity operations including creation and status updates.
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<Order?> GetByCustomerEmailAsync(string customerEmail, CancellationToken cancellationToken = default);
    Task<Order> CreateOrderAsync(Order order, CancellationToken cancellationToken = default);
    Task UpdateOrderStatusAsync(Guid orderId, PaymentStatus newStatus, CancellationToken cancellationToken = default);
    Task<IEnumerable<Order>> GetOrdersByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default);
}

public class OrderRepository : IOrderRepository
{
    private readonly CatalogDbContext _context;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(CatalogDbContext context, ILogger<OrderRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
    }

    public async Task<Order?> GetByCustomerEmailAsync(string customerEmail, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerEmail == customerEmail)
            .OrderByDescending(o => o.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Order> CreateOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        // Ensure timestamps are set
        if (order.CreatedUtc == default)
        {
            order.CreatedUtc = DateTime.UtcNow;
        }
        
        if (order.UpdatedUtc == default)
        {
            order.UpdatedUtc = DateTime.UtcNow;
        }

        // Validate order has items
        if (!order.Items.Any())
        {
            throw new InvalidOperationException("Order must have at least one item");
        }

        // Calculate total if not set
        if (order.TotalAmount == 0)
        {
            order.TotalAmount = order.Items.Sum(item => item.Quantity * item.UnitPrice);
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Order created: OrderId={OrderId}, ItemCount={ItemCount}, Total={Total:C}",
            order.OrderId,
            order.Items.Count,
            order.TotalAmount);

        return order;
    }

    public async Task UpdateOrderStatusAsync(Guid orderId, PaymentStatus newStatus, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders.FindAsync([orderId], cancellationToken);
        
        if (order == null)
        {
            throw new InvalidOperationException($"Order not found: {orderId}");
        }

        // Validate status transition
        if (order.PaymentStatus != PaymentStatus.Pending && newStatus != order.PaymentStatus)
        {
            throw new InvalidOperationException(
                $"Invalid status transition: {order.PaymentStatus} -> {newStatus}. Terminal states cannot be changed.");
        }

        order.PaymentStatus = newStatus;
        order.UpdatedUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Order status updated: OrderId={OrderId}, NewStatus={NewStatus}",
            orderId,
            newStatus);
    }

    public async Task<IEnumerable<Order>> GetOrdersByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.PaymentStatus == status)
            .OrderByDescending(o => o.CreatedUtc)
            .ToListAsync(cancellationToken);
    }
}
