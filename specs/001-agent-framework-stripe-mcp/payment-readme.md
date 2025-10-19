# Payment & Order Migration Notes

## Overview
This document captures implementation decisions and migration patterns for adding payment session lifecycle and order persistence to AspireShop.

## Entity Design Decisions

### PaymentSession Entity
- **Primary Key**: stripeSessionId (external identifier from Stripe)
- **Lifecycle**: pending → paid | failed | expired (no outbound transitions from terminal states)
- **TTL Strategy**: expiresUtc derived as createdUtc + 15 minutes
- **Idempotency**: Basket guard prevents duplicate session creation within 2-minute window
- **PCI Compliance**: NO cardholder data persisted; qualifies for SAQ A scope

### Order Entity
- **Status Mirroring**: paymentStatus field mirrors PaymentSession.status
- **Snapshot Pattern**: OrderItem collection captures catalog state at checkout time (immutable)
- **Currency**: ISO 4217 code (default USD); multi-currency out of scope

## Integration Points

### Stripe Hosted Checkout Flow
1. Frontend/Chat initiates checkout → PaymentSessionService
2. Service creates Order (pending) + PaymentSession (pending)
3. Stripe.Checkout.SessionService.Create(...) returns hosted URL
4. User redirected to Stripe-hosted form
5. Webhook receives payment success/failure → updates status
6. Expiration sweep (background job) marks abandoned sessions expired

### Throttling Strategy
- **Chat Throttle**: In-memory counter per user (≤30 req/min)
- **Session Guard**: Redis or in-memory lock preventing concurrent session creation for same basket
- **Upgrade Path**: Redis token bucket with sliding window documented in research.md

## Migration Sequence
1. Add entities to CatalogDb
2. Generate EF migration
3. Wire repositories + services
4. Implement endpoints (session create, status check, webhook)
5. Add expiration sweep background service
6. Wire throttling middleware

## Open Questions Resolved
- Webhook signature verification: Use Stripe SDK ConstructEvent
- Expiration mechanism: Timer-based background service running every 5 minutes
- Order-PaymentSession relationship: 1:1 via orderId FK

## Future Enhancements
- Saved payment methods (requires PCI DSS SAQ D scope)
- Subscription billing (out of scope)
- Refund/dispute handling (deferred)
- Metrics dashboard (upgrade from minimal observability)
