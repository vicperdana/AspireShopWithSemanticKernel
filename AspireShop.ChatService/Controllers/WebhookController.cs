using AspireShop.ChatService.Services;
using AspireShop.ServiceDefaults.Stripe;
using AspireShop.ServiceDefaults.Logging;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;

namespace AspireShop.ChatService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IPaymentSessionService _paymentService;
    private readonly StripeSettings _stripeConfig;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IPaymentSessionService paymentService,
        StripeSettings stripeConfig,
        ILogger<WebhookController> logger)
    {
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        _stripeConfig = stripeConfig ?? throw new ArgumentNullException(nameof(stripeConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("stripe")]
    public async Task<IActionResult> HandleStripeWebhook(CancellationToken cancellationToken)
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Stripe webhook received without signature");
            return BadRequest(new { error = "Missing signature" });
        }

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                signature,
                _stripeConfig.WebhookSecret,
                throwOnApiVersionMismatch: false
            );

            // Handle supported events
            if (stripeEvent.Type.StartsWith("checkout.session."))
            {
                var session = stripeEvent.Data.Object as Session;
                if (session != null)
                {
                    await _paymentService.HandleWebhookEventAsync(
                        stripeEvent.Type,
                        session.Id,
                        cancellationToken);
                }
            }

            return Ok(new { received = true });
        }
        catch (StripeException ex)
        {
            _logger.LogWebhookSignatureInvalid();
            _logger.LogError(ex, "Stripe webhook signature validation failed");
            return BadRequest(new { error = "Invalid signature" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Stripe webhook");
            return StatusCode(500, new { error = "Webhook processing failed" });
        }
    }
}
