using System.Diagnostics;

namespace AspireShop.ServiceDefaults.Tracing;

public static class TracingHelper
{
    private static readonly ActivitySource ActivitySource = new("AspireShop.Payments");

    public static Activity? StartCheckoutActivity(string orderId, decimal amount)
    {
        var activity = ActivitySource.StartActivity("checkout.create", ActivityKind.Server);
        activity?.SetTag("order.id", orderId);
        activity?.SetTag("order.amount", amount);
        return activity;
    }

    public static void RecordCheckoutSuccess(Activity? activity, string sessionId)
    {
        activity?.SetTag("stripe.session_id", sessionId);
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    public static void RecordCheckoutFailure(Activity? activity, Exception ex)
    {
        activity?.SetTag("error", true);
        activity?.SetTag("error.type", ex.GetType().Name);
        activity?.SetTag("error.message", ex.Message);
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    }

    public static ActivitySource GetActivitySource() => ActivitySource;
}
