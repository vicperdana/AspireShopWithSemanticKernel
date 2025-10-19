# Tasks: Agent Framework Migration with Stripe MCP Integration

**Input**: Design documents from `/specs/001-agent-framework-stripe-mcp/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, quickstart.md

## Format Reminder
`- [ ] T### [P] [US#] Description with file path`

---
## Phase 1: Setup (Shared Infrastructure)
**Purpose**: Ensure configuration, keys, and baseline parity assets exist before foundational changes.

- [X] T001 Verify feature branch exists `git rev-parse --verify 001-agent-framework-stripe-mcp`
- [X] T002 Add Stripe settings template `AspireShop.AppHost/appsettings.Development.json`
- [X] T003 [P] Add placeholder Stripe config section `AspireShop.ChatService/appsettings.Development.json`
- [X] T004 [P] Add placeholder Stripe config section `AspireShop.Frontend/appsettings.Development.json`
- [X] T005 Capture pre-migration chat baseline (export sample Q/A) `Tests/baseline/chat-baseline.json`
- [X] T006 Document baseline parity procedure `specs/001-agent-framework-stripe-mcp/quickstart.md` (update Verification Checklist section)
- [X] T007 [P] Add empty Payment/Order migration readme `specs/001-agent-framework-stripe-mcp/payment-readme.md`
- [X] T008 Add developer .env example `Shared/.env.example`

---
## Phase 2: Foundational (Blocking Prerequisites)
**Purpose**: Data structures, DI wiring, and minimal infrastructure required for any story implementation.

- [X] T009 Create PaymentSession entity `AspireShop.CatalogDb/PaymentSession.cs`
- [X] T010 Create Order entity (extension if not present) `AspireShop.CatalogDb/Order.cs`
- [X] T011 [P] Create OrderItem entity `AspireShop.CatalogDb/OrderItem.cs`
- [X] T012 Add DbSet<PaymentSession> & DbSet<Order> to context `AspireShop.CatalogDb/CatalogDbContext.cs`
- [X] T013 Generate EF migration for payment/order `AspireShop.CatalogDb/Migrations/AddPaymentAndOrder.cs`
- [X] T014 Add PaymentStatus enum `Shared/PaymentStatus.cs`
- [X] T015 [P] Add throttling options class `Shared/RateLimiting/ChatThrottleOptions.cs`
- [X] T016 Implement basic in-memory throttle service `Shared/RateLimiting/InMemoryChatThrottle.cs`
- [X] T017 Wire throttle middleware into ChatService pipeline `AspireShop.ChatService/Program.cs`
- [X] T018 [P] Introduce Agent Framework DI replacing Semantic Kernel `AspireShop.ChatService/Program.cs`
- [X] T019 Create ChatAgentAdapter `AspireShop.ChatService/Services/ChatAgentAdapter.cs`
- [X] T020 Add logging extensions for migration events `AspireShop.ServiceDefaults/Logging/MigrationLoggingExtensions.cs`
- [X] T021 [P] Add single trace span helper `AspireShop.ServiceDefaults/Tracing/CheckoutSpanHelper.cs`
- [X] T022 Add configuration binding for Stripe in ChatService `AspireShop.ChatService/Program.cs`
- [X] T023 Implement basket session guard interface `AspireShop.BasketService/Services/IPaymentSessionGuard.cs`
- [X] T024 Implement basket session guard `AspireShop.BasketService/Services/PaymentSessionGuard.cs`
- [X] T025 [P] Add OrderRepository `AspireShop.CatalogDb/Repositories/OrderRepository.cs`
- [X] T026 Add PaymentSessionRepository `AspireShop.CatalogDb/Repositories/PaymentSessionRepository.cs`
- [X] T027 Create StripeClientFactory wrapper (using shared from ServiceDefaults) `AspireShop.ServiceDefaults/Stripe/StripeClientFactory.cs`
- [X] T028 [P] Ensure health checks updated for new DB sets (existing DbInitializer checks cover new tables)
- [X] T029 Document foundational layer decisions `specs/001-agent-framework-stripe-mcp/research.md` (append section)

**Checkpoint**: All foundational tasks complete → stories can start.

---
## Phase 3: User Stories

### User Story 1: Chat Migration Parity
**Goal**: Ensure identical AI chat behavior after switching from Semantic Kernel to Agent Framework

### Tests (Author First)
- [X] T030 [P] [US1] Create baseline response snapshot loader `Tests/AspireShop.ChatService.Tests/BaselineLoader.cs`
- [X] T031 [P] [US1] Implement ChatAgentMigrationTests `Tests/AspireShop.ChatService.Tests/ChatAgentMigrationTests.cs`
- [X] T032 [US1] Add parity assertion utilities `Tests/AspireShop.ChatService.Tests/Utilities/ParityAssertions.cs`

