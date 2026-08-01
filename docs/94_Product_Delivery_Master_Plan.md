# Mini ERP SaaS Platform — Product Delivery Master Plan

| Field | Value |
|---|---|
| Document | Product Delivery Master Plan |
| Status | Active living plan |
| Owner | Hossam |
| Repository | `Hossam1104/mini-erp-saas-platform` |
| Suggested repository path | `docs/94_Product_Delivery_Master_Plan.md` |
| Last updated | 1 August 2026 |
| Product boundary | Release 1 B2B ERP only |
| Current implementation item | `MESP-57 / TE-01 — Modular Monolith solution and module seam` |
| Current branch | `feat/mesp-57-modular-monolith-seam` |
| Current Sprint | `S1-Solution Foundation` |

---

## 1. Purpose

This document is the master execution plan for taking the Mini ERP SaaS Platform from approved product requirements through business analysis, domain design, functional specification, technical design, Jira delivery, implementation, testing, and release.

It is a living progress tracker. Completed work is marked as **Done**, the current active item is marked as **In Progress**, and remaining work stays unchecked until it is completed and verified.

---

## 2. Status legend

- [x] **Done** — completed, reviewed, and accepted.
- [ ] **Not Started** — planned but not started.
- [ ] **In Progress** — add `🔄` beside the item while actively executing it.
- [ ] **Blocked** — add `⛔` and the blocking dependency.
- [ ] **Deferred** — add `⏸` and the owning future decision or phase.

---

## 3. Current position

### Product-wide position

The project is currently in **Phase 2 — Business Requirements Documents**, because the full set of business-domain BRDs has not yet been completed.

### Platform Administration slice

The SaaS Platform Administration domain has progressed further than the rest of the product and is now at the transition from technical foundation into implementation.

| Area | Current status |
|---|---|
| Product PRD | Done |
| Product architecture baseline | Done |
| MESP-27 Platform Administration BRD | Done |
| MESP-27 implementation backlog | Done |
| MESP-27 Jira synchronization | Done |
| MESP-57 Sprint activation | Done |
| MESP-57 development branch | Done |
| MESP-57 implementation | In Progress — code not yet implemented |
| MESP-28 Identity and Access BRD | Not Started / To Do |
| Remaining product BRDs | Not Started / To Do |

### Current Jira and Git state

- [x] `MESP-27` — SaaS Platform Administration BRD is **Done**.
- [x] `MESP-2` — Platform Administration Epic moved to **In Progress**.
- [x] `MESP-57` — Modular Monolith solution and module seam moved to **In Progress**.
- [x] Sprint `S1-Solution Foundation` created and started.
- [x] Sprint contains only `MESP-57`.
- [x] `MESP-58` through `MESP-85` remain **To Do**.
- [x] `MESP-28` through `MESP-40` remain **To Do**.
- [x] Branch `feat/mesp-57-modular-monolith-seam` created from the latest `main`.
- [x] Branch pushed to `origin`.
- [x] Working tree confirmed clean before implementation.
- [ ] 🔄 Implement `MESP-57 / TE-01`.
- [ ] Review the MESP-57 implementation.
- [ ] Create and review the Pull Request.
- [ ] Merge MESP-57 into `main`.
- [ ] Move MESP-57 to **Done** after successful validation.

---

# 4. Delivery lifecycle

The product will follow this controlled lifecycle:

1. Product Requirements Document
2. Business Requirements and BPMN
3. Domain-Driven Design
4. Functional Requirements Specification
5. Data Design
6. Technical Design Specification
7. Implementation-ready Jira Backlog
8. Implementation and Automated Testing
9. Integration, UAT, Release, and Operations

A domain must not move into implementation until its applicable upstream artifacts are approved, except for explicitly approved architecture-foundation Enablers such as MESP-57.

---

# Phase 1 — Product Requirements Document

## Objective

Freeze the product vision, business scope, target market, product boundaries, architecture direction, major non-functional expectations, and Release 1 decisions.

## Required outputs

