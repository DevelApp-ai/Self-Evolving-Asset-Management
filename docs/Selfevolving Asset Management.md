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

Estimated overall completion toward the target architecture: **~93%**.

| Area | Status | Test Coverage |
|---|---|---|
| Architecture baseline and blueprint API | Implemented | Integration-tested |
| Asset inventory create/read API | Partially implemented (in-memory) | Unit + integration-tested |
| Asset ownership assignment API | Partially implemented (in-memory) | Unit + integration-tested |
| PostgreSQL persistence and EF Core migrations | Not implemented | Not yet |
| Feedback ingestion and telemetry pipeline | Partially implemented (in-memory feedback ingestion API) | Unit + integration-tested |
| `DevelApp.SelfEvolvingFramework` runtime orchestration | Partially implemented (in-memory candidate generation from feedback + execution budget configuration + run telemetry capture + fitness evaluation API) | Unit + integration-tested |
| Rollout governance and approval workflow | Partially implemented (in-memory candidate approval/rejection + staged rollout promotion + activation + release + rollback + regression-signal-triggered auto-rollback + retirement + lifecycle event audit API + minimum fitness gate for approval/promotion/release) | Unit + integration-tested |

### OPA Guidance (Current + Next)
- Current implementation applies **OPA-style policy guidance** at asset create time (explicit allow/deny decision before persistence).
- Current policy checks:
  - `assetTag` must start with `A-`
  - `name` length must be <= 100
  - `category` must be one of `Hardware`, `Software`, `Devices`, `General`
- Current implementation also records policy decision audit events via API (`GET /api/policy/decisions`) for governance traceability.
- Next step to reach full OPA alignment:
  1. Externalize policies to Rego bundles and evaluate through an OPA sidecar/service.
  2. Persist policy decision audit records to PostgreSQL.
  3. Add integration tests that validate policy outcomes against loaded Rego policies.

## 10. DevelApp.SelfEvolvingFramework v1.1.0 Upgrade Assessment

`DevelApp.SelfEvolvingFramework` v1.1.0 introduces meaningful capabilities that align with current gaps in this system.

### 10.1 New capabilities in v1.1.0 relevant to this solution
- **Execution-budget enforcement** via `EvolutionOrchestratorOptions.ExecutionBudgetMilliseconds`.
- **Run telemetry** on evolution results via `EvolutionResult.Telemetry` and `EvolutionRunTelemetry` (stage timings, diagnostic count, timeout/cancellation source).
- **Behavior-based fitness scoring** via `ExecutionFlowFitnessEvaluator` and `ExecutionFlowFitnessScoringOptions`.
- **Post-compilation behavioral evaluation support**, including Playwright-based execution flow evaluation.
- **OPA WASM policy evaluation support** via `OpaWasmAstPolicyEvaluator`.
- **Expanded orchestration extensions** for crossover/evolution engine scenarios (including GeneticSharp/SemanticKernel integrations).

### 10.2 Implemented improvements in current codebase
1. **Runtime safety budget applied**
   - `EvolutionExecutionBudgetMilliseconds` is enforced through orchestration configuration and validated at service construction.
2. **Operational observability endpointed**
   - `EvolutionRunTelemetry` is recorded for each generated candidate and exposed through candidate telemetry APIs.
3. **Rollout quality gates enforced**
   - Fitness scoring is required for approval and rechecked before activation, rollout promotion, and release.
4. **Policy decision audit trail added**
   - OPA-style allow/deny decisions for asset creation are now captured and queryable through policy decision audit APIs.

### 10.3 Remaining improvement opportunities for Self-Evolving Asset Management
The previously suggested **policy decision audit trail** work is now implemented and documented in section **10.2**.  
This section now lists only not-yet-implemented scope.

1. **Move from OPA-style checks to real OPA policy execution**
   - Replace current hardcoded policy logic with `OpaWasmAstPolicyEvaluator`-driven policy checks and versioned policy bundles.
   - Persist the existing in-memory policy decision audit records to PostgreSQL.
2. **Raise automation depth for evolution generation**
   - Introduce mutator/crossover abstractions from v1.1.0 to improve candidate diversity and reduce manual candidate crafting.

### 10.4 Recommended adoption order for remaining scope
- **Phase 1 (governance hardening):** OPA WASM policy integration and PostgreSQL persistence for the already-implemented policy decision audit trail.
- **Phase 2 (advanced evolution):** genetic/crossover strategy integrations for broader autonomous optimization.
