using System.Collections.Concurrent;

namespace AspireShop.ServiceDefaults.RateLimiting;

public interface IChatThrottle
{
    bool IsAllowed(string userId);
    void Reset(string userId);
}

public class InMemoryChatThrottle : IChatThrottle
{
    private readonly ChatThrottleOptions _options;
    private readonly ConcurrentDictionary<string, UserRequestWindow> _requestWindows = new();

    public InMemoryChatThrottle(ChatThrottleOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public bool IsAllowed(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var window = _requestWindows.GetOrAdd(userId, _ => new UserRequestWindow());

        lock (window)
        {
            // Remove expired timestamps
            window.Timestamps.RemoveAll(t => (now - t).TotalMinutes > _options.WindowMinutes);

            if (window.Timestamps.Count >= _options.MaxRequestsPerUser)
            {
                return false;
            }

            window.Timestamps.Add(now);
            return true;
        }
    }

    public void Reset(string userId)
    {
        _requestWindows.TryRemove(userId, out _);
    }

    private class UserRequestWindow
    {
        public List<DateTime> Timestamps { get; } = new();
    }
}