- Product vision and goals.
- Target market and primary users.
- Release 1 scope.
- Explicit exclusions.
- Product modules and capability map.
- Approved organizational hierarchy.
- SaaS and multi-tenancy principles.
- Localization and country-launch direction.
- High-level architecture baseline.
- Major product decisions and open decision register.
- Approval record.

## Progress

- [x] Product vision defined.
- [x] Release 1 B2B ERP scope defined.
- [x] Retail POS explicitly excluded.
- [x] Target market and Saudi launch direction defined.
- [x] Platform hierarchy approved:
  - Platform
  - Tenant
  - Company / Legal Entity
  - Branch
  - Warehouse
- [x] Wafra defined as Tenant #1 and validation customer only.
- [x] Tenant-specific core code prohibited.
- [x] Product architecture direction approved.
- [x] PRD v1.2 approved and frozen as the current product baseline.
- [x] Initial product decisions recorded.
- [ ] Close remaining production-gate decisions when their owning phases require them:
  - `MESP-48` — reference volumes and supported-volume evidence.
  - `MESP-50` — hosting, residency, retention, legal hold, backup, purge, and restoration policy.

## Exit criteria

- [x] Approved PRD baseline exists.
- [x] Scope and exclusions are unambiguous.
- [x] Product hierarchy and architecture direction are approved.
- [x] Remaining open decisions have explicit owners and gates.

**Phase 1 status: DONE**

---

# Phase 2 — Business Requirements Documents and BPMN

## Objective

Define the business meaning of each domain before technical implementation. Each BRD must capture workflows, actors, rules, exceptions, states, data requirements, reports, approvals, audit evidence, and business acceptance scenarios.

## Required outputs per domain

- Business purpose.
- Actors and responsibilities.
- Preconditions and triggers.
- Main workflow.
- Alternative workflows.
- Exception scenarios.
- Business rules.
- Document lifecycle.
- State transitions.
- Validation rules.
- Approval controls.
- Separation of duties.
- Permissions.
- Data requirements.
- Reports and KPIs.
- Audit evidence.
- Integration requirements.
- Migration requirements.
- Accounting impact.
- Inventory impact.
- Multi-currency impact.
- Saudi localization impact.
- Given/When/Then business acceptance scenarios.
- BPMN process diagrams.
- Open decisions.
- Founder/business-owner approval.

## Domain sequence and progress

### Foundation and governance

- [x] Product foundation and governance preparation.
- [x] Jira simplification and delivery-governance setup.
- [x] `MESP-27` — SaaS Platform Administration BRD.
- [x] MESP-27 founder approval.
- [x] MESP-27 Wave 1 wireframes and Layout B baseline.
- [ ] BPMN diagrams for all MESP-27 workflows.
- [ ] `MESP-28` — Identity and Access BRD.
- [ ] `MESP-29` — Multi-Tenancy BRD.
- [ ] `MESP-30` — Organization BRD.

### Core ERP domains

- [ ] Master Data and Catalog BRD.
- [ ] Business Parties BRD.
- [ ] Procurement BRD.
- [ ] Inventory BRD.
- [ ] B2B Sales BRD.
- [ ] Finance BRD.
- [ ] Reporting and Analytics BRD.
- [ ] Saudi Country Pack BRD.
- [ ] Files and Integrations BRD.
- [ ] Migration and cutover requirements.

## MESP-27 completed work

- [x] Business scope defined.
- [x] Actors and responsibilities defined.
- [x] Plan and Subscription model approved.
- [x] Entitlement override prohibition approved.
- [x] Tenant lifecycle defined.
- [x] Support-access controls defined.
- [x] Export and offboarding controls defined.
- [x] Purge review and certificate requirements defined.
- [x] Multiple Legal Entities per Tenant approved.
- [x] No consolidation or intercompany automation in Release 1.
- [x] MESP-48 and MESP-50 production gates preserved.
- [x] MESP-27 marked Done in Jira.

## Phase 2 exit criteria per domain

