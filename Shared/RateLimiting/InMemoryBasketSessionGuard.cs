namespace AspireShop.ServiceDefaults.RateLimiting;

public interface IBasketSessionGuard
{
    Task<bool> CanCreateSessionAsync(string userId, CancellationToken cancellationToken = default);
    Task RecordCheckoutAttemptAsync(string userId, CancellationToken cancellationToken = default);
}

public class InMemoryBasketSessionGuard : IBasketSessionGuard
{
    private readonly Dictionary<string, DateTime> _lastCheckouts = new();
    private readonly ChatThrottleOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public InMemoryBasketSessionGuard(ChatThrottleOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<bool> CanCreateSessionAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_lastCheckouts.TryGetValue(userId, out var lastCheckout))
            {
                var elapsed = DateTime.UtcNow - lastCheckout;
                if (elapsed.TotalMinutes < _options.PaymentSessionGuardWindowMinutes)
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RecordCheckoutAttemptAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _lastCheckouts[userId] = DateTime.UtcNow;
        }
        finally
        {
            _lock.Release();
        }
    }
}
