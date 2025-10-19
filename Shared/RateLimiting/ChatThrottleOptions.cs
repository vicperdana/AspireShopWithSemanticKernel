namespace AspireShop.ServiceDefaults.RateLimiting;

public class ChatThrottleOptions
{
    public int MaxRequestsPerUser { get; set; } = 30;
    public int WindowMinutes { get; set; } = 1;
    public int PaymentSessionGuardWindowMinutes { get; set; } = 2;
}