- [ ] BRD approved.
- [ ] BPMN diagrams approved.
- [ ] All critical business rules are explicit.
- [ ] State transitions are unambiguous.
- [ ] Exceptions and negative paths are covered.
- [ ] Permissions and separation-of-duties rules are approved.
- [ ] Open decisions have owners and implementation gates.
- [ ] No technical behavior is invented to fill a business gap.

**Phase 2 status: IN PROGRESS**

---

# Phase 3 — Domain-Driven Design

## Objective

Transform approved business requirements into clear domain boundaries and models without prematurely designing database tables or API payloads.

## Required outputs per domain

- Bounded Context.
- Context map and relationships.
- Ubiquitous Language updates.
- Aggregate Roots.
- Aggregates.
- Entities.
- Value Objects.
- Domain Services.
- Domain Events.
- Commands.
- Business invariants.
- Ownership boundaries.
- Cross-context contracts.
- Consistency boundaries.
- Transaction boundaries.
- Domain error taxonomy.
- Deferred design decisions.

## Planned execution

For each approved BRD:

1. Extract nouns, roles, documents, states, and business events.
2. Confirm the owning Bounded Context.
3. Define Aggregate Roots and consistency boundaries.
4. Define entities and immutable Value Objects.
5. Define domain invariants.
6. Define domain events for completed business facts.
7. Define allowed dependencies on other contexts.
8. Validate the model against every BRD workflow and exception.
9. Update the Business Glossary.
10. Obtain approval before Data Design and detailed TDS work.

## Progress

- [x] High-level module boundaries defined in the architecture baseline.
- [x] Platform Administration identified as a distinct module boundary.
- [ ] Formal Platform Administration DDD pack.
- [ ] Identity and Access DDD pack.
- [ ] Multi-Tenancy DDD pack.
- [ ] Organization DDD pack.
- [ ] Remaining domain DDD packs.

## Exit criteria per domain

- [ ] Every business concept has one owning context.
- [ ] Aggregate boundaries are explicit.
- [ ] Business invariants are enforceable.
- [ ] Cross-context dependencies are approved.
- [ ] No shared business-model dumping ground exists.
- [ ] The model covers all BRD workflows and exceptions.

**Phase 3 status: NOT STARTED FORMALLY**

---

# Phase 4 — Functional Requirements Specification

## Objective

Describe every approved feature from the user’s perspective, including user journeys, screens, actions, inputs, outputs, states, validations, permissions, errors, reports, localization, and functional acceptance criteria.

## Required outputs per domain

- Feature catalogue.
- Actor-to-feature matrix.
- User journeys.
- Navigation flows.
- Screen and page inventory.
- Fields and controls.
- Input validation.
- Default values.
- Read-only and editable states.
- Empty, loading, error, restricted, and no-result states.
- Search, filter, sort, and pagination behavior.
- Notifications and confirmations.
- Reports and exports.
- Localization and RTL behavior.
- Accessibility expectations.
- Functional acceptance criteria.
- Traceability to BRD requirements and BPMN steps.

## Progress

- [x] MESP-27 Wave 1 low-fidelity wireframes exist.
- [x] Layout B selected as the Tenant Workspace baseline.
- [x] MESP-27 implementation Stories contain user-facing outcomes and acceptance criteria.
- [ ] Formal MESP-27 FRS.
- [ ] Formal Identity and Access FRS.
- [ ] Formal Multi-Tenancy FRS.
- [ ] Formal Organization FRS.
- [ ] Remaining domain FRS documents.

## Exit criteria per domain

- [ ] Every BRD capability maps to one or more functions.
- [ ] User-visible behavior is unambiguous.
- [ ] Validation and state behavior are defined.
- [ ] Error and restricted states are covered.
- [ ] Localization and accessibility expectations are included.
- [ ] Functional acceptance criteria are testable.

**Phase 4 status: PARTIAL FOR MESP-27, OTHERWISE NOT STARTED**

---

# Phase 5 — Data Design

## Objective

Design the logical and physical data structures required to support approved domain behavior, tenant isolation, business integrity, performance, auditability, retention, and reporting.

