<!--
Sync Impact Report:
- Version change: 1.0.0 → 1.1.0
- Modified principles: AI-First Integration (updated from Semantic Kernel to Agent Framework)
- Technology Standards updated: Core Technology Stack and AI Integration Requirements sections
- Templates requiring updates: ✅ plan-template.md (updated), ✅ spec-template.md (aligned), ✅ tasks-template.md (updated)
- Follow-up TODOs: None - migration from Semantic Kernel to Agent Framework completed
-->

# AspireShop with Agent Framework Constitution

## Core Principles

### I. Service-First Architecture
Every feature MUST be implemented as an independently deployable service; Services MUST be self-contained with clear boundaries, independently testable, and documented; Each service MUST have a single responsibility aligned with business capabilities; Cross-service communication MUST use well-defined contracts (HTTP APIs, gRPC, or message queues).

**Rationale**: Microservices architecture enables independent scaling, deployment, and team ownership while maintaining system resilience and allowing different services to evolve at different rates.

### II. AI-First Integration
AI capabilities MUST be treated as first-class services with proper abstraction layers; Microsoft Agent Framework integration MUST use IChatClient abstraction with ChatClientAgent pattern; AI services MUST support multiple providers (Azure OpenAI, OpenAI Responses, Azure AI Foundry) through unified interfaces; All AI interactions MUST be observable with built-in OpenTelemetry integration for debugging and monitoring.

**Rationale**: AI functionality is central to the application's value proposition and must be architected using Agent Framework's simplified API, better performance, and unified interface across different AI providers while maintaining observability and testability.

### III. Test-Driven Development (NON-NEGOTIABLE)
xUnit tests MUST be written before implementation; All services MUST achieve minimum 80% code coverage; Tests MUST be categorized: unit tests (service logic), integration tests (service boundaries), and end-to-end tests (user scenarios); Mock frameworks (Moq) MUST be used for external dependencies.

**Rationale**: TDD ensures code quality, design clarity, and regression prevention in a microservices environment where service interactions can create complex failure modes.

### IV. Microservice Boundaries
Service boundaries MUST align with business domains (Catalog, Basket, Chat, Frontend); Database-per-service pattern MUST be enforced (PostgreSQL for Catalog, Redis for Basket); Services MUST NOT directly access other services' databases; All inter-service communication MUST go through published APIs.

**Rationale**: Clear boundaries prevent tight coupling, enable independent data model evolution, and support team autonomy while maintaining system integrity.

### V. Observability & Monitoring
All services MUST implement structured logging with correlation IDs; Health checks MUST be implemented for all services and dependencies; Aspire dashboard integration MUST be maintained for local development observability; Production telemetry MUST be collected for performance monitoring and AI interaction tracking.

**Rationale**: Microservices complexity requires comprehensive observability to diagnose issues, track AI performance, and ensure system reliability across service boundaries.

## Technology Standards

### Core Technology Stack
- **Runtime**: .NET 9.0 with latest Aspire hosting framework
- **AI Framework**: Microsoft Agent Framework with Microsoft.Extensions.AI abstraction layer
- **AI Packages**: Microsoft.Agents.AI (core), Microsoft.Agents.AI.OpenAI (providers), Microsoft.Agents.AI.Hosting (DI integration)
- **Data Storage**: PostgreSQL (catalog data), Redis (session/basket data)
- **Testing**: xUnit, Moq, coverlet for coverage analysis
- **Container Orchestration**: .NET Aspire for local development, Azure Container Apps for production
- **Security**: Azure Identity integration, user secrets for development configuration

### AI Integration Requirements
- MUST support Azure OpenAI, OpenAI Responses, and Azure AI Foundry providers through IChatClient abstraction
- MUST use ChatClientAgent for agent creation with simplified API patterns
- MUST implement dependency injection with services.AddAIAgent() extension methods
- MUST leverage built-in OpenTelemetry integration for AI operation tracing and monitoring
- MUST implement proper AI tool registration using AIFunctionFactory.Create() patterns
- AI agents MUST use AgentRunResponse and AgentRunResponseUpdate for consistent response handling
- Agent threads MUST be created using agent.GetNewThread() pattern for provider abstraction

## Development Workflow

### Code Quality Gates
1. **Pre-commit**: All code MUST pass local build and unit tests
2. **Pull Request**: MUST include tests for new functionality, MUST maintain or improve coverage, MUST pass all CI checks including CodeQL security analysis
3. **Integration**: MUST pass integration tests against containerized dependencies
4. **Deployment**: MUST pass health checks in target environment before traffic routing

### Branching Strategy
- **Main branch**: Production-ready code only, protected with required PR reviews
- **Feature branches**: Named `[issue-number]-feature-name`, MUST be short-lived (<1 week)
- **Agent branches**: AI-assisted development branches following same naming convention
- All changes MUST go through pull requests with automated CI validation

### Azure Developer CLI Integration
- Local development MUST use `azd up` for consistent environment provisioning
- Infrastructure as Code MUST be maintained in `azure.yaml` and Bicep files
- Secret management MUST use Azure Key Vault for production, user secrets for development
- Deployment automation MUST support multiple environments (dev, staging, production)

## Governance

This constitution supersedes all other development practices and architectural decisions. All pull requests and code reviews MUST verify compliance with these principles. Architectural complexity MUST be justified against business requirements and MUST NOT violate service boundary principles.

Amendments require:
1. Documentation of proposed changes with architectural impact analysis
2. Team review and approval process
3. Migration plan for existing code if breaking changes are introduced
4. Update of all dependent templates and documentation

All development decisions MUST consider the dual nature of this application as both a distributed system showcase and an AI-powered user experience.

**Version**: 1.1.0 | **Ratified**: 2025-10-18 | **Last Amended**: 2025-10-18
