using AspireShop.CatalogDb;
using AspireShop.CatalogDb.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace AspireShop.ChatService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CheckoutController : ControllerBase
{
    private readonly PaymentSessionRepository _sessionRepository;
    private readonly OrderRepository _orderRepository;
    private readonly ILogger<CheckoutController> _logger;
    private readonly ActivitySource _activitySource;

    public CheckoutController(
        PaymentSessionRepository sessionRepository,
        OrderRepository orderRepository,
        ILogger<CheckoutController> logger)
    {
        _sessionRepository = sessionRepository;
        _orderRepository = orderRepository;
        _logger = logger;
        _activitySource = new ActivitySource("AspireShop.ChatService");
    }

    /// <summary>
    /// Creates a new checkout session
    /// POST /api/checkout/session
    /// </summary>
    [HttpPost("session")]
    public async Task<IActionResult> CreateSession([FromBody] CreateCheckoutSessionRequest request, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("CreateCheckoutSession");
        activity?.SetTag("customer.email", request.CustomerEmail);
        activity?.SetTag("items.count", request.Items.Count);

        try
        {
            // Validate request
            if (request.Items == null || request.Items.Count == 0)
                return BadRequest(new { error = "Order must contain at least one item" });

            if (string.IsNullOrWhiteSpace(request.CustomerEmail))
                return BadRequest(new { error = "Customer email is required" });

            // Check for existing pending session (idempotency)
            // TODO: Implement basket-based idempotency once basket integration is complete
            // For now, each request creates a new session

            // Calculate total
            var totalAmount = request.Items.Sum(item => item.Quantity * item.UnitPrice);
            activity?.SetTag("order.total", totalAmount);

            // Validate minimum amount
            if (totalAmount < 0.50m)
                return BadRequest(new { error = "Order total must be at least $0.50" });

            // Create order
            var order = new Order
            {
                OrderId = Guid.NewGuid(),
                CustomerEmail = request.CustomerEmail,
                TotalAmount = totalAmount,
                Currency = request.Currency ?? "USD",
                PaymentStatus = PaymentStatus.Pending,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                Items = request.Items.Select(i => new OrderItem
                {
                    OrderItemId = Guid.NewGuid(),
                    CatalogItemId = i.CatalogItemId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };

            await _orderRepository.CreateOrderAsync(order, cancellationToken);

            // Create Stripe session (mock for now)
            var stripeSessionId = $"cs_test_{Guid.NewGuid():N}";
            var checkoutUrl = $"https://checkout.stripe.com/c/pay/{stripeSessionId}";

            // Create payment session
            var paymentSession = new PaymentSession
            {
                StripeSessionId = stripeSessionId,
                OrderId = order.OrderId,
                CustomerEmail = request.CustomerEmail,
                Status = PaymentStatus.Pending,
                CreatedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(15)
            };

            await _sessionRepository.CreateSessionAsync(paymentSession, cancellationToken);

            _logger.LogInformation("Created checkout session {SessionId} for order {OrderId}",
                stripeSessionId, order.OrderId);

            activity?.SetTag("session.id", stripeSessionId);
            activity?.SetTag("order.id", order.OrderId);

            return Ok(new CheckoutSessionResponse
            {
                SessionId = stripeSessionId,
                CheckoutUrl = checkoutUrl,
                ExpiresAt = paymentSession.ExpiresUtc
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create checkout session");
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", ex.Message);
            return StatusCode(500, new { error = "Failed to create checkout session" });
        }
    }

    /// <summary>
    /// Gets the status of a checkout session
    /// GET /api/checkout/session/{sessionId}
    /// </summary>
    [HttpGet("session/{sessionId}")]
    public async Task<IActionResult> GetSessionStatus(string sessionId, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("GetSessionStatus");
        activity?.SetTag("session.id", sessionId);

        try
        {
            var session = await _sessionRepository.GetByStripeSessionIdAsync(sessionId, cancellationToken);
            
            if (session == null)
            {
                _logger.LogWarning("Session {SessionId} not found", sessionId);
                return NotFound(new { error = "Session not found" });
            }

            activity?.SetTag("session.status", session.Status.ToString());

            return Ok(new SessionStatusResponse
            {
                SessionId = session.StripeSessionId,
                Status = session.Status.ToString().ToLower(),
                CreatedAt = session.CreatedUtc,
                ExpiresAt = session.ExpiresUtc,
                CompletedAt = session.CompletedUtc
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session status for {SessionId}", sessionId);
            activity?.SetTag("error", true);
            return StatusCode(500, new { error = "Failed to retrieve session status" });
        }
    }
}

// DTOs
public class CreateCheckoutSessionRequest
{
    public string CustomerEmail { get; set; } = string.Empty;
    public string? BasketId { get; set; }
    public string? Currency { get; set; }
    public List<CheckoutItem> Items { get; set; } = new();
}

public class CheckoutItem
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class CheckoutSessionResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
}

public class SessionStatusResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