## Required outputs per domain

### Logical design

- Logical ERD.
- Business entities.
- Relationships and cardinality.
- Ownership boundaries.
- Tenant and organizational scope.
- Lifecycle and state history.
- Audit and evidence requirements.
- Retention classification.

### Physical design

- Module-owned database schema.
- Tables.
- Columns and data types.
- Primary keys.
- Foreign keys.
- Unique constraints.
- Check constraints.
- Tenant keys.
- Organizational keys.
- Concurrency fields.
- Audit columns.
- Index strategy.
- Partitioning decision where justified.
- Temporal/history strategy where justified.
- Migration strategy.
- Seed/reference-data strategy.
- Backup and purge impact.
- Reporting/read-model requirements.

## Approved baseline constraints

- [x] Shared SQL Server database approved for Release 1.
- [x] Module-owned schemas approved.
- [x] Database-per-Tenant excluded.
- [x] Direct cross-module table mutation prohibited.
- [x] Tenant isolation required across persistence, jobs, files, exports, and audit.
- [ ] Detailed tenant-isolation data model.
- [ ] Logical ERDs.
- [ ] Physical ERDs.
- [ ] Domain indexing strategy.
- [ ] Domain migration plans.

## Exit criteria per domain

- [ ] Logical ERD approved.
- [ ] Physical ERD approved.
- [ ] Tenant and organizational scoping is explicit.
- [ ] Constraints enforce critical invariants.
- [ ] Indexes support expected queries and supported volumes.
- [ ] Audit, retention, and purge effects are covered.
- [ ] No table ownership conflict exists between modules.

**Phase 5 status: BASELINE DECISIONS DONE, DETAILED DESIGN NOT STARTED**

---

# Phase 6 — Technical Design Specification

## Objective

Define how the approved business and functional behavior will be implemented across the solution, APIs, database, authorization, integrations, background processing, observability, security, and deployment.

## Required outputs per domain

- Component architecture.
- Module boundaries.
- Application-service design.
- Domain-service design.
- Public contracts.
- API contracts.
- OpenAPI definitions.
- Request and response models.
- Error contracts.
- Authentication requirements.
- Authorization policies.
- Resource and scope checks.
- Transaction boundaries.
- Idempotency approach.
- Concurrency behavior.
- Database mapping.
- Outbox and background-job design.
- Integration contracts.
- File and object-storage behavior.
- Logging.
- Metrics.
- Tracing.
- Health checks.
- Rate limiting.
- Threat model.
- Security controls.
- Configuration.
- Deployment requirements.
- Technical test strategy.
- Failure and recovery behavior.
- Traceability to BRD, DDD, FRS, and Data Design.

## Progress

### Product-level technical baseline

- [x] Modular Monolith approved.
- [x] Angular 22 approved.
- [x] TypeScript approved.
- [x] ASP.NET Core Web API on .NET 10 LTS approved.
- [x] EF Core 10 approved.
- [x] SQL Server 2025 approved.
- [x] REST and OpenAPI approved.
- [x] ASP.NET Core Identity direction approved.
- [x] Secure HTTP-only cookie direction approved.
- [x] Policy/resource authorization direction approved.
- [x] Docker Compose local-dependency direction approved.
- [x] OpenTelemetry direction approved.
- [x] xUnit and Playwright TypeScript direction approved.
- [x] Transactional outbox/inbox direction approved.
- [ ] Detailed Identity and Access TDS.
- [ ] Detailed Multi-Tenancy TDS.
- [ ] Detailed Organization TDS.
- [ ] Detailed domain API and database specifications.
- [ ] Detailed production deployment design.

### Current MESP-57 technical foundation

- [x] MESP-57 implementation scope approved.
- [x] MESP-57 Demonstration Outcome defined.
- [x] MESP-57 Definition of Done defined.
- [x] MESP-57 branch created.
- [x] MESP-57 Sprint activated.
- [ ] 🔄 Implement the three-project Modular Monolith seam.
- [ ] Add targeted architecture validation.
- [ ] Build and demonstrate the API host/module registration seam.
- [ ] Review MESP-57 implementation.
- [ ] Merge MESP-57.

