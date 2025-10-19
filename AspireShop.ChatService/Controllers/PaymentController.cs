using AspireShop.ChatService.Services;
using AspireShop.ServiceDefaults.RateLimiting;
using AspireShop.ServiceDefaults.Logging;
using Microsoft.AspNetCore.Mvc;

namespace AspireShop.ChatService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentSessionService _paymentService;
    private readonly IBasketSessionGuard _sessionGuard;
    private readonly IChatThrottle _chatThrottle;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentSessionService paymentService,
        IBasketSessionGuard sessionGuard,
        IChatThrottle chatThrottle,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        _sessionGuard = sessionGuard ?? throw new ArgumentNullException(nameof(sessionGuard));
        _chatThrottle = chatThrottle ?? throw new ArgumentNullException(nameof(chatThrottle));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckoutSession(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new { error = "UserId is required" });
        }

        if (string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            return BadRequest(new { error = "CustomerEmail is required" });
        }

        if (request.Items == null || !request.Items.Any())
        {
            return BadRequest(new { error = "Items list cannot be empty" });
        }

        // Check basket session guard
        if (!await _sessionGuard.CanCreateSessionAsync(request.UserId, cancellationToken))
        {
            _logger.LogBasketSessionGuardTriggered(request.UserId, DateTime.UtcNow);
            return StatusCode(429, new { error = "Please wait before creating another checkout session" });
        }

        try
        {
            // Create payment session
            var result = await _paymentService.CreatePaymentSessionAsync(
                request.UserId,
                request.CustomerEmail,
                request.Items,
                cancellationToken);

            // Record checkout attempt
            await _sessionGuard.RecordCheckoutAttemptAsync(request.UserId, cancellationToken);

            return Ok(new
            {
                sessionId = result.StripeSessionId,
                checkoutUrl = result.CheckoutUrl,
                orderId = result.OrderId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create checkout session for user {UserId}", request.UserId);
            return StatusCode(500, new { error = "Failed to create checkout session" });
        }
    }

    [HttpGet("session/{sessionId}")]
    public async Task<IActionResult> GetSession(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { error = "SessionId is required" });
        }

        var session = await _paymentService.GetSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            return NotFound(new { error = "Session not found" });
        }

        return Ok(new
        {
            sessionId = session.StripeSessionId,
            orderId = session.OrderId,
            status = session.Status.ToString(),
            createdUtc = session.CreatedUtc,
            expiresUtc = session.ExpiresUtc
        });
    }
}

public record CheckoutRequest(string UserId, string CustomerEmail, List<CheckoutItemDto> Items);