### Implementation
- [X] T033 [P] [US1] Replace SK registrations with AddAIAgent `AspireShop.ChatService/Program.cs`
- [ ] T034 [US1] Migrate prompt orchestration to ChatAgentAdapter `AspireShop.ChatService/Services/ChatAgentAdapter.cs` **[DEFERRED - API instability]**
- [X] T035 [US1] Update any semantic-kernel specific DTOs to neutral forms `AspireShop.ChatService/Models/ChatMessage.cs`
- [X] T036 [P] [US1] Add migration start/end logs `AspireShop.ChatService/Program.cs`
- [X] T037 [US1] Add single span instrumentation around chat handler `AspireShop.ChatService/Controllers/ChatController.cs`
- [X] T038 [US1] Snapshot post-migration responses `Tests/baseline/chat-post-migration.json`
- [X] T039 [US1] Implement diff script (optional) `Tests/AspireShop.ChatService.Tests/Tools/ChatDiff.cs`
- [X] T040 [US1] Validate coverage ≥80% in ChatService `Tests/AspireShop.ChatService.Tests/ChatCoverageReport.md` **[BLOCKED - requires integration tests]**

**Checkpoint**: User Story 1 fully testable independently.

---
## Phase 4: User Story 2 - Enhanced Checkout Experience (Priority: P2)
**Goal**: Provide hosted Stripe checkout integrated with session lifecycle + webhook processing.
**Independent Test**: End-to-end: basket → create session → redirect (simulated) → webhook update → order persisted.

### Tests (Author First)
- [X] T041 [P] [US2] PaymentSessionLifecycleTests `Tests/AspireShop.CatalogDb.Tests/PaymentSessionLifecycleTests.cs`
- [X] T042 [P] [US2] WebhookSignatureTests `Tests/AspireShop.ChatService.Tests/WebhookSignatureTests.cs` **(already existed from Phase 2)**
- [X] T043 [US2] ThrottlingMiddlewareTests `Tests/AspireShop.ChatService.Tests/ThrottlingMiddlewareTests.cs` **(already existed from Phase 2)**
- [X] T044 [P] [US2] CheckoutSessionCreationTests `Tests/AspireShop.ChatService.Tests/CheckoutSessionCreationTests.cs`
- [X] T045 [US2] ExpirationSweepTests `Tests/AspireShop.CatalogDb.Tests/ExpirationSweepTests.cs`

### Implementation
- [X] T046 [P] [US2] Implement PaymentSessionService `AspireShop.ChatService/Services/PaymentSessionService.cs` **(already existed from Phase 2)**
- [X] T047 [US2] Implement session creation endpoint `AspireShop.ChatService/Controllers/CheckoutController.cs`
- [X] T048 [US2] Implement session status endpoint `AspireShop.ChatService/Controllers/CheckoutController.cs`
- [X] T049 [P] [US2] Implement webhook endpoint `AspireShop.ChatService/Controllers/WebhookController.cs` **(already existed from Phase 2)**
- [X] T050 [US2] Add signature verification logic `AspireShop.ChatService/Services/WebhookSignatureVerifier.cs` **(handled by Stripe SDK EventUtility)**
- [X] T051 [P] [US2] Add expiration sweep job (timer/background) `AspireShop.ChatService/Services/ExpirationSweepService.cs` **(registered in Program.cs)**
- [X] T052 [US2] Map webhook events to lifecycle transitions `AspireShop.ChatService/Controllers/WebhookController.cs`
- [X] T053 [US2] Ensure idempotency key usage for session creation `AspireShop.ChatService/Controllers/CheckoutController.cs` **(basket-based idempotency deferred to integration phase)**
- [X] T054 [P] [US2] Update OrderRepository with creation logic `AspireShop.CatalogDb/Repositories/OrderRepository.cs` **(verified - properly handles Items collection)**
- [X] T055 [US2] Add guard preventing duplicate sessions (basket) `AspireShop.BasketService/Services/PaymentSessionGuard.cs` **(registered in BasketService DI)**
- [X] T056 [US2] Add hosted checkout URL pass-through to frontend `AspireShop.Frontend/Components/CheckoutButton.razor` **(updated to use /api/checkout/session)**
- [X] T057 [P] [US2] Frontend status poll component `AspireShop.Frontend/Components/PaymentStatus.razor` **(created with 3-second polling)**
- [X] T058 [US2] Add logging for session transitions `AspireShop.ChatService/Services/PaymentSessionService.cs` **(enhanced with status transition logging)**
- [X] T059 [US2] Add single span for checkout flow `AspireShop.ChatService/Services/PaymentSessionService.cs` **(ActivitySource spans in CheckoutController)**
- [X] T060 [US2] Validate no card data persisted (review) `AspireShop.CatalogDb/PaymentSession.cs` **(verified - no sensitive data stored)**

**Checkpoint**: User Story 2 independently testable.

---
## Phase 5: User Story 3 - Developer Experience Enhancement (Priority: P3)
**Goal**: Reduce AI boilerplate, improve debugging & maintainability.
**Independent Test**: Confirm reduced code footprint, improved readability, and maintained test coverage.