## Exit criteria per domain

- [ ] Detailed design covers all approved functions.
- [ ] API, data, authorization, and transaction behavior are explicit.
- [ ] Security and tenant-isolation controls are reviewable.
- [ ] Failure and recovery paths are defined.
- [ ] Observability and operational requirements are included.
- [ ] Technical risks and deferred decisions are recorded.
- [ ] No implementation depends on an unresolved critical decision.

**Phase 6 status: HIGH-LEVEL BASELINE DONE; DETAILED DOMAIN TDS PENDING**

---

# Phase 7 — Implementation-Ready Jira Backlog

## Objective

Convert approved specifications into a controlled, traceable, sequenced backlog that can be implemented without inventing business or technical behavior.

## Required Jira structure

- Epics.
- Features where the Jira configuration supports them.
- Technical Enablers.
- User Stories.
- Tasks only when independently deliverable.
- Bugs.
- Releases and Fix Versions.
- Acceptance criteria.
- Dependencies.
- Estimates.
- Design references.
- Traceability.
- Definition of Ready.
- Definition of Done.
- Demonstration outcome.
- Sprint proposal.

## MESP-27 backlog progress

- [x] Existing Epic `MESP-2` reused.
- [x] Duplicate implementation Epic avoided.
- [x] 8 Technical Enablers created:
  - `MESP-57` through `MESP-64`.
- [x] 21 User Stories created:
  - `MESP-65` through `MESP-85`.
- [x] All issues mapped to MESP-2.
- [x] Component assigned.
- [x] Release assigned.
- [x] Sequence labels added.
- [x] Dependency pairs created.
- [x] Dependency graph confirmed acyclic.
- [x] Jira descriptions repaired for encoding issues.
- [x] MESP-57 refined for implementation.
- [x] Sprint `S1-Solution Foundation` created with MESP-57 only.
- [ ] Refine each future Enabler or Story immediately before implementation.
- [ ] Populate native estimation fields when Jira configuration supports them.
- [ ] Create Bugs only from verified defects.
- [ ] Keep future items out of active Sprints until Definition of Ready is met.

## Definition of Ready

A future Story or Enabler may enter a Sprint only when:

- [ ] Applicable BRD is approved.
- [ ] BPMN workflow is approved.
- [ ] DDD ownership and invariants are approved.
- [ ] FRS behavior is approved.
- [ ] Data Design is approved.
- [ ] TDS is approved.
- [ ] Dependencies are resolved or explicitly sequenced.
- [ ] Acceptance criteria are testable.
- [ ] Security impact is reviewed.
- [ ] Tenant-isolation impact is reviewed.
- [ ] MESP-48 or MESP-50 gates are resolved where applicable.
- [ ] Traceability is complete.
- [ ] No unresolved critical product decision remains.

## Definition of Done for Jira preparation

- [ ] Correct parent Epic.
- [ ] Correct issue type.
- [ ] Clear single outcome.
- [ ] Scope and exclusions recorded.
- [ ] Acceptance criteria complete.
- [ ] Dependencies correct.
- [ ] Estimate recorded.
- [ ] Demonstration outcome recorded.
- [ ] Technical and design references recorded.
- [ ] No duplicate issue exists.
- [ ] No parallel-work recommendation exists.

**Phase 7 status: DONE FOR MESP-27 WAVE 1; PENDING FOR OTHER DOMAINS**

---

# Phase 8 — Implementation and Automated Testing

## Objective

Implement approved backlog items sequentially, validate them with focused automated tests, review the code, merge safely, and preserve traceability from requirements to release evidence.

## Standard execution flow per item

