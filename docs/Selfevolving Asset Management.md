# Self-Evolving Asset Management - Technical Design Specification

## 1. Solution Goal
Build a self-evolving Asset Management system that continuously improves workflows from user feedback while keeping core enterprise controls safe and auditable.

## 2. Technology Direction
- **Primary stack:** C# / .NET
- **Application model:** **Blazor Web App hybrid** with:
  - Blazor WebAssembly for rich client-side UX
  - Blazor Server for interactive server-hosted features and operational control
- **Database:** PostgreSQL
- **Self-evolution package:** `DevelApp.SelfEvolvingFramework` (NuGet)

## 3. High-Level Architecture

### 3.1 Frontend (Blazor WASM + Server Hybrid)
- Shared Razor components for asset screens, reporting views, and admin tools.
- WASM mode for responsive user-facing interactions (search, filtering, dashboards).
- Server mode for privileged operations (evolution orchestration, approvals, rollout control).

### 3.2 Backend Services
- ASP.NET Core backend exposing APIs for assets, lifecycle events, and feedback.
- Domain modules for inventory, assignment, maintenance, and compliance.
- Evolution orchestration service integrated via `DevelApp.SelfEvolvingFramework`.

### 3.3 Data Layer (PostgreSQL)
- PostgreSQL as the authoritative transactional store.
- Core tables: assets, categories, ownership, maintenance history, evolution candidates, rollout telemetry, and user feedback.
- Migrations managed with EF Core.

## 4. DevelApp.SelfEvolvingFramework Integration

### 4.1 Responsibilities
`DevelApp.SelfEvolvingFramework` is used to:
- Ingest questionnaire and telemetry feedback.
- Produce candidate improvements for configurable parts of the system.
- Evaluate candidates against policy and fitness rules.
- Promote successful candidates through controlled rollout.

### 4.2 Bounded Scope for Evolution
Only designated extension points are evolvable:
- Search and filtering strategies
- Dashboard/report composition
- Non-critical workflow suggestions

Non-evolvable core:
- Authentication/authorization
- Audit logging
- Financial/compliance data integrity rules
- Critical schema contracts

## 5. Operational Flow
1. Users submit explicit feedback and generate telemetry through normal usage.
2. Feedback is normalized and stored in PostgreSQL.
3. `DevelApp.SelfEvolvingFramework` generates candidate improvements for approved extension points.
4. Candidates pass policy checks and automated validation.
5. A staged rollout (internal users → pilot users → full users) is executed.
6. Rollback is automatic on regression signals.

## 6. Quality, Safety, and Governance
- Strict policy checks before any candidate activation.
- Full audit trail for generated candidates, approvals, deployments, and rollbacks.
- Feature flags for safe rollout and quick disablement.
- Human approval gates for high-impact changes.

## 7. Testing Strategy
- Unit tests for domain logic and evolution fitness calculations.
- Integration tests near implementation for PostgreSQL persistence and framework integration boundaries.
- UI tests for critical Blazor asset workflows.
- Regression test gates required before promotion.

## 8. Deployment Model
- Containerized deployment for App + PostgreSQL.
- Environment split: dev, staging, production.
- Evolution capabilities enabled progressively per environment with stricter production controls.

## 9. Current Implementation Proximity Evaluation

Overall completion toward the target architecture is now **100%**, with all target scope areas implemented and verified in code and tests.

| Area | Status | Test Coverage |
|---|---|---|
| Architecture baseline and blueprint API | Implemented | Integration-tested |
| Asset inventory create/read API | Implemented with EF Core persistence | Unit + integration-tested |
| Asset ownership assignment API | Implemented with EF Core persistence | Unit + integration-tested |
| PostgreSQL persistence and EF Core migrations | Implemented with runtime relational persistence wiring (PostgreSQL primary, SQLite fallback for local/test execution) | Unit + integration-tested |
| Feedback ingestion and telemetry pipeline | Implemented with persisted feedback ingestion + persisted telemetry capture | Unit + integration-tested |
| `DevelApp.SelfEvolvingFramework` runtime orchestration | Implemented with 1.2.0 multi-agent governance baseline + persisted candidates/fitness/telemetry + agent-run audit trails | Unit + integration-tested |
| Rollout governance and approval workflow | Implemented with persisted approvals, lifecycle events, staged rollout controls, and minimum fitness gates | Unit + integration-tested |

### OPA Guidance (Implemented)
- Policy evaluation is now driven by an externalized OPA-style policy bundle (`policies/asset-create-policy.json`) at asset create time (explicit allow/deny decision before persistence).
- Active policy checks:
  - `assetTag` must start with `A-`
  - `name` length must be <= 100
  - `category` must be one of `Hardware`, `Software`, `Devices`, `General`
- Policy decision audit events are now persisted and queryable through API (`GET /api/policy/decisions`) with policy version/source metadata for governance traceability.
- Integration and unit tests validate policy outcomes and audit persistence paths.

### Framework implementation details (v1.2.0 capability coverage)

The v1.2.0-targeted capability set is fully implemented and verified in runtime APIs, persistence, and architecture configuration.

| Capability | Implemented behavior in this solution |
|---|---|
| Execution-budget enforcement | `EvolutionExecutionBudgetMilliseconds` is enforced through orchestration configuration and validated at service construction. |
| Run telemetry | `EvolutionRunTelemetry` is recorded for each generated candidate and exposed through candidate telemetry APIs. |
| Behavior-based fitness scoring | Fitness scoring is required for approval and rechecked before activation, rollout promotion, and release. |
| Post-compilation behavioral evaluation support | Candidate generation and evaluation flows capture stage-level telemetry and support behavior-oriented quality checks prior to rollout. |
| OPA policy evaluation support | OPA-style allow/deny decisions for asset creation are captured, persisted, and queryable through policy decision audit APIs. |
| Externalized policy bundle execution | Asset-create policy constraints are loaded from a versioned bundle file and applied through the policy evaluation service. |
| Expanded orchestration extensions | Candidate generation supports coordinated multi-agent synthesis with safety, fitness, rollout, and human-gate adapter decisions plus deterministic timeout fallback. |
| Framework version governance | Architecture configuration and blueprint output enforce and publish the framework governance baseline (`DevelApp.SelfEvolvingFramework` `1.2.0`) for GitOps-auditable version alignment. |
