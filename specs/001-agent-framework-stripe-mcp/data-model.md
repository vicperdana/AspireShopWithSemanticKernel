# Data Model

## Overview
Defines new/modified entities required for Agent Framework migration + Stripe Hosted Checkout integration.

## Entities

### PaymentSession
| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| stripeSessionId | string | required, unique | Provided by Stripe when session created |
| orderId | guid | required | Links to Order (FK) |
| customerEmail | string | required, RFC 5322 format | Minimal PII stored |
| status | enum(pending, paid, failed, expired) | required | Lifecycle states |
| createdUtc | datetime | required | Session creation timestamp |
| completedUtc | datetime? | nullable | Set when paid or failed |
| expiresUtc | datetime? | nullable | Derived TTL (15 min) |

Validation Rules:
- `customerEmail` MUST match email format.
- `status` transitions allowed:
  - pending → paid | failed | expired
  - paid/failed/expired are terminal.
- Expiration rule: if not terminal within 15 minutes, mark expired.
- Idempotency: Reuse existing session if basket already has a pending session within window.

### Order
| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| orderId | guid | primary key | New or existing catalog order identifier |
| items | collection<OrderItem> | required | Derived from basket snapshot |
| totalAmount | decimal(18,2) | required, >=0 | Calculated at checkout initiation |
| currency | string(3) | required | ISO 4217 code (default e.g. USD) |
| customerEmail | string | required | Same as PaymentSession |
| paymentStatus | enum(pending, paid, failed, expired) | required | Mirrors PaymentSession status |
| createdUtc | datetime | required | Order creation |
| updatedUtc | datetime | required | Touch on status change |

### OrderItem
| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| orderItemId | guid | primary key | |
| orderId | guid | required (FK) | |
| catalogItemId | guid | required | Reference to catalog item |
| quantity | int | required, >0 | |
| unitPrice | decimal(18,2) | required, >=0 | Copied from catalog at time of order |

### Basket (Existing - extended behavior)
No schema change; add logic to prevent multiple simultaneous session creations (check PaymentSession existence).

### AIChatThread (Conceptual)
No persistence change; migrate chat agent usage from Semantic Kernel to Agent Framework `ChatClientAgent` (transient thread objects).

## Relationships
- PaymentSession (1) → Order (1) (same lifecycle status mirrored)
- Order (1) → OrderItem (N)
- Basket (1) → PaymentSession (0..1 active pending)

## State Machine (Payment/Order Status)
```
[pending] --(Stripe success webhook)--> [paid]
[pending] --(Stripe failure webhook)--> [failed]
[pending] --(TTL 15m elapsed)--> [expired]
```
Terminal states have no outbound transitions.

## Indexes
- PaymentSession: stripeSessionId (unique), status + expiresUtc (for expiry scans)
- Order: customerEmail, paymentStatus

## Derived Data & TTL
- expiresUtc = createdUtc + 00:15:00
- Expired sweep may run every 5 minutes or via webhook reconciliation fallback.

## Security & Privacy
- No cardholder data stored.
- Only minimal email retained for receipt linkage.

## Open Questions
None (all clarifications resolved).