1. Confirm the item is implementation-ready.
2. Add only the approved item to the active Sprint.
3. Move the item to In Progress.
4. Create one feature branch.
5. Read only the required approved sources.
6. Implement the smallest complete scope.
7. Add targeted automated tests.
8. Run targeted validation.
9. Review the complete task-related diff.
10. Commit with the Jira key.
11. Push the branch.
12. Add implementation evidence to Jira.
13. Perform independent review.
14. Create Pull Request.
15. Resolve review findings.
16. Merge into `main`.
17. Run post-merge validation.
18. Demonstrate the outcome.
19. Move the Jira item to Done.
20. Select the next approved item.

## Current implementation sequence

### Sprint 1 — Solution Foundation

- [x] Sprint created.
- [x] Sprint started.
- [x] MESP-57 added as the only Sprint item.
- [x] MESP-2 moved to In Progress.
- [x] MESP-57 moved to In Progress.
- [x] Development branch created and published.
- [ ] 🔄 Implement MESP-57.
- [ ] Run MESP-57 targeted build and architecture validation.
- [ ] Review MESP-57 code and evidence.
- [ ] Create MESP-57 Pull Request.
- [ ] Merge MESP-57.
- [ ] Move MESP-57 to Done.
- [ ] Close Sprint 1 after MESP-57 completion.

### After MESP-57

Do not automatically start MESP-58.

The next activity will be selected based on approved dependencies:

- [ ] Resume `MESP-28` Identity and Access BRD.
- [ ] Complete `MESP-29` Multi-Tenancy BRD.
- [ ] Complete `MESP-30` Organization BRD.
- [ ] Produce the related DDD, FRS, Data Design, and TDS artifacts.
- [ ] Refine the next Enabler only after its Definition of Ready is met.

## Testing strategy

- xUnit for:
  - Domain rules.
  - Application behavior.
  - Persistence.
  - Authorization.
  - Concurrency.
  - Architecture boundaries.
  - Integration behavior.

- Playwright TypeScript for:
  - Critical browser journeys.
  - API journeys.
  - Cookie and antiforgery behavior.
  - RTL behavior.
  - Tenant-isolation failures.
  - End-to-end business flows.

- Additional validation where needed:
  - SQL integrity checks.
  - Contract validation.
  - Security checks.
  - Performance checks against MESP-48 evidence.
  - Recovery and retry behavior.
  - Deployment smoke tests.

## Exit criteria per implementation item

- [ ] Approved scope implemented.
- [ ] No later-phase scope introduced.
- [ ] Targeted automated tests pass.
- [ ] Build passes.
- [ ] Security and tenant-isolation checks pass where applicable.
- [ ] No known P0 or P1 defect remains.
- [ ] Code review completed.
- [ ] Pull Request merged.
- [ ] Jira evidence added.
- [ ] Demonstration completed.
- [ ] Documentation updated only where necessary.

**Phase 8 status: IN PROGRESS — MESP-57 IS THE FIRST IMPLEMENTATION ITEM**

---

# Phase 9 — Integration, UAT, Release, and Operations

## Objective

Validate the complete product across modules, prepare production operations, complete business acceptance, release safely, and monitor the platform after deployment.

## Required outputs

- Integrated test environment.
- Integration test plan.
- Cross-module process validation.
- Data migration rehearsal.
- Security assessment.
- Performance and supported-volume validation.
- Backup and restoration validation.
- Monitoring and alerting.
- Operational runbooks.
- Deployment checklist.
- Rollback plan.
- UAT plan.
- UAT evidence.
- Release notes.
- Training material.
- Support handover.
- Production readiness review.
- Go-live approval.
- Post-release monitoring.
- Incident and defect process.
- Release retrospective.

## Progress

- [ ] Integration environment.
- [ ] Full cross-domain integration validation.
- [ ] MESP-48 supported-volume validation.
- [ ] MESP-50 production-policy decisions.
- [ ] Security testing.
- [ ] Performance testing.
- [ ] Migration rehearsal.
- [ ] Backup and restore test.
- [ ] UAT.
- [ ] Production readiness review.
- [ ] Release 1 deployment.
- [ ] Post-release monitoring.

## Exit criteria

