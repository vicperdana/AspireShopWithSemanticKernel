using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AspireShop.BasketService.Services;

/// <summary>
/// In-memory implementation of payment session guard.
/// Prevents duplicate checkout sessions within a 2-minute window.
/// For production, consider Redis-backed implementation for distributed scenarios.
/// </summary>
public class PaymentSessionGuard : IPaymentSessionGuard
{
    private readonly ConcurrentDictionary<string, DateTime> _checkoutAttempts = new();
    private readonly ILogger<PaymentSessionGuard> _logger;
    private readonly TimeSpan _guardWindow = TimeSpan.FromMinutes(2);

    public PaymentSessionGuard(ILogger<PaymentSessionGuard> logger)
    {
        _logger = logger;
    }

    public Task<bool> CanCheckoutAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        }

        // Check if there's a recent checkout attempt
        if (_checkoutAttempts.TryGetValue(userId, out var lastAttempt))
        {
            var timeSinceLastAttempt = DateTime.UtcNow - lastAttempt;
            
            if (timeSinceLastAttempt < _guardWindow)
            {
                _logger.LogWarning(
                    "Checkout blocked for user {UserId}. Last attempt was {Seconds} seconds ago (guard window: {GuardWindowSeconds}s)",
                    userId,
                    timeSinceLastAttempt.TotalSeconds,
                    _guardWindow.TotalSeconds);
                
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    public Task RecordCheckoutAttemptAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        }

        _checkoutAttempts[userId] = DateTime.UtcNow;
        
        _logger.LogInformation(
            "Recorded checkout attempt for user {UserId} at {Timestamp:O}",
            userId,
            DateTime.UtcNow);

        // Clean up old entries (older than guard window * 2)
        CleanupOldEntries();

        return Task.CompletedTask;
    }

    public Task ClearCheckoutGuardAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        }

        _checkoutAttempts.TryRemove(userId, out _);
        
        _logger.LogInformation(
            "Cleared checkout guard for user {UserId}",
            userId);

        return Task.CompletedTask;
    }

    private void CleanupOldEntries()
    {
        var cutoffTime = DateTime.UtcNow - (_guardWindow * 2);
        var keysToRemove = _checkoutAttempts
            .Where(kvp => kvp.Value < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _checkoutAttempts.TryRemove(key, out _);
        }

        if (keysToRemove.Any())
        {
            _logger.LogDebug(
                "Cleaned up {Count} old checkout guard entries",
                keysToRemove.Count);
        }
    }
}
