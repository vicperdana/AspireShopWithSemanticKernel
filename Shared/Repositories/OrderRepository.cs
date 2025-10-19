using AspireShop.CatalogDb;
using Microsoft.EntityFrameworkCore;

namespace AspireShop.ServiceDefaults.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(string orderId, CancellationToken cancellationToken = default);
    Task<Order?> GetByStripeSessionIdAsync(string stripeSessionId, CancellationToken cancellationToken = default);
    Task CreateAsync(Order order, CancellationToken cancellationToken = default);
    Task UpdatePaymentStatusAsync(string orderId, PaymentStatus status, CancellationToken cancellationToken = default);
}

public class OrderRepository : IOrderRepository
{
    private readonly CatalogDbContext _context;

    public OrderRepository(CatalogDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Order?> GetByIdAsync(string orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.PaymentSession)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
    }

    public async Task<Order?> GetByStripeSessionIdAsync(string stripeSessionId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.PaymentSession)
            .FirstOrDefaultAsync(o => o.PaymentSession != null && o.PaymentSession.StripeSessionId == stripeSessionId, cancellationToken);
    }

    public async Task CreateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePaymentStatusAsync(string orderId, PaymentStatus status, CancellationToken cancellationToken = default)
    {
        var order = await GetByIdAsync(orderId, cancellationToken);
        if (order != null)
        {
            order.PaymentStatus = status;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