- [ ] UAT approved.
- [ ] Critical defects resolved.
- [ ] Security risks accepted or resolved.
- [ ] Performance targets validated.
- [ ] Operational ownership established.
- [ ] Backup and restoration proven.
- [ ] Deployment and rollback tested.
- [ ] Production gates approved.
- [ ] Founder release approval recorded.

**Phase 9 status: NOT STARTED**

---

# 5. Domain delivery order

The expected execution order is:

1. Platform Administration foundation.
2. Identity and Access.
3. Multi-Tenancy.
4. Organization.
5. Master Data and Catalog.
6. Business Parties.
7. Procurement.
8. Inventory.
9. B2B Sales.
10. Finance.
11. Reporting and Analytics.
12. Saudi Country Pack.
13. Files and Integrations.
14. Migration, UAT, and Release.

This order may be adjusted only when approved dependencies require a different sequence.

---

# 6. Requirements traceability

Every implemented capability must preserve this chain:

```text
PRD requirement
→ BRD requirement and business rule
→ BPMN activity
→ DDD context, aggregate, and invariant
→ FRS feature and user behavior
→ Logical and physical data design
→ TDS component, API, and security control
→ Jira Enabler, Story, or Task
→ Automated and manual test evidence
→ Commit and Pull Request
→ Release evidence
```

## Traceability rules

- [ ] No Jira implementation item without an upstream approved source.
- [ ] No approved requirement omitted from downstream artifacts.
- [ ] No API or table created without an owning domain.
- [ ] No code behavior invented to resolve an unapproved business decision.
- [ ] Every acceptance criterion maps to validation evidence.
- [ ] Every release item maps to a Jira key and Pull Request.
- [ ] Every production gate maps to an approval or evidence record.

---

# 7. Cross-cutting controls

These controls must be reviewed in every applicable phase and domain.

## Multi-tenancy

- Tenant isolation.
- Organizational scope.
- Cross-Tenant denial.
- Background jobs.
- Files and exports.
- Audit evidence.
- Database constraints.
- Support access.

## Security

- Authentication.
- Authorization.
- Least privilege.
- Separation of duties.
- Session handling.
- Privileged access.
- Export authorization.
- Auditability.
- Secrets management.
- Threat modeling.

## Data integrity

- Transaction boundaries.
- Idempotency.
- Concurrency.
- Referential integrity.
- Decimal and currency precision.
- State transition integrity.
- Immutable evidence.
- Migration safety.

## Localization

- English and Arabic.
- RTL behavior.
- LTR handling for identifiers.
- Saudi defaults.
- Country-pack extensibility.
- Accessible status presentation.

## Operations

- Logging.
- Metrics.
- Tracing.
- Correlation identifiers.
- Health checks.
- Retry behavior.
- Background-job visibility.
- Backup and restoration.
- Purge and legal hold.
- Incident evidence.

---

# 8. Model usage policy

## Luna Max — default model

Use Luna Max for:

- BRDs.
- BPMN preparation.
- DDD drafting.
- FRS preparation.
- Data Design drafting.
- TDS drafting.
- Jira backlog work.
- Jira synchronization.
- Routine implementation.
- Documentation updates.
- Normal self-review.

## Codex / project direction

Use Codex for:

- Directing the next step.
- Reviewing Luna deliverables.
- Checking Jira and GitHub state.
- Detecting scope drift.
- Issuing bounded execution prompts.
- Verifying implementation reports.
- Deciding whether a critical specialist review is required.

## SOL High — critical use only

Use SOL High only for:

- Tenant-isolation architecture.
- Authentication and authorization security.
- Accounting and posting integrity.
- Irreversible physical database design.
- Data-loss, purge, retention, and restoration decisions.
- Severe contradictions between approved sources.
- High-impact production architecture changes.

## Sonnet

Sonnet is not part of the normal workflow. Use it only when explicitly approved and quota is available for a narrowly scoped independent review.

---

# 9. Immediate action plan

## Current action

- [ ] 🔄 Execute the approved Luna prompt for `MESP-57 / TE-01`.

## Expected MESP-57 outputs

