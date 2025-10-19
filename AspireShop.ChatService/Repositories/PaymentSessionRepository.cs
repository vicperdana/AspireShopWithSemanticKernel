using AspireShop.CatalogDb;
using Microsoft.EntityFrameworkCore;

namespace AspireShop.ChatService.Repositories;

public interface IPaymentSessionRepository
{
    Task<PaymentSession?> GetByStripeSessionIdAsync(string stripeSessionId, CancellationToken cancellationToken = default);
    Task<PaymentSession?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task CreateAsync(PaymentSession session, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(string stripeSessionId, PaymentStatus status, CancellationToken cancellationToken = default);
    Task<List<PaymentSession>> GetExpiredSessionsAsync(DateTime expirationThreshold, CancellationToken cancellationToken = default);
}

public class PaymentSessionRepository : IPaymentSessionRepository
{
    private readonly CatalogDbContext _context;

    public PaymentSessionRepository(CatalogDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PaymentSession?> GetByStripeSessionIdAsync(string stripeSessionId, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentSessions
            .Include(ps => ps.Order)
            .FirstOrDefaultAsync(ps => ps.StripeSessionId == stripeSessionId, cancellationToken);
    }

    public async Task<PaymentSession?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentSessions
            .Include(ps => ps.Order)
            .FirstOrDefaultAsync(ps => ps.OrderId == orderId, cancellationToken);
    }

    public async Task CreateAsync(PaymentSession session, CancellationToken cancellationToken = default)
    {
        _context.PaymentSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(string stripeSessionId, PaymentStatus status, CancellationToken cancellationToken = default)
    {
        var session = await GetByStripeSessionIdAsync(stripeSessionId, cancellationToken);
        if (session != null)
        {
            session.Status = status;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<List<PaymentSession>> GetExpiredSessionsAsync(DateTime expirationThreshold, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentSessions
            .Where(ps => ps.Status == PaymentStatus.Pending && ps.ExpiresUtc < expirationThreshold)
            .ToListAsync(cancellationToken);
    }
}
