using Microsoft.Extensions.Logging;

namespace AspireShop.ServiceDefaults.Logging;

public static class LoggingExtensions
{
    // Migration events
    public static void LogAgentFrameworkMigrationStarted(this ILogger logger, string serviceName)
        => logger.LogInformation("Agent Framework migration started for service: {ServiceName}", serviceName);

    public static void LogAgentFrameworkMigrationCompleted(this ILogger logger, string serviceName)
        => logger.LogInformation("Agent Framework migration completed for service: {ServiceName}", serviceName);

    public static void LogPromptMigrated(this ILogger logger, string promptName, string oldFormat, string newFormat)
        => logger.LogDebug("Prompt migrated: {PromptName} from {OldFormat} to {NewFormat}", promptName, oldFormat, newFormat);

    // Payment events
    public static void LogPaymentSessionCreated(this ILogger logger, string sessionId, string orderId, decimal amount)
        => logger.LogInformation("Payment session created: SessionId={SessionId}, OrderId={OrderId}, Amount={Amount:C}", 
            sessionId, orderId, amount);

    public static void LogPaymentSessionExpired(this ILogger logger, string sessionId, DateTime expiresUtc)
        => logger.LogWarning("Payment session expired: SessionId={SessionId}, ExpiresUtc={ExpiresUtc:O}", 
            sessionId, expiresUtc);

    public static void LogPaymentSessionStatusTransition(this ILogger logger, string sessionId, string orderId, string fromStatus, string toStatus)
        => logger.LogInformation("Payment session status transition: SessionId={SessionId}, OrderId={OrderId}, From={FromStatus}, To={ToStatus}", 
            sessionId, orderId, fromStatus, toStatus);

    public static void LogPaymentSessionCompleted(this ILogger logger, string sessionId, string orderId, string status)
        => logger.LogInformation("Payment session completed: SessionId={SessionId}, OrderId={OrderId}, FinalStatus={Status}", 
            sessionId, orderId, status);

    public static void LogWebhookReceived(this ILogger logger, string eventType, string sessionId)
        => logger.LogInformation("Stripe webhook received: Type={EventType}, SessionId={SessionId}", 
            eventType, sessionId);

    public static void LogWebhookSignatureInvalid(this ILogger logger)
        => logger.LogError("Stripe webhook signature validation failed");

    public static void LogOrderCreated(this ILogger logger, string orderId, int itemCount, decimal total)
        => logger.LogInformation("Order created: OrderId={OrderId}, ItemCount={ItemCount}, Total={Total:C}", 
            orderId, itemCount, total);

    // Throttling events
    public static void LogThrottleExceeded(this ILogger logger, string userId, int maxRequests, int windowMinutes)
        => logger.LogWarning("User throttle exceeded: UserId={UserId}, Limit={MaxRequests}/{WindowMinutes}min", 
            userId, maxRequests, windowMinutes);

    public static void LogBasketSessionGuardTriggered(this ILogger logger, string userId, DateTime lastCheckoutUtc)
        => logger.LogWarning("Basket session guard triggered: UserId={UserId}, LastCheckout={LastCheckoutUtc:O}", 
            userId, lastCheckoutUtc);
}
