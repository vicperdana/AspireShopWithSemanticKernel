using System.Diagnostics;

namespace AspireShop.ServiceDefaults.Tracing;

/// <summary>
/// Helper for creating and managing OpenTelemetry spans for checkout operations.
/// Provides minimal observability instrumentation for MVP.
/// </summary>
public static class CheckoutSpanHelper
{
    private static readonly ActivitySource ActivitySource = new("AspireShop.Checkout");

    /// <summary>
    /// Creates a span for the entire checkout session creation flow.
    /// </summary>
    public static Activity? StartCheckoutSessionSpan(string orderId, decimal amount, string currency)
    {
        var activity = ActivitySource.StartActivity("checkout.session.create");
        activity?.SetTag("order.id", orderId);
        activity?.SetTag("order.amount", amount);
        activity?.SetTag("order.currency", currency);
        return activity;
    }

    /// <summary>
    /// Creates a span for webhook processing.
    /// </summary>
    public static Activity? StartWebhookSpan(string eventType, string sessionId)
    {
        var activity = ActivitySource.StartActivity("checkout.webhook.process");
        activity?.SetTag("webhook.event_type", eventType);
        activity?.SetTag("session.id", sessionId);
        return activity;
    }

    /// <summary>
    /// Creates a span for payment session status updates.
    /// </summary>
    public static Activity? StartStatusUpdateSpan(string sessionId, string fromStatus, string toStatus)
    {
        var activity = ActivitySource.StartActivity("checkout.session.status_update");
        activity?.SetTag("session.id", sessionId);
        activity?.SetTag("status.from", fromStatus);
        activity?.SetTag("status.to", toStatus);
        return activity;
    }

    /// <summary>
    /// Creates a span for expiration sweep background job.
    /// </summary>
    public static Activity? StartExpirationSweepSpan(int sessionCount)
    {
        var activity = ActivitySource.StartActivity("checkout.expiration.sweep");
        activity?.SetTag("session.count", sessionCount);
        return activity;
    }

    /// <summary>
    /// Records an error on the current span.
    /// </summary>
    public static void RecordError(Activity? activity, Exception exception, string errorMessage)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, errorMessage);
        activity.SetTag("error.type", exception.GetType().Name);
        activity.SetTag("error.message", exception.Message);
        activity.SetTag("error.stack_trace", exception.StackTrace);
    }

    /// <summary>
    /// Marks a span as successful with optional result data.
    /// </summary>
    public static void RecordSuccess(Activity? activity, string? resultMessage = null)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Ok);
        if (!string.IsNullOrEmpty(resultMessage))
        {
            activity.SetTag("result.message", resultMessage);
        }
    }
}
