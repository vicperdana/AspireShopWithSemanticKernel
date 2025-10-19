using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AspireShop.CatalogDb.Repositories;

/// <summary>
/// Repository for PaymentSession entity operations including lifecycle management.
/// </summary>
public interface IPaymentSessionRepository
{
    Task<PaymentSession?> GetByStripeSessionIdAsync(string stripeSessionId, CancellationToken cancellationToken = default);
    Task<PaymentSession?> GetPendingSessionByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<PaymentSession> CreateSessionAsync(PaymentSession session, CancellationToken cancellationToken = default);
    Task UpdateSessionStatusAsync(string stripeSessionId, PaymentStatus newStatus, CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentSession>> GetExpiredSessionsAsync(CancellationToken cancellationToken = default);
    Task MarkSessionsAsExpiredAsync(IEnumerable<string> sessionIds, CancellationToken cancellationToken = default);
}

public class PaymentSessionRepository : IPaymentSessionRepository
{
    private readonly CatalogDbContext _context;
    private readonly ILogger<PaymentSessionRepository> _logger;

    public PaymentSessionRepository(CatalogDbContext context, ILogger<PaymentSessionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaymentSession?> GetByStripeSessionIdAsync(string stripeSessionId, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentSessions
            .Include(ps => ps.Order)
            .FirstOrDefaultAsync(ps => ps.StripeSessionId == stripeSessionId, cancellationToken);
    }

    public async Task<PaymentSession?> GetPendingSessionByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentSessions
            .Where(ps => ps.OrderId == orderId && ps.Status == PaymentStatus.Pending)
            .OrderByDescending(ps => ps.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PaymentSession> CreateSessionAsync(PaymentSession session, CancellationToken cancellationToken = default)
    {
        // Ensure timestamps are set
        if (session.CreatedUtc == default)
        {
            session.CreatedUtc = DateTime.UtcNow;
        }

        // Set expiration if not set (15 minutes from creation)
        if (session.ExpiresUtc == null)
        {
            session.ExpiresUtc = session.CreatedUtc.AddMinutes(15);
        }

        // Default to pending status
        if (session.Status == default)
        {
            session.Status = PaymentStatus.Pending;
        }

        _context.PaymentSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Payment session created: SessionId={SessionId}, OrderId={OrderId}, ExpiresUtc={ExpiresUtc:O}",
            session.StripeSessionId,
            session.OrderId,
            session.ExpiresUtc);

        return session;
    }

    public async Task UpdateSessionStatusAsync(string stripeSessionId, PaymentStatus newStatus, CancellationToken cancellationToken = default)
    {
        var session = await _context.PaymentSessions
            .FirstOrDefaultAsync(ps => ps.StripeSessionId == stripeSessionId, cancellationToken);

        if (session == null)
        {
            throw new InvalidOperationException($"Payment session not found: {stripeSessionId}");
        }

        // Validate status transition (pending -> terminal states only)
        if (session.Status != PaymentStatus.Pending && newStatus != session.Status)
        {
            throw new InvalidOperationException(
                $"Invalid status transition: {session.Status} -> {newStatus}. Terminal states cannot be changed.");
        }

        session.Status = newStatus;

        // Set completion timestamp for terminal states
        if (newStatus == PaymentStatus.Paid || newStatus == PaymentStatus.Failed || newStatus == PaymentStatus.Expired)
        {
            session.CompletedUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Payment session status updated: SessionId={SessionId}, NewStatus={NewStatus}",
            stripeSessionId,
            newStatus);
    }

    public async Task<IEnumerable<PaymentSession>> GetExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _context.PaymentSessions
            .Where(ps => ps.Status == PaymentStatus.Pending 
                      && ps.ExpiresUtc != null 
                      && ps.ExpiresUtc < now)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkSessionsAsExpiredAsync(IEnumerable<string> sessionIds, CancellationToken cancellationToken = default)
    {
        var sessions = await _context.PaymentSessions
            .Where(ps => sessionIds.Contains(ps.StripeSessionId))
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.Status = PaymentStatus.Expired;
            session.CompletedUtc = DateTime.UtcNow;

            _logger.LogWarning(
                "Payment session expired: SessionId={SessionId}, ExpiresUtc={ExpiresUtc:O}",
                session.StripeSessionId,
                session.ExpiresUtc);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
