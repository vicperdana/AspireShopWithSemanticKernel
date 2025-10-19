using AspireShop.CatalogDb;
using AspireShop.ChatService.Repositories;
using AspireShop.ServiceDefaults.Stripe;
using AspireShop.ServiceDefaults.Logging;
using AspireShop.ServiceDefaults.Tracing;
using Microsoft.Extensions.Logging;
using Stripe.Checkout;

namespace AspireShop.ChatService.Services;

public interface IPaymentSessionService
{
    Task<PaymentSessionResult> CreatePaymentSessionAsync(string userId, string customerEmail, List<CheckoutItemDto> items, CancellationToken cancellationToken = default);
    Task<PaymentSession?> GetSessionAsync(string stripeSessionId, CancellationToken cancellationToken = default);
    Task HandleWebhookEventAsync(string eventType, string stripeSessionId, CancellationToken cancellationToken = default);
}

public class PaymentSessionService : IPaymentSessionService
{
    private readonly IPaymentSessionRepository _sessionRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IStripeClientFactory _stripeFactory;
    private readonly StripeSettings _stripeConfig;
    private readonly ILogger<PaymentSessionService> _logger;

    public PaymentSessionService(
        IPaymentSessionRepository sessionRepository,
        IOrderRepository orderRepository,
        IStripeClientFactory stripeFactory,
        StripeSettings stripeConfig,
        ILogger<PaymentSessionService> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _stripeFactory = stripeFactory ?? throw new ArgumentNullException(nameof(stripeFactory));
        _stripeConfig = stripeConfig ?? throw new ArgumentNullException(nameof(stripeConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PaymentSessionResult> CreatePaymentSessionAsync(
        string userId,
        string customerEmail,
        List<CheckoutItemDto> items,
        CancellationToken cancellationToken = default)
    {
        using var activity = TracingHelper.StartCheckoutActivity(Guid.NewGuid().ToString(), items.Sum(i => i.UnitPrice * i.Quantity));

        try
        {
            // Create order
            var orderId = Guid.NewGuid();
            var order = new Order
            {
                OrderId = orderId,
                CustomerEmail = customerEmail,
                TotalAmount = items.Sum(i => i.UnitPrice * i.Quantity),
                PaymentStatus = PaymentStatus.Pending,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                Items = items.Select(i => new OrderItem
                {
                    OrderItemId = Guid.NewGuid(),
                    OrderId = orderId,
                    CatalogItemId = i.CatalogItemId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };

            await _orderRepository.CreateAsync(order, cancellationToken);

            // Create Stripe checkout session
            var sessionService = _stripeFactory.CreateSessionService();
            var sessionOptions = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = items.Select(i => new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = i.ProductName
                        },
                        UnitAmount = (long)(i.UnitPrice * 100) // Convert to cents
                    },
                    Quantity = i.Quantity
                }).ToList(),
                Mode = "payment",
                SuccessUrl = _stripeConfig.SuccessUrl,
                CancelUrl = _stripeConfig.CancelUrl,
                CustomerEmail = customerEmail,
                Metadata = new Dictionary<string, string>
                {
                    { "order_id", orderId.ToString() },
                    { "user_id", userId }
                }
            };

            var session = await sessionService.CreateAsync(sessionOptions, cancellationToken: cancellationToken);

            // Create payment session record
            var paymentSession = new PaymentSession
            {
                StripeSessionId = session.Id,
                OrderId = orderId,
                CustomerEmail = customerEmail,
                Status = PaymentStatus.Pending,
                CreatedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(15)
            };

            await _sessionRepository.CreateAsync(paymentSession, cancellationToken);

            _logger.LogPaymentSessionCreated(session.Id, orderId.ToString(), order.TotalAmount);
            TracingHelper.RecordCheckoutSuccess(activity, session.Id);

            return new PaymentSessionResult(session.Id, session.Url, orderId.ToString());
        }
        catch (Exception ex)
        {
            TracingHelper.RecordCheckoutFailure(activity, ex);
            _logger.LogError(ex, "Failed to create payment session for user {UserId}", userId);
            throw;
        }
    }

    public async Task<PaymentSession?> GetSessionAsync(string stripeSessionId, CancellationToken cancellationToken = default)
    {
        return await _sessionRepository.GetByStripeSessionIdAsync(stripeSessionId, cancellationToken);
    }

    public async Task HandleWebhookEventAsync(string eventType, string stripeSessionId, CancellationToken cancellationToken = default)
    {
        _logger.LogWebhookReceived(eventType, stripeSessionId);

        var paymentSession = await _sessionRepository.GetByStripeSessionIdAsync(stripeSessionId, cancellationToken);
        if (paymentSession == null)
        {
            _logger.LogWarning("Payment session not found for Stripe session {StripeSessionId}", stripeSessionId);
            return;
        }

        var newStatus = eventType switch
        {
            "checkout.session.completed" => PaymentStatus.Paid,
            "checkout.session.async_payment_succeeded" => PaymentStatus.Paid,
            "checkout.session.async_payment_failed" => PaymentStatus.Failed,
            "checkout.session.expired" => PaymentStatus.Expired,
            _ => PaymentStatus.Pending
        };

        if (newStatus != PaymentStatus.Pending)
        {
            var oldStatus = paymentSession.Status.ToString();
            await _sessionRepository.UpdateStatusAsync(stripeSessionId, newStatus, cancellationToken);
            await _orderRepository.UpdatePaymentStatusAsync(paymentSession.OrderId, newStatus, cancellationToken);
            
            _logger.LogPaymentSessionStatusTransition(stripeSessionId, paymentSession.OrderId.ToString(), oldStatus, newStatus.ToString());
            
            // Log completion for terminal states
            if (newStatus is PaymentStatus.Paid or PaymentStatus.Failed or PaymentStatus.Expired)
            {
                _logger.LogPaymentSessionCompleted(stripeSessionId, paymentSession.OrderId.ToString(), newStatus.ToString());
            }
        }
    }
}

public record PaymentSessionResult(string StripeSessionId, string CheckoutUrl, string OrderId);
public record CheckoutItemDto(int CatalogItemId, string ProductName, int Quantity, decimal UnitPrice);
