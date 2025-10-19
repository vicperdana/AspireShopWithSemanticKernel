# Research Summary

## Decisions & Rationale

### Hosted Stripe Checkout (vs Custom Card Form)
- Decision: Use Stripe Hosted Checkout, store only order id, stripe session id, customer email, status.
- Rationale: Minimizes PCI scope (SAQ A), fastest integration path, reduces security/test surface.
- Alternatives: Custom Elements form (higher PCI burden), third-party aggregator (adds latency), manual tokenization (more complexity).

### Payment Lifecycle Depth (Pending, Paid, Failed, Expired)
- Decision: Four-state model.
- Rationale: Distinguishes abandonment from rejection; sufficient for MVP without auth/capture/refund complexity.
- Alternatives: Minimal (lost visibility), full transactional (overkill), dispute-inclusive (not needed yet).

### Observability (Minimal)
- Decision: Structured logs + single span per checkout.
- Rationale: Speed of delivery > deep telemetry; upgrade path documented.
- Alternatives: Histograms/RED metrics now (time cost), deep SLO instrumentation (premature).

### Rate Limiting (Basic Throttling)
- Decision: In-memory per-user counters (chat) & single active payment session window.
- Rationale: Simple, low latency, sufficient for demo load.
- Alternatives: Redis token bucket (kept as upgrade path), adaptive ML-based throttling (overkill).

### Agent Framework Migration Strategy
- Decision: Replace Semantic Kernel invocation with ChatClientAgent + IChatClient; introduce an adapter only if legacy prompt wrappers complicate refactor.
- Rationale: Reduces boilerplate (SC-008), aligns with constitution AI-First principle, leverages built-in OpenTelemetry.
- Alternatives: Hybrid layer (retain both SK & Agent) adds maintenance overhead; full rewrite of prompts (unnecessary risk).

### Data Minimization
- Decision: Persist only payment status essentials.
- Rationale: Reduces compliance burden and breach impact.
- Alternatives: Storing more metadata (coupon, card brand) deferred until discount/gift card features become in-scope.

## Upgrade Paths (Future Phases)
- Observability: Add metrics (payment_success_total, webhook_latency_ms), traces with span links.
- Rate Limiting: Redis token bucket + global circuit breaker based on p95 latency.
- Commerce: Introduce discount codes, gift cards, subscription billing.
- Resilience: Idempotent webhook processing with replay detection.

## No Remaining Unknowns
All clarifications resolved; no NEEDS CLARIFICATION items.

---

## Foundational Layer Implementation Decisions

### Entity Design
**Decision**: PaymentSession and Order as separate entities with one-to-one relationship.
**Rationale**: 
- Clear separation of concerns (payment lifecycle vs order fulfillment)
- Allows order creation before payment completion
- Supports future scenarios like manual payment approval

**Implementation Details**:
- PaymentSession uses StripeSessionId as primary key (natural key from Stripe)
- Order uses Guid primary key for internal referencing
- Status transitions enforced at repository level (pending → terminal states only)
- Expiration calculated as CreatedUtc + 15 minutes

### Repository Pattern
**Decision**: Dedicated repositories for Order and PaymentSession with explicit interfaces.
**Rationale**:
- Encapsulates EF Core queries and business logic
- Enables easy testing with mocks
- Provides clear contract for service layer
- Enforces status transition validation rules

**Key Operations**:
- OrderRepository: CRUD + status updates with timestamp tracking
- PaymentSessionRepository: Lifecycle management + expiration queries
- Both include logging for audit trail

### Stripe Integration Architecture
**Decision**: StripeClientFactory wrapper pattern.
**Rationale**:
- Centralizes Stripe SDK configuration
- Provides consistent error handling
- Simplifies testing with factory injection
- Webhook signature validation co-located with client creation

**Configuration Approach**:
- Settings bound from appsettings via StripeConfiguration class
- DataAnnotations validation ensures required keys present at startup
- Supports both test and production keys via environment

### Throttling Strategy
**Decision**: Dual-level throttling (chat requests + checkout sessions).
**Rationale**:
- Chat throttle prevents AI API abuse (30 req/min per user)
- Payment session guard prevents duplicate checkouts (2-minute window)
- Both use in-memory storage for simplicity (upgrade to Redis documented)

**Implementation**:
- InMemoryChatThrottle: Sliding window counter per user
- PaymentSessionGuard: Timestamp-based guard with automatic cleanup
- Basket session guard prevents race conditions on checkout