- [ ] `backend/MiniErp.sln`.
- [ ] `backend/Directory.Build.props`.
- [ ] `backend/Directory.Packages.props`.
- [ ] `backend/src/MiniErp.Api`.
- [ ] `backend/src/MiniErp.App`.
- [ ] `backend/src/MiniErp.Contracts`.
- [ ] `backend/tests/MiniErp.ArchitectureTests`.
- [ ] Platform Administration module registration seam.
- [ ] Public contracts separated from internal module code.
- [ ] Minimal API startup evidence.
- [ ] Targeted architecture validation.
- [ ] Local build instructions.
- [ ] Successful restore and build.
- [ ] Focused commit.
- [ ] Remote push.
- [ ] Jira evidence comment.

## Review gate after Luna finishes

- [ ] Stop before creating the Pull Request.
- [ ] Review the full MESP-57 diff.
- [ ] Verify the project dependency graph.
- [ ] Verify no future-Enabler scope was implemented.
- [ ] Verify build and architecture validation evidence.
- [ ] Confirm no secrets or generated local files were committed.
- [ ] Approve corrections or authorize Pull Request creation.

## After MESP-57 is merged

- [ ] Move MESP-57 to Done.
- [ ] Complete Sprint 1.
- [ ] Return to `main`.
- [ ] Begin the next approved requirements activity.
- [ ] Expected next requirements item: `MESP-28 — Identity and Access BRD`.
- [ ] Do not start another implementation Enabler until its Definition of Ready is satisfied.

---

# 10. Progress log

Use this section to record major milestones.

| Date | Item | Status | Evidence / Notes |
|---|---|---|---|
| 31 July 2026 | PRD v1.2 approved | Done | Final approved product baseline |
| 1 August 2026 | Architecture baseline approved | Done | Modular Monolith, Angular 22, .NET 10, EF Core 10, SQL Server 2025 |
| 1 August 2026 | MESP-27 BRD approved | Done | Platform Administration BRD v0.10 |
| 1 August 2026 | MESP-27 Jira backlog synchronized | Done | MESP-57–64 and MESP-65–85 under MESP-2 |
| 1 August 2026 | Sprint 1 activated | Done | `S1-Solution Foundation`, MESP-57 only |
| 1 August 2026 | Development branch published | Done | `feat/mesp-57-modular-monolith-seam` |
| 1 August 2026 | MESP-57 implementation | In Progress | Code execution not yet started at document creation |

---

# 11. Change-control rules

- Update this file after each approved milestone.
- Do not mark an item Done based only on a model report.
- Mark Done only after checking the actual Jira state, repository change, generated artifact, or approval evidence.
- Do not change completed product decisions without an explicit decision record.
- Do not silently reorder domain dependencies.
- Do not create implementation scope from unresolved BRD, DDD, FRS, Data Design, or TDS gaps.
- Keep one active implementation item unless an explicit founder decision authorizes otherwise.
- Preserve Release 1 B2B ERP scope.
- Keep Retail POS excluded.
- Keep Wafra as Tenant #1 and a validation customer, not a source of Tenant-specific core code.

---

# 12. Overall progress summary

| Phase | Status |
|---|---|
| Phase 1 — PRD | **Done** |
| Phase 2 — BRDs and BPMN | **In Progress** |
| Phase 3 — DDD | **Not Started Formally** |
| Phase 4 — FRS | **Partial for MESP-27** |
| Phase 5 — Data Design | **Baseline Decisions Done; Detailed Design Pending** |
| Phase 6 — TDS / Technical Architecture | **High-Level Baseline Done; Detailed Designs Pending** |
| Phase 7 — Jira Backlog | **Done for MESP-27 Wave 1** |
| Phase 8 — Implementation and Automated Testing | **In Progress — MESP-57** |
| Phase 9 — Integration, UAT, Release, Operations | **Not Started** |

---

## Current single next action

> Implement `MESP-57 / TE-01 — Modular Monolith solution and module seam` on branch `feat/mesp-57-modular-monolith-seam`, then stop for review before creating a Pull Request.
