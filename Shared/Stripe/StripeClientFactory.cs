using Stripe;

namespace AspireShop.ServiceDefaults.Stripe;

public class StripeConfiguration
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = "https://localhost:5001/checkout/success";
    public string CancelUrl { get; set; } = "https://localhost:5001/checkout/cancel";
}

public interface IStripeClientFactory
{
    SessionService CreateSessionService();
    EventService CreateEventService();
}

public class StripeClientFactory : IStripeClientFactory
{
    private readonly StripeConfiguration _configuration;

    public StripeClientFactory(StripeConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        StripeConfiguration.ApiKey = _configuration.SecretKey;
    }

    public SessionService CreateSessionService() => new SessionService();
    public EventService CreateEventService() => new EventService();
}
