# Implementation Plan: Agent Framework Migration with Stripe MCP Integration

**Branch**: `001-agent-framework-stripe-mcp` | **Date**: 2025-10-18 | **Spec**: `spec.md`
**Input**: Feature specification from `/specs/001-agent-framework-stripe-mcp/spec.md`

## Summary

Migrate existing Semantic Kernel usage to Microsoft Agent Framework ensuring 100% feature parity while adding a hosted Stripe Checkout integration via MCP extension. Payment lifecycle states (pending, paid, failed, expired) tracked with minimal in-memory throttling and strict PCI scope minimization (no card data persisted). Observability kept minimal (structured logs + single span) for MVP with clear upgrade paths documented.

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# .NET 9.0 + Aspire distributed application model
**Primary Dependencies**: Microsoft.Agents.AI (core), Microsoft.Agents.AI.OpenAI (providers), Microsoft.Agents.AI.Hosting (DI), Stripe .NET SDK, Redis client, EF Core (Catalog/Order persistence)
**AI Migration Approach**: Replace Semantic Kernel abstractions with ChatClientAgent + IChatClient; create adapter layer if necessary for legacy prompt wrappers; maintain existing public service contracts.
**MCP Extension**: New Stripe MCP tool capability for creating checkout sessions and querying status; webhook endpoint for session completion/failure.
**Storage**: PostgreSQL (catalog + orders); Redis (basket, simple throttling counters); NO payment method storage.
**Testing**: xUnit (unit/service), integration tests for payment webhook + AI agent instantiation, minimal end-to-end chat + checkout scenario test; Moq for external AI + Stripe clients; coverlet enforcing ≥80% coverage.
**Deployment Target**: Local Aspire for dev; Azure Container Apps (future) assumed; feature designed cloud-neutral.
**Performance Goals**: AI response p95 ≤2s (parity with baseline); checkout initiation <500ms (excluding Stripe redirect); webhook processing <300ms.
**Security Constraints**: PCI DSS SAQ A scope only; environment secrets via user secrets (dev) and Key Vault (future); idempotency keys for session creation; input validation on email/order identifiers.
**Observability (MVP)**: Structured logs for migration steps + payment status transitions; one span per checkout; correlation IDs preserved via Aspire diagnostics.
**Rate Limiting**: In-memory per-user chat throttle (≤30 req/min) + single active session creation attempt per basket (2 min window) using Redis fallback if memory contention emerges.
**Scale/Scope**: Demo with production-ready patterns; 6+ microservices (Catalog, CatalogDbManager, Basket, Chat, Frontend, Payment additions) minimal changes localized.
**Open Questions**: NONE (all clarified in spec) → No NEEDS CLARIFICATION markers required.

## Constitution Check (Initial)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **Service-First Architecture**: Payment logic & agent migration changes confined to existing service boundaries (no cross-db access introduced)
- [x] **AI-First Integration**: Migration plan adopts Agent Framework (ChatClientAgent + AddAIAgent DI) across ChatService
- [x] **Test-Driven Development**: Test additions planned before code (agent adapter tests, payment session lifecycle tests, webhook handling)
- [x] **Microservice Boundaries**: Order persistence remains in service-specific storage; Basket remains Redis-scoped
- [x] **Observability & Monitoring**: Minimal log + span plan documented; health checks unaffected

## Project Structure

### Documentation (this feature)

```
specs/001-agent-framework-stripe-mcp/
├── plan.md
├── spec.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── openapi.yaml
└── tasks.md (future, via /speckit.tasks)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```
AspireShop.AppHost/
AspireShop.CatalogService/
AspireShop.CatalogDb/
AspireShop.CatalogDbManager/
AspireShop.BasketService/
AspireShop.ChatService/
AspireShop.Frontend/
Shared/
AspireShop.ServiceDefaults/
Tests/
```

**Structure Decision**: Multi-project Aspire solution retaining existing microservice directories; no new top-level service introduced—payment functionality added within existing Basket/Frontend + new Order persistence extension (PostgreSQL) and Stripe webhook inside ChatService or a dedicated minimal PaymentSession handler (to be finalized in tasks phase but does not alter high-level structure).

## Complexity Tracking

*Fill ONLY if Constitution Check has violations that must be justified*

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | N/A | N/A |

---

## Phase 0: Research Summary

All clarifications addressed in spec; research captured rationale for Hosted Checkout (scope minimization), minimal observability (time-to-delivery), basic throttling (simplicity), Agent Framework adoption (simpler API + unified providers). See `research.md`.

## Phase 1: Design Outputs

Artifacts generated: `data-model.md` (entities + state), `contracts/openapi.yaml` (new endpoints), `quickstart.md` (setup & migration steps). Agent context updated via update-agent-context script.

## Constitution Check (Post-Design)
- [x] Service-First: No cross-service DB access introduced
- [x] AI-First: Agent Framework abstractions documented
- [x] TDD: Test targets enumerated in quickstart & plan
- [x] Boundaries: Order persistence isolated; Basket unchanged
- [x] Observability: Logging + single span strategy formalized

## Next Steps (Phase 2 Preview)
- Derive granular tasks (migration steps per service, adapter creation, test scaffolding, Stripe session service, webhook handler, throttling middleware, logging & span instrumentation).
- Implement feature branch with incremental commits ensuring passing tests.

## Exit Criteria for /speckit.plan
All required artifacts produced; no unresolved clarifications; gates passed pre/post design.