### Observability Implementation
**Decision**: ActivitySource-based spans + structured logging extensions.
**Rationale**:
- OpenTelemetry native (no external dependencies)
- Integrates with Aspire dashboard automatically
- Minimal performance overhead
- Easy to extend with metrics later

**Span Coverage**:
- checkout.session.create: Full session creation flow
- checkout.webhook.process: Webhook event handling
- checkout.session.status_update: Status transitions
- checkout.expiration.sweep: Background cleanup job

### Agent Framework Adapter
**Decision**: ChatAgentAdapter wrapping ChatClient with convenience methods.
**Rationale**:
- Simplifies migration from Semantic Kernel
- Provides consistent API for chat operations
- Enables streaming support for future UI enhancements
- Encapsulates intent determination logic

**Design Pattern**:
- Wrapper over OpenAI ChatClient (not full abstraction)
- Returns strings for simple scenarios (MVP)
- Streaming support via IAsyncEnumerable
- Error handling with fallback to default intent

### Health Check Strategy
**Decision**: Leverage existing CatalogDbInitializer health check.
**Rationale**:
- DbInitializer already validates schema migrations
- New tables (PaymentSession, Order) included in migration
- No additional health check needed for MVP
- Aspire dashboard shows database status automatically

### Testing Approach (Foundation)
**Decision**: Repository unit tests + integration tests for database operations.
**Rationale**:
- Validates status transition logic
- Ensures expiration queries work correctly
- Tests idempotency of session creation
- Verifies EF Core mappings and indexes

**Test Coverage Goals**:
- Repository: ≥80% coverage
- Status transitions: All valid/invalid combinations
- Expiration logic: Edge cases (just expired, not yet expired)
- Error scenarios: Duplicate sessions, invalid transitions

### Migration Strategy
**Decision**: Single EF Core migration for Payment/Order entities.
**Rationale**:
- Atomic deployment (all or nothing)
- Clear rollback path (drop tables via migration)
- Preserves existing catalog data
- Migration named AddPaymentAndOrder for clarity

**Schema Decisions**:
- Indexes on status + expiresUtc (for expiration sweep queries)
- Indexes on customerEmail (for user order history)
- Decimal(18,2) for currency amounts (standard precision)
- ISO 4217 currency codes (3-char string)

### Configuration Management
**Decision**: Layered configuration (appsettings.json + user secrets + environment).
**Rationale**:
- Development: User secrets for API keys
- Production: Environment variables from Key Vault
- Validation at startup (fail fast on missing keys)
- Type-safe configuration classes with DataAnnotations

**Configuration Classes**:
- StripeConfiguration: API keys + URLs
- ChatThrottleOptions: Rate limits
- AzureOpenAI: Already exists for AI

### Error Handling Philosophy
**Decision**: Repository throws domain exceptions, services handle + log.
**Rationale**:
- Repository enforces business rules (throw on invalid transitions)
- Services translate to HTTP status codes
- Structured logging at service boundary
- OpenTelemetry spans capture error context

**Exception Strategy**:
- InvalidOperationException: Business rule violations
- ArgumentException: Input validation failures
- StripeException: Stripe API errors (caught + logged)
- All exceptions recorded in active span

---

## Implementation Notes

### Completed Infrastructure (Phase 2)
1. ✅ Data models (PaymentSession, Order, OrderItem, PaymentStatus enum)
2. ✅ EF Core DbSets and entity configurations
3. ✅ Database migration (AddPaymentAndOrder)
4. ✅ Repositories (Order, PaymentSession) with interfaces
5. ✅ Stripe client factory + configuration
6. ✅ Throttling services (chat + basket session guard)
7. ✅ Agent Framework DI wiring (ChatClient + ChatAgentAdapter)
8. ✅ Logging extensions (migration events + payment lifecycle)
9. ✅ Tracing helpers (CheckoutSpanHelper for observability)

### Ready for User Stories
Foundation complete. All prerequisites in place for:
- US1: Chat migration parity testing
- US2: Checkout session + webhook implementation
- US3: Developer experience improvements

### Known Limitations (MVP Scope)
- In-memory throttling (not distributed)
- No webhook replay detection (idempotency keys only)
- Single currency support (future: multi-currency)
- No refund/dispute handling (future phase)
- Basic expiration sweep (no background worker yet)