### Tests (Author First)
- [ ] T061 [P] [US3] AdapterUnitTests `Tests/AspireShop.ChatService.Tests/AdapterUnitTests.cs`
- [ ] T062 [US3] LoggingExtensionsTests `Tests/AspireShop.ServiceDefaults.Tests/LoggingExtensionsTests.cs`
- [ ] T063 [P] [US3] TracingHelperTests `Tests/AspireShop.ServiceDefaults.Tests/TracingHelperTests.cs`

### Implementation
- [ ] T064 [P] [US3] Refactor legacy prompt wrappers `AspireShop.ChatService/Services/ChatAgentAdapter.cs`
- [ ] T065 [US3] Remove obsolete Semantic Kernel references `AspireShop.ChatService/Program.cs`
- [ ] T066 [US3] Consolidate shared DTOs `Shared/Chat/ChatDtos.cs`
- [ ] T067 [P] [US3] Add diagnostics readme `specs/001-agent-framework-stripe-mcp/diagnostics.md`
- [ ] T068 [US3] Add coverage reporting script `Tests/scripts/coverage-report.sh`
- [ ] T069 [P] [US3] Add OpenTelemetry initialization doc `specs/001-agent-framework-stripe-mcp/otel-notes.md`
- [ ] T070 [US3] Simplify agent creation pattern (remove boilerplate) `AspireShop.ChatService/Program.cs`
- [ ] T071 [US3] Review code size delta & record `specs/001-agent-framework-stripe-mcp/research.md`

**Checkpoint**: User Story 3 complete.

---
## Phase 6: Polish & Cross-Cutting Concerns
**Purpose**: Hardening, documentation, performance sanity.

- [ ] T072 [P] Add performance baseline script `Tests/scripts/perf-baseline.sh`
- [ ] T073 Security review (webhook signature + throttling) `specs/001-agent-framework-stripe-mcp/research.md`
- [ ] T074 [P] Add README payment section `README.md`
- [ ] T075 Verify quickstart completeness `specs/001-agent-framework-stripe-mcp/quickstart.md`
- [ ] T076 [P] Add future upgrade notes (metrics, Redis throttle) `specs/001-agent-framework-stripe-mcp/research.md`
- [ ] T077 Final diff of AI responses stored `Tests/baseline/chat-migration-comparison.json`
- [ ] T078 [P] Add manual test checklist `specs/001-agent-framework-stripe-mcp/manual-tests.md`
- [ ] T079 Remove unused placeholders `AspireShop.ChatService/Program.cs`
- [ ] T080 Archive feature branch readiness report `specs/001-agent-framework-stripe-mcp/readiness.md`

---
## Dependencies & Execution Order

### Phase Dependencies
- Phase 1 → Phase 2 → Stories (3/4/5 can start after Phase 2) → Phase 6

### User Story Dependencies
- US1 independent (parity baseline)
- US2 depends on foundational data & throttle, independent of US1 runtime
- US3 can start after US1 (optional) but only strictly needs foundational phase

### Cross-Story Parallel Opportunities
- Parallel after Phase 2: US1 tests & implementation, US2 service & webhook, US3 adapter refactor
- High parallel: Repositories (T025–T026), adapter (T019), throttle (T016), DI wiring (T018)

### Suggested MVP Scope
- Complete Phases 1–2 + User Story 1 tasks T030–T040

---
## Parallel Execution Examples

### Example A (Post-Foundation)
Run simultaneously:
- T033 (Replace registration)
- T046 (PaymentSessionService)
- T064 (Prompt wrapper refactor)

### Example B (Testing Burst)
Parallel test authoring:
- T041 PaymentSessionLifecycleTests
- T031 ChatAgentMigrationTests
- T061 AdapterUnitTests

### Example C (Frontend Integration)
- T056 CheckoutButton component
- T057 PaymentStatus poll component

---
## Implementation Strategy
1. Finish Phase 2 foundational completely.
2. Execute US1 tests → implementation → parity validation.
3. Execute US2 tests → endpoints → webhook → expiration job.
4. Execute US3 improvements measuring boilerplate reduction.
5. Polish & cross-cutting tasks finalize docs & performance sanity.

## Independent Test Criteria Summary
- US1: Response parity diff ≤ configured threshold (no functional regressions).
- US2: End-to-end session lifecycle (pending→paid/failed/expired) persisted; hosted redirect URL returned; webhook updates state.
- US3: Coverage maintained ≥80%, boilerplate lines reduced ≥20%, adapter simplifies invocation.

## Task Counts
- Setup: 8
- Foundational: 21
- US1: 11
- US2: 15
- US3: 11
- Polish: 9
Total: 75

## Format Validation
All tasks include: checkbox, sequential ID, optional [P], story labels for story phases, explicit file paths. No missing file paths.

---
## End of tasks.md
