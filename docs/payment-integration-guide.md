# AspireShop Payment Integration - Developer Guide

## Quick Start

### Prerequisites
- .NET 9 SDK
- PostgreSQL (local or container)
- Redis (local or container)
- Stripe account (test mode)

### Configuration

#### 1. Stripe Setup
1. Sign up at [stripe.com](https://stripe.com) (test mode)
2. Get API keys from Dashboard → Developers → API keys
3. Create webhook endpoint: Dashboard → Developers → Webhooks
   - Endpoint URL: `https://yourdomain.com/api/webhook/stripe` (use ngrok for local dev)
   - Events: `checkout.session.completed`, `checkout.session.async_payment_succeeded`, `checkout.session.async_payment_failed`, `checkout.session.expired`
4. Copy webhook signing secret

#### 2. Environment Variables
Create `/Shared/.env` from `.env.example`:

```bash
# Azure OpenAI (or OpenAI)
AZURE_OPENAI_ENDPOINT=https://your-openai-instance.openai.azure.com
AZURE_OPENAI_API_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4

# Stripe
STRIPE_SECRET_KEY=sk_test_...
STRIPE_PUBLISHABLE_KEY=pk_test_...
STRIPE_WEBHOOK_SECRET=whsec_...
STRIPE_SUCCESS_URL=https://localhost:5001/checkout/success
STRIPE_CANCEL_URL=https://localhost:5001/checkout/cancel

# Rate Limiting
CHAT_THROTTLE_MAX_REQUESTS=30
CHAT_THROTTLE_WINDOW_MINUTES=1
BASKET_SESSION_GUARD_WINDOW_MINUTES=2
```

#### 3. Database Setup
```bash
cd AspireShop.CatalogDbManager
dotnet ef database update --context CatalogDbContext
```

### Running Locally

#### Option A: Aspire Dashboard (Recommended)
```bash
cd AspireShop.AppHost
dotnet run
```
Navigate to http://localhost:15888 for Aspire dashboard.

#### Option B: Individual Services
```bash
# Terminal 1 - CatalogService
cd AspireShop.CatalogService
dotnet run

# Terminal 2 - ChatService
cd AspireShop.ChatService
dotnet run

# Terminal 3 - Frontend
cd AspireShop.Frontend
dotnet run
```

### Testing Payment Flow

#### 1. Test Checkout
```bash
curl -X POST http://localhost:5002/api/payment/checkout \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "test-user",
    "customerEmail": "test@example.com",
    "items": [
      {
        "catalogItemId": "1",
        "productName": "Test Product",
        "quantity": 1,
        "unitPrice": 29.99
      }
    ]
  }'
```

Response:
```json
{
  "sessionId": "cs_test_...",
  "checkoutUrl": "https://checkout.stripe.com/c/pay/...",
  "orderId": "order_..."
}
```

#### 2. Test Webhook (Local)
Use Stripe CLI for local webhook testing:
```bash
stripe listen --forward-to localhost:5002/api/webhook/stripe
stripe trigger checkout.session.completed
```

#### 3. Test Payment Cards (Stripe Test Mode)
- Success: `4242 4242 4242 4242`
- Decline: `4000 0000 0000 0002`
- Requires authentication: `4000 0025 0000 3155`

### Troubleshooting

#### Webhook Signature Validation Fails
- Ensure `STRIPE_WEBHOOK_SECRET` matches Dashboard webhook secret
- Use Stripe CLI for local testing: `stripe listen --forward-to ...`
- Check request headers: `Stripe-Signature` present

#### Payment Session Not Found
- Check database for `PaymentSession` record
- Verify `OrderId` in Stripe session metadata
- Check logs for session creation errors

#### Throttle Rate Limiting
- Chat throttle: Max 30 requests/min per user
- Basket guard: 2-minute window between checkout attempts
- Reset: Wait for window to expire or restart service (dev only)

## Architecture

### Payment Lifecycle
```
User → Frontend → PaymentController → PaymentSessionService
                        ↓
                  Create Order (DB)
                        ↓
                  Create Stripe Session
                        ↓
                  Create PaymentSession (DB)
                        ↓
                  Return checkout URL
                        ↓
User completes payment on Stripe → Webhook → WebhookController
                                                ↓
                                    Update PaymentSession status
                                                ↓
                                    Update Order status
```

### Data Flow
1. **Checkout Initiation**: Frontend → PaymentController → PaymentSessionService
2. **Session Creation**: PaymentSessionService → PostgreSQL (Order + PaymentSession) + Stripe API
3. **Payment Completion**: Stripe → WebhookController → Repositories → PostgreSQL
4. **Expiration Sweep**: Background service → PostgreSQL (every 5 min)

### Components
- **PaymentSessionService**: Core payment logic
- **PaymentController**: REST API for checkout
- **WebhookController**: Stripe webhook handler
- **PaymentSessionExpirationService**: Background expiration job
- **Repositories**: Data access (Order, PaymentSession)
- **Stripe Client Factory**: Stripe SDK initialization

## Testing

### Unit Tests
```bash
cd Tests
dotnet test --filter "FullyQualifiedName~PaymentLifecycleTests"
dotnet test --filter "FullyQualifiedName~WebhookSignatureTests"
dotnet test --filter "FullyQualifiedName~ThrottlingTests"
```

### Integration Tests
```bash
dotnet test --filter "FullyQualifiedName~ChatAgentMigrationTests"
```

### Coverage
Target: ≥80% for payment/webhook services
```bash
dotnet test /p:CollectCoverage=true /p:CoverageReportsDirectory=./coverage
```

## Deployment

### Azure App Service
1. Configure App Settings with production Stripe keys
2. Set webhook URL to production domain
3. Enable HTTPS enforcement
4. Configure Application Insights for observability

### Docker
```bash
docker build -t aspireshop-chatservice -f AspireShop.ChatService/Dockerfile .
docker run -p 5002:8080 \
  -e STRIPE_SECRET_KEY=sk_live_... \
  -e STRIPE_WEBHOOK_SECRET=whsec_... \
  aspireshop-chatservice
```

## Monitoring

### Key Metrics
- Payment session creation rate
- Webhook processing latency
- Session expiration count
- Throttle rejection rate

### Logs
- Structured logs: JSON format with correlation IDs
- Key events: Session created, webhook received, signature invalid, session expired
- Errors: Exceptions with stack traces + user ID context

### Tracing
- Checkout span: Order ID, amount, session ID tags
- Webhook span: Event type, session ID tags
- Distributed tracing via OpenTelemetry

## FAQ

**Q: How long do payment sessions last?**  
A: 15 minutes from creation. Expired sessions are marked by background service.

**Q: What happens if webhook fails?**  
A: Stripe retries with exponential backoff. Check Dashboard → Webhooks for failed events.

**Q: Can users checkout multiple times quickly?**  
A: No, basket session guard enforces 2-minute window between checkout attempts.

**Q: Where is payment data stored?**  
A: Only session ID + order ID + email in PostgreSQL. No card data (PCI SAQ A scope).

**Q: How to test expiration?**  
A: Set `ExpiresUtc` to past date in DB, wait for background service sweep (5 min), or manually trigger.

## Support
- Docs: [/specs/001-agent-framework-stripe-mcp/](../specs/001-agent-framework-stripe-mcp/)
- Issues: GitHub Issues
- Stripe Docs: [stripe.com/docs/payments/checkout](https://stripe.com/docs/payments/checkout)
