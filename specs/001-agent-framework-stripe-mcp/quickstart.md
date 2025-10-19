# Quickstart: Agent Framework Migration + Stripe Hosted Checkout

## Prerequisites
- .NET 9 SDK installed.
- Stripe API keys (secret + publishable) available as environment variables.
- Existing AspireShop solution cloned; feature branch `001-agent-framework-stripe-mcp` checked out.
- Basket & Catalog services running locally via Aspire.

## 1. Configure App Settings
Add Stripe keys to development settings (example):
```
{
  "Stripe": {
    "SecretKey": "sk_test_xxx",
    "PublishableKey": "pk_test_xxx",
    "WebhookSecret": "whsec_xxx",
    "CheckoutSuccessUrl": "https://localhost:5001/checkout/success",
    "CheckoutCancelUrl": "https://localhost:5001/checkout/cancel"
  }
}
```

## 2. Migrate AI to Agent Framework
1. Replace Semantic Kernel registrations with `services.AddAIAgent<ChatClientAgent>(...)` in `AspireShop.ChatService/Program.cs`.
2. Introduce adapter if any legacy prompt wrappers require transformation (create `ChatAgentAdapter.cs`).
3. Update usages: replace previous kernel invocation with `agent.GetNewThread().InvokeAsync(...)` patterns.
4. Retain existing public endpoints—no contract changes.

## 3. Implement Payment Session Creation
1. Add `PaymentSessionService` to chosen service (ChatService or new minimalPayments component) handling idempotent session creation.
2. Use Stripe SDK: create Checkout Session with line items from Basket snapshot.
3. Persist `stripeSessionId`, `orderId`, `customerEmail`, `status=pending`, timestamps.
4. Return hosted session URL to caller.

## 4. Webhook Handling
1. Add `/api/checkout/webhook` endpoint verifying signature with `WebhookSecret`.
2. On success event: update PaymentSession + Order to `paid`.
3. On failure event: update to `failed`.
4. Periodic sweep (or scheduler) marks overdue pending sessions `expired` (TTL 15 min).

## 5. Basic Throttling
1. Middleware counting per-user chat requests (IP/user id) in-memory; reject >30/min with 429.
2. Guard: Do not create a second pending PaymentSession for same basket within 2 minutes.

## 6. Logging & Tracing (MVP)
- Log: migration start/end, session create, status transitions.
- Trace: wrap checkout flow in single span tagging `sessionId`, final status.

## 7. Tests (Author Before Code)
| Test | Purpose |
|------|---------|
| ChatAgentMigrationTests | Verifies parity of responses vs snapshot baseline |
| PaymentSessionLifecycleTests | Validates status transitions + expiration rule |
| WebhookSignatureTests | Ensures invalid signature yields 400 |
| ThrottlingMiddlewareTests | Ensures >30 req/min returns 429 |

## 8. Running Locally
```
dotnet restore
./build.sh
# Launch Aspire host (or through IDE)
```
Visit frontend, initiate chat-guided checkout.

## 9. Upgrade Paths (Future)
- Replace in-memory throttle with Redis token bucket.
- Add metrics and extended traces (latency histograms, span links).
- Introduce discount codes & subscriptions after out-of-scope reevaluation.

## 10. Verification Checklist
- [ ] AI responses unchanged (sample baseline script)
- [ ] Hosted checkout redirects and returns success URL
- [ ] Webhook updates payment status
- [ ] Expired sessions visible after TTL
- [ ] Throttling blocks excess chat requests

## Reference Artifacts
- `data-model.md`
- `contracts/openapi.yaml`
- `research.md`
- `spec.md`
