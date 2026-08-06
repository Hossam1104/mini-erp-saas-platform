# Mini ERP SaaS Platform — Product Delivery Master Plan

| Field | Value |
|---|---|
| Document | Product Delivery Master Plan |
| Status | Active living plan |
| Owner | Hossam |
| Repository | `Hossam1104/mini-erp-saas-platform` |
| Suggested repository path | `docs/94_Product_Delivery_Master_Plan.md` |
| Last updated | 6 August 2026 |
| Product boundary | Release 1 B2B ERP only |
| Current activity | `MESP-91 Correction Package 1 — merged and Done` |
| Current implementation item | `None — MESP-92 is the next eligible correction; no implementation item is active` |
| Merged branch | `fix/MESP-91-verified-work-scope-authority` (baseline `4eb1ef3ab094242cbb26ec9ab79b4037512e0d2d`; deleted after merge) |
| Current Sprint | `No active Sprint — MESP-63 was delivered outside a Sprint` |
| Current review checkpoint | `MESP-91 focused ChatGPT security review APPROVED TO MERGE; PR #20 merged at f2cde57400fed470ab048776e05b56f353b36890; MESP-92/MESP-93/MESP-94/MESP-31 remain To Do` |

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

### Foundation backend slice

The SaaS Platform Administration domain and the approved Foundation backend slice
have progressed further than the rest of the product. MESP-57, MESP-58,
MESP-87, MESP-59, MESP-88, MESP-60, MESP-62 and MESP-89 are complete and merged.
MESP-63, MESP-90, MESP-61 and MESP-64 are complete. Product-wide Phase 2
remains in progress because core ERP BRDs are not complete, and Foundation
backend work is not complete ERP backend implementation.

PR #18 ([link](https://github.com/Hossam1104/mini-erp-saas-platform/pull/18))
merged MESP-64 to `main` at
`2002d1c25d39022b227e89b3d70f41a53de0408c`, which remains the historical
Foundation baseline. MESP-91 Correction Package 1
([PR #20](https://github.com/Hossam1104/mini-erp-saas-platform/pull/20)) was
approved by focused ChatGPT security review and merged to `main` at
`f2cde57400fed470ab048776e05b56f353b36890`. No implementation item or Sprint
is active; MESP-92 is the next eligible correction.

| Area | Current status |
|---|---|
| Product PRD | Done |
| Product architecture baseline | Done |
| MESP-27 Platform Administration BRD | Done |
| MESP-27 implementation backlog | Done |
| MESP-27 Jira synchronization | Done |
| MESP-57 Sprint activation | Done |
| MESP-57 development branch | Done |
| MESP-57 implementation | Done — implemented, reviewed, merged through PR #1, validated on `main`, and closed in Jira |
| Sprint 1 | Done — `S1-Solution Foundation` completed |
| MESP-58 trusted TenantContext and persistence isolation | Done — PR #6 merged; corrective hardening included in final main |
| MESP-87 Tenant persistence guardrail hardening | Done — merged with MESP-58 correction sequence |
| MESP-59 authentication and authorization seam | Done — PR #8 merged and corrected through MESP-88 |
| MESP-88 MESP-59 security correction | Done — PR #9 merged; 161 tests passed |
| MESP-60 REST/OpenAPI foundation | Done — PR #10 merged to `main` |
| MESP-62 immutable audit and OpenTelemetry evidence | Done — merged with the Foundation Backend Review Checkpoint package |
| MESP-89 Foundation host authentication, antiforgery and evidence integration | Done — PR #12 merged at `a1c5627b40e11b14a50736663c6da56cf11c9ef8`; focused ChatGPT review approved; merged-main validation passed with 247 tests |
| MESP-63 Angular Foundation shell | Done — PR #14 merged at `ad9e6a7c40d229b564a7232ca62b3d70ec1fdc15`; Wave 1 shell, session/context integration, bilingual RTL and safe states validated |
| MESP-90 MESP-63 false-logout security correction | Done — PR #16 merged at `469ab863a5fc20f02d3ba674a97dceb969bbec75`; preserves authenticated state until server-confirmed revocation |
| MESP-61 background processing foundation | Done — PR #17 merged at `7db49a88e11232f055c2016b8bb033a61de629ec`; typed Tenant-bound durable work/outbox/inbox, bounded worker, notification contracts and private-file adapter |
| MESP-64 provider/schema/index validation | Done — PR #18 merged at `2002d1c25d39022b227e89b3d70f41a53de0408c`; ADR-018, disposable LocalDB SQL Server harness, exact 75-assertion evidence and merged-main validation complete; no production provider or migration |
| MESP-91 verified work scope and worker authority correction | Done — Correction Package 1; PR #20 merged at `f2cde57400fed470ab048776e05b56f353b36890` after focused ChatGPT approval; Identity-owned organization resolver, authorization-context-bound scopes, live worker/outbox revalidation and safe authority dead-letter are merged to `main` |
| MESP-3 Identity and Access Epic | In Progress |
| MESP-28 Identity and Access BRD | Done — v0.3 Approved Release 1 Baseline (founder change-control approval 3 August 2026) |
| MESP-4 Multi-Tenancy and Tenant Lifecycle Epic | In Progress |
| MESP-29 Multi-Tenancy BRD | Done — v0.2 Approved Release 1 Baseline (2 August 2026) |
| MESP-5 Organization and Company Structure Epic | In Progress |
| MESP-30 Organization BRD | Done — v0.2 Approved Release 1 Baseline (2 August 2026); outside all Sprints |
| Remaining product BRDs | Not Started / To Do |

### Current Jira and Git state

- [x] `MESP-27` — SaaS Platform Administration BRD is **Done**.
- [x] `MESP-2` — Platform Administration Epic remains **In Progress**.
- [x] `MESP-57` — Modular Monolith solution and module seam is **Done**.
- [x] `MESP-3` — Identity and Access Epic remains **In Progress** for the sequential foundation/domain documentation stream.
- [x] `MESP-28` — Identity and Access BRD approved as the v0.2 Release 1 baseline and moved to **Done**.
- [x] `MESP-4` — Multi-Tenancy and Tenant Lifecycle Epic moved to **In Progress**; the Epic continues beyond this BRD.
- [x] `MESP-29` — Multi-Tenancy BRD approved as the v0.2 Release 1 baseline and moved to **Done**; it remains outside all Sprints.
- [x] `MESP-5` — Organization and Company Structure Epic moved to **In Progress**; the Epic continues beyond this BRD.
- [x] `MESP-30` — Organization BRD approved as the v0.2 Release 1 baseline, moved to **Done**, and kept outside all Sprints; approval commit was prepared on `docs/mesp-30-organization-brd` and is verified in the documentation merge to `main`.
- [x] `MESP-86` — Foundation Release 1 Lean Implementation Specification v0.4 approved and moved to **Done** after review/merge evidence; it remains outside implementation Sprints.
- [x] `MESP-1` — Product Governance and BRD Management Epic moved to **In Progress** for the controlled specification activity.
- [x] `MESP-23` — Open Questions Register remains **In Progress** as a living governance register; its current working deliverable is maintained in Jira comments, not as a second BRD/LIS delivery artifact.
- [x] Sprint `S1-Solution Foundation` was created, started, and completed.
- [x] Sprint contained only `MESP-57`.
- [x] `MESP-58`, `MESP-87`, `MESP-59` and `MESP-88` are **Done** with merged implementation/security evidence.
- [x] `MESP-60` is **Done**; PR #10 is merged and merged-main validation passed.
- [x] `MESP-62` is **Done**; the immutable audit/observability seam and checkpoint package are merged.
- [x] `MESP-89` is **Done**; PR #12 merged at `a1c5627b40e11b14a50736663c6da56cf11c9ef8` after focused ChatGPT approval; merged-main validation passed with 247 tests.
- [x] `MESP-63` moved to **In Progress**, implemented and merged through PR #14; it is now **Done** and the sequence advanced to MESP-61, then MESP-64.
- [x] `MESP-90` false-logout correction is **Done**; approved PR #16 merged to `main` at `469ab863a5fc20f02d3ba674a97dceb969bbec75`; MESP-63 remains **Done**.
- [x] `MESP-61` is **Done**; PR #17 merged to `main` at `7db49a88e11232f055c2016b8bb033a61de629ec` and merged-main validation passed.
- [x] `MESP-64` is **Done**; PR #18 merged to `main` at `2002d1c25d39022b227e89b3d70f41a53de0408c`, merged-main validation passed, and its branch was deleted.
- [x] `MESP-91` is **Done**; PR #20 merged at `f2cde57400fed470ab048776e05b56f353b36890` after focused ChatGPT security review approval; branch `fix/MESP-91-verified-work-scope-authority` deleted after merge.
- [x] `MESP-31` through `MESP-40` remain **To Do**; no downstream BRD was started.
- [x] `MESP-92`, `MESP-93` and `MESP-94` remain **To Do**; Correction Package 2/3 work is untouched. MESP-92 is the next eligible correction; it had not started before MESP-91 closure.
- [x] No Sprint is active; MESP-89 and MESP-63 were delivered outside a Sprint.
- [x] MESP-63, MESP-90, MESP-61, MESP-64 and MESP-91 completed sequentially; no implementation item is currently active and no parallel implementation is authorized.
- [x] MESP-86 v0.4 Approved Release 1 Baseline is merged to `main`; implementation refinement is controlled and is not production readiness.
- [x] Product-wide Phase 2 remains **In Progress** because core ERP BRDs remain incomplete; Foundation backend work does not imply complete ERP backend implementation.
- [x] Branch `feat/mesp-57-modular-monolith-seam` was created from `main` and pushed.
- [x] Implement `MESP-57 / TE-01`.
- [x] Run the Release build.
- [x] Run targeted architecture validation.
- [x] Validate API startup and required endpoints.
- [x] Review the MESP-57 implementation.
- [x] Create and review Pull Request #1.
- [x] Merge Pull Request #1 into `main`.
- [x] Run post-merge validation on `main`.
- [x] Move MESP-57 to **Done**.
- [x] Complete `S1-Solution Foundation`.
- [x] Synchronize local `main` with `origin/main`.

---

# 4. Delivery lifecycle and fast-track documentation policy

The product will follow this controlled lifecycle. The former separate DDD, FRS, Data Design, and TDS documents are not mandatory standing deliverables.

1. Product Requirements Document
2. Business Requirements and BPMN
3. One Lean Implementation Specification per approved foundation/domain slice
4. Implementation-ready Jira Backlog
5. Implementation and Automated Testing
6. Integration, UAT, Release, and Operations

A domain must not move into implementation until its BRD/BPMN and applicable Lean Implementation Specification are approved, except for explicitly approved architecture-foundation Enablers such as MESP-57.

## Fast-track documentation policy

- An approved BRD is the business baseline for its domain; do not keep it in a draft state after founder approval.
- For each approved foundation/domain slice, create one Lean Implementation Specification that carries the required domain model/invariants, functional journeys, logical data decisions, authorization/API behavior, targeted tests, and acceptance traceability in one controlled document.
- Do not create separate mandatory DDD, FRS, Data Design, or TDS documents for the same slice. Those concerns are content sections of the Lean Implementation Specification.
- Security, tenant-isolation, MESP-48 supported-volume, and MESP-50 retention/privacy/legal-hold/purge gates remain mandatory and are not bypassed by the fast-track policy.
- Keep documentation activities sequential: one current BRD or Lean Implementation Specification activity at a time; do not start implementation or a later BRD before the current evidence is committed and reviewed.
- A living governance register such as `MESP-23` is maintenance work, not a second active BRD/LIS delivery artifact. It is exempt from the one-active-delivery-artifact rule while no separate drafting task is actively being executed.
- For the founder-authorized fast-track implementation batch, Luna performs bounded implementation, self-review and validation; eligible Pull Requests may merge automatically after all safety gates pass.
- ChatGPT reviews each execution report using the actual Jira, GitHub, diff, build and test evidence. Independent Opus review is reserved for major checkpoints rather than every Jira item. MESP-89 was an explicit security exception: focused ChatGPT review was required before merge, approved the corrected PR, and no additional full Opus review is required for this completed item.

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

Define the business meaning of each domain before technical implementation. Each BRD must capture workflows, actors, rules, exceptions, states, data requirements, reports, approvals, audit evidence, and business acceptance scenarios. A BRD is approved once its founder/business-owner decision record is complete; it is not held open for a second documentation package.

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
- BPMN process diagrams where the workflow needs them.
- Founder-approved decision record and explicit deferred gates.
- Founder/business-owner approval.

## Domain sequence and progress

### Foundation and governance

- [x] Product foundation and governance preparation.
- [x] Jira simplification and delivery-governance setup.
- [x] `MESP-27` — SaaS Platform Administration BRD.
- [x] MESP-27 founder approval.
- [x] MESP-27 Wave 1 wireframes and Layout B baseline.
- [ ] BPMN diagrams for all MESP-27 workflows.
- [x] `MESP-28` — Identity and Access BRD — **Done: v0.3 Approved Release 1 Baseline** (founder change-control approval 3 August 2026).
- [x] `MESP-29` — Multi-Tenancy BRD — **Done: v0.2 Approved Release 1 Baseline** (2 August 2026).
- [x] `MESP-30` — Organization BRD — **Done: v0.2 Approved Release 1 Baseline** (2 August 2026).

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

- [ ] Applicable BRD approved with a recorded founder/business-owner decision record.
- [ ] BPMN diagrams approved where they are required to remove material workflow ambiguity.
- [ ] All critical business rules are explicit.
- [ ] State transitions are unambiguous.
- [ ] Exceptions and negative paths are covered.
- [ ] Permissions and separation-of-duties rules are approved.
- [x] Deferred decisions have owners and explicit implementation or production gates.
- [ ] No technical behavior is invented to fill a business gap.

MESP-28, MESP-29 and MESP-30 are the approved BRD baselines for the current
foundation slice. MESP-31 through MESP-40 remain To Do and are not implied to be
approved by the MESP-28 approval; each requires its own controlled BRD decision.

**Phase 2 status: IN PROGRESS — MESP-28 v0.3, MESP-29 v0.2, and MESP-30 v0.2 are approved and Done; MESP-5 remains In Progress. MESP-31 through MESP-40 remain To Do and require separate approval. MESP-86 v0.4 is approved and Done; no remaining BRD is implicitly approved by that decision.**

---

# Phase 3 — Lean Implementation Specification: domain and behavior

## Objective

Capture the domain-model and behavior sections of one Lean Implementation Specification for an approved foundation/domain slice. This is not a separate DDD deliverable.

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
- Deferred design decisions and explicit production gates.

## Planned execution

For each approved BRD, create or update one Lean Implementation Specification:

1. Extract nouns, roles, documents, states, and business events.
2. Confirm the owning Bounded Context.
3. Define Aggregate Roots and consistency boundaries.
4. Define entities and immutable Value Objects.
5. Define domain invariants.
6. Define domain events for completed business facts.
7. Define allowed dependencies on other contexts.
8. Validate the model against every BRD workflow and exception.
9. Update the Business Glossary.
10. Obtain approval for the complete Lean Implementation Specification before implementation refinement.

## Progress

- [x] High-level module boundaries defined in the architecture baseline.
- [x] Platform Administration identified as a distinct module boundary.
- [ ] Platform Administration Lean Implementation Specification.
- [ ] Identity and Access Lean Implementation Specification.
- [ ] Multi-Tenancy Lean Implementation Specification.
- [ ] Organization Lean Implementation Specification.
- [ ] Remaining domain Lean Implementation Specifications.

## Exit criteria per domain

- [ ] Every business concept has one owning context.
- [ ] Aggregate boundaries are explicit.
- [ ] Business invariants are enforceable.
- [ ] Cross-context dependencies are approved.
- [ ] No shared business-model dumping ground exists.
- [ ] The model covers all BRD workflows and exceptions.

**Phase 3 status: IN PROGRESS — MESP-86 v0.4 is the approved combined lean domain/behavior baseline for MESP-28/29/30; implementation refinement is controlled and begins only with MESP-58.**

---

# Phase 4 — Lean Implementation Specification: user journeys and acceptance

## Objective

Capture user journeys, screens, actions, inputs, outputs, states, validations, permissions, errors, reports, localization, and functional acceptance criteria as sections of the same Lean Implementation Specification. No standalone FRS document is required.

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
- [ ] MESP-27 user-journey and acceptance sections in its Lean Implementation Specification.
- [ ] Identity and Access user-journey and acceptance sections.
- [ ] Multi-Tenancy user-journey and acceptance sections.
- [ ] Organization user-journey and acceptance sections.
- [ ] Remaining domain user-journey and acceptance sections.

## Exit criteria per domain

- [ ] Every BRD capability maps to one or more functions.
- [ ] User-visible behavior is unambiguous.
- [ ] Validation and state behavior are defined.
- [ ] Error and restricted states are covered.
- [ ] Localization and accessibility expectations are included.
- [ ] Functional acceptance criteria are testable.

**Phase 4 status: IN PROGRESS — MESP-86 v0.4 contains the approved combined functional journeys, route/state inventory and acceptance coverage for MESP-28/29/30; no standalone FRS documents are planned.**

---

# Phase 5 — Lean Implementation Specification: logical data and integrity

## Objective

Capture the logical data and integrity decisions required to support approved domain behavior, tenant isolation, business integrity, performance, auditability, retention, and reporting in the same Lean Implementation Specification. Physical implementation detail is added only when the slice is ready; no standalone Data Design document is required.

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

**Phase 5 status: IN PROGRESS — MESP-86 v0.4 contains the approved logical Foundation data model, ERD and tenant-aware integrity design; the implemented persistence, immutable audit seam and MESP-64 SQL Server provider/schema/index/collation/rowversion validation remain bounded foundation seams. Physical migrations remain excluded, and detailed physical ERDs are required before implementing each future ERP domain. MESP-50 remains a production gate.**

---

# Phase 6 — Lean Implementation Specification: implementation readiness

## Objective

Capture the implementation-readiness decisions for the approved slice across solution boundaries, APIs, database, authorization, integrations, background processing, observability, security, and deployment in the same Lean Implementation Specification. No standalone TDS document is required.

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
- Traceability to the approved BRD/BPMN and the relevant Lean Implementation Specification sections.

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
- [ ] Identity and Access Lean Implementation Specification implementation-readiness sections.
- [ ] Multi-Tenancy Lean Implementation Specification implementation-readiness sections.
- [ ] Organization Lean Implementation Specification implementation-readiness sections.
- [ ] Detailed domain API and database sections inside each Lean Implementation Specification.
- [ ] Detailed production deployment design.

### Current MESP-57 technical foundation

- [x] MESP-57 implementation scope approved.
- [x] MESP-57 Demonstration Outcome defined.
- [x] MESP-57 Definition of Done defined.
- [x] MESP-57 branch created.
- [x] MESP-57 Sprint activated.
- [x] Implement the three-project Modular Monolith seam.
- [x] Add targeted architecture validation.
- [x] Build and demonstrate the API host/module-registration seam.
- [x] Review the MESP-57 implementation.
- [x] Merge MESP-57 through Pull Request #1.
- [x] Validate the merged result on `main`.
- [x] Move MESP-57 to **Done**.
- [x] Complete Sprint 1.

## Exit criteria per domain

- [ ] Detailed design covers all approved functions.
- [ ] API, data, authorization, and transaction behavior are explicit.
- [ ] Security and tenant-isolation controls are reviewable.
- [ ] Failure and recovery paths are defined.
- [ ] Observability and operational requirements are included.
- [ ] Technical risks and deferred decisions are recorded.
- [ ] No implementation depends on an unresolved critical decision.

**Phase 6 status: IN PROGRESS — MESP-86 v0.4 contains the approved authorization, API, persistence, security, observability and slicing design. MESP-58, MESP-87, MESP-59, MESP-88, MESP-60, MESP-62, MESP-89, MESP-63, MESP-90, MESP-61 and MESP-64 are implemented as bounded Foundation seams; MESP-89 merged the catalog-backed exact operation authorization, mandatory protected-write evidence, composite idempotency replay and separate eligibility/selection versions. Production provider validation remains separately gated.**

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
- [x] MESP-58 through MESP-64 descriptions refined against MESP-86 v0.4; the founder-authorized fast-track sequence is MESP-60 followed by MESP-62, with the later items gated by the checkpoint.
- [x] Sprint `S1-Solution Foundation` created with MESP-57 only.
- [ ] Refine each future Enabler or Story immediately before implementation.
- [ ] Populate native estimation fields when Jira configuration supports them.
- [ ] Create Bugs only from verified defects.
- [ ] Keep future items out of active Sprints until Definition of Ready is met.

## Definition of Ready

A future Story or Enabler may enter a Sprint only when:

- [ ] Applicable BRD is approved.
- [ ] BPMN workflow is approved where required.
- [ ] Lean Implementation Specification for the slice is approved, including domain ownership, journeys, data decisions, authorization, and technical readiness.
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
    12. Review the complete task-related diff, run `git diff --check`, and correct every finding.
    13. Add implementation evidence to Jira.
    14. Create a non-draft Pull Request.
    15. Automatically merge only after base/head, build, tests, security, scope and repository gates pass; use a normal merge commit. MESP-89 was the completed security exception and required focused ChatGPT approval before its merge.
    16. Run post-merge validation on synchronized `main`.
    17. Demonstrate the outcome and update the delivery documentation.
    18. Move the Jira item to Done.
    19. Delete the completed local and remote branch.
    20. Select the next explicitly authorized item only.

## Current implementation sequence

### Sprint 1 — Solution Foundation

- [x] Sprint created.
- [x] Sprint started.
- [x] MESP-57 added as the only Sprint item.
- [x] MESP-2 moved to In Progress.
- [x] MESP-57 moved to In Progress.
- [x] Development branch created and published.
- [x] Implement MESP-57.
- [x] Run the MESP-57 Release build and architecture validation.
- [x] Review the MESP-57 code and evidence.
- [x] Create MESP-57 Pull Request #1.
- [x] Complete final review of Pull Request #1.
- [x] Merge Pull Request #1.
- [x] Validate `main` after merge.
- [x] Move MESP-57 to Done.
- [x] Complete Sprint 1.

### Founder-authorized fast-track Foundation batch (no Sprint)

- [x] No Sprint is required or active for this controlled batch.
- [x] `MESP-57`, `MESP-58`, `MESP-87`, `MESP-59` and `MESP-88` are complete and merged.
- [x] `MESP-60` is Done; PR #10 merged at `2569acbe6dc26223108f7ad539ca7db2bcdf5f93` and merged-main validation passed.
- [x] `MESP-62` is Done; immutable audit/observability evidence and the checkpoint package are complete and merged.
- [x] `MESP-89` is Done; PR #12 merged at `a1c5627b40e11b14a50736663c6da56cf11c9ef8` after focused ChatGPT approval and 247-test merged-main validation.
- [x] `MESP-63` moved to In Progress, completed its bounded Angular Wave 1 implementation, and merged through PR #14 at `ad9e6a7c40d229b564a7232ca62b3d70ec1fdc15`.
- [x] `MESP-61` is **Done**; PR #17 merged to `main` at
  `7db49a88e11232f055c2016b8bb033a61de629ec` and merged-main validation passed.
- [x] `MESP-64` is **Done**; PR #18 merged to `main` at
  `2002d1c25d39022b227e89b3d70f41a53de0408c`, merged-main validation passed,
  and no implementation item is active.

### Current sequence after the approved Foundation specification

The product-wide Phase 2 BRD stream remains in progress because core ERP BRDs
remain incomplete. The Foundation slice has completed the approved MESP-63
frontend baseline, MESP-61 durable-work foundation and the MESP-91 Correction
Package 1 authority hardening; that does not mean complete ERP backend
implementation has started. MESP-91 is Done and merged; no implementation item
or Sprint is active. Do not start MESP-31, MESP-92, MESP-93, MESP-94 or any
downstream ERP transaction work without a separate authorized decision.

- [x] Approve `MESP-28` Identity and Access BRD v0.3 change-control baseline on `docs/foundation-release1-lean-spec`.
- [x] Begin and complete `MESP-29` Multi-Tenancy BRD as the single requirements activity; v0.2 is approved and Done.
- [x] Merge the approved MESP-29 documentation to `main`; no implementation item or Sprint was created.
- [x] Begin and complete `MESP-30` Organization BRD; approve v0.2 Release 1 Baseline on `docs/mesp-30-organization-brd`.
- [x] Resolve and record `ORG-OD-001` through `ORG-OD-007`; move `MESP-30` to Done after approval evidence.
- [x] Produce and approve v0.4 of the combined Foundation Release 1 Lean Implementation Specification under MESP-86; merge the approved documentation to `main`.
- [x] Refine MESP-58 through MESP-64 against v0.4 and complete the Definition of Ready checks for the Foundation sequence.
- [x] Complete MESP-58, MESP-87, MESP-59 and MESP-88 with merged validation evidence.
- [x] Complete MESP-60 REST/OpenAPI and safe operation contracts.
- [x] Complete MESP-62 immutable audit and OpenTelemetry evidence.
- [x] Complete `MESP-89` host authentication, antiforgery, catalog-backed exact permissions, trusted context, mandatory protected-write evidence, composite idempotency replay and separate context eligibility/selection versions; PR #12 merged after focused ChatGPT approval.
- [x] Start `MESP-63` Angular Foundation shell sequentially after the MESP-89 Jira closure and reconciliation evidence.

### MESP-63 completed delivery evidence

- [x] Jira MESP-63 moved to **In Progress**, completed, and has its implementation evidence recorded; MESP-61 and MESP-64 are **Done**.
- [x] Branch `feature/mesp-63-angular-wave-1-shell-rtl` was created from synchronized `main`; implementation commits `798d15d1aa1e53781df3a2683305e95ac3143890` and `46bf2d30f91ef00e9e450b59b8de0b3a2d34dbab` were merged through PR #14 at `ad9e6a7c40d229b564a7232ca62b3d70ec1fdc15`.
- [x] Angular 22/TypeScript standalone workspace and modular `core`, `features`, and `shared` structure created.
- [x] EN/AR translations, runtime direction switching, responsive shell, navigation, header, context rail and accessible focus baseline implemented.
- [x] Session bootstrap, secure-cookie `withCredentials`, in-memory antiforgery bootstrap, safe expiry handling and server-confirmed context switching implemented against the merged `/api/v1/auth/*` contracts.
- [x] Loading, empty, restricted, denied, expired-session and safe-error presentation states included; no token or Tenant authority is stored in browser storage.
- [x] Focused Angular tests pass (8/8); mocked Playwright TypeScript Wave 1 smoke journey passes (1/1); production/shared provider and database work is excluded.
- [x] Review the complete diff, publish the non-draft Pull Request #14, merge after all gates passed, validate merged `main`, record Jira closure evidence and delete the completed branch.

### MESP-90 — MESP-63 false-logout security correction (completed)

- [x] Jira correction task `MESP-90` is **Done**, relates to MESP-63 and unblocked MESP-61; MESP-63 remains **Done**.
- [x] Branch `fix/mesp-63-signout-fail-closed` was created from the verified `main` baseline `7efb9e76e3bd12d8c97a48cb882efd238ea93373`.
- [x] AuthService now distinguishes confirmed sign-out, server-confirmed already-invalid sessions and unconfirmed outcomes; it does not clear local state or navigate to login after antiforgery, audit, server, malformed-response or network failure.
- [x] Cached antiforgery material is cleared after a 403, no sign-out POST is sent without a non-empty in-memory token, concurrent sign-outs coalesce, and stale responses cannot overwrite newer authentication state.
- [x] The shell keeps the selected context visible, exposes an accessible EN/AR retry message and disables the action only while the request is active; no token or cookie authority is stored in browser storage.
- [x] Correction validation currently passes 27 Angular unit/component tests and 4 Playwright journeys; backend source and contract remain unchanged and the 247-test/0-warning/0-error backend baseline remains required.
- [x] Publish non-draft correction PR #16 ([link](https://github.com/Hossam1104/mini-erp-saas-platform/pull/16)); focused ChatGPT review approved the exact head and the PR merged by normal merge at `469ab863a5fc20f02d3ba674a97dceb969bbec75`.
- [x] Validate merged `main`, post Jira closure evidence, move `MESP-90` to Done and delete the completed implementation branch; MESP-61 was started only after this closure.

### MESP-61 — Durable work, notification, and private-file adapters (completed)

- [x] Confirm MESP-90 is Done on merged `main` at
  `469ab863a5fc20f02d3ba674a97dceb969bbec75`; keep MESP-63 Done and advance
  to MESP-64 only after MESP-61 closure.
- [x] Move MESP-61 to **In Progress** only after the MESP-90 closure comment;
  no Sprint is active.
- [x] Review ADR timing and author ADR-006, ADR-007, ADR-008 and ADR-009.
- [x] Add typed Tenant-bound durable-work identity, scope, initiator,
  lifecycle, lease, retry, dead-letter and optimistic-concurrency contracts.
- [x] Add the bounded local relational outbox/inbox adapter, typed dispatcher
  and one-item worker seam; no global Tenant business query path exists.
- [x] Add provider-neutral notification intent/delivery contracts with a
  deterministic local adapter that stores no contact data.
- [x] Add private-file metadata/access contracts and a deterministic local
  adapter with exact Tenant ownership and checksum/concurrency checks; expiry
  remains metadata only and no physical purge exists.
- [x] Add focused tests for Tenant isolation, single-effect dispatch, retry,
  lease ownership, safe audit, notifications and private files; merged-main
  validation remains a closure gate.
- [x] Complete the full diff/self-review, publish and merge PR #17 after all
  required tests and security gates passed, validate merged `main`, post Jira
  closure evidence, move MESP-61 to Done and delete the branch.

MESP-48 supported-volume/performance evidence and MESP-50 retention, privacy,
legal-hold, purge, residency, backup and restoration decisions remain gates;
MESP-61 does not select production providers or execute purge.

### MESP-64 — Foundation safety harness and SQL Server validation (completed)

- [x] Confirm MESP-61 is Done on merged `main` at
  `7db49a88e11232f055c2016b8bb033a61de629ec`; no Sprint is active.
- [x] Author ADR-018 before environment setup. The current machine uses the
  installed SQL Server LocalDB `MSSQLLocalDB`, a unique disposable
  `MiniErpFoundation_*` database, Windows integrated authentication and fixture
  cleanup. Docker/Testcontainers CI compatibility remains deferred.
- [x] Add provider-specific SQL Server tests for the Tenant filter, stored-owner
  update/delete guard, Tenant-aware unique index, rowversion concurrency,
  schema/index metadata, collation/Arabic Unicode, transaction atomicity,
  Tenant-scoped idempotency and single-owner lease claims.
- [x] Add the exact 75-assertion report at
  `docs/96_Foundation_Release1_Safety_Validation.md`; record 53 PASS, 21 NOT
  APPLICABLE scope-boundary rows and one MESP-48/MESP-50 DEFERRED production gate.
- [x] Run the complete diff/self-review, publish and merge PR #18 after
  targeted SQL, backend/frontend regression, security and scope gates passed;
  validate merged `main`, post Jira evidence, move MESP-64 to Done and delete
  the branch.

MESP-48 and MESP-50 remain production gates; MESP-64 did not select a
production provider, create a migration, execute purge or authorize later ERP
work.

### MESP-91 — Verified work scope and worker authority correction (Done, Package 1)

- [x] Confirm MESP-91 is the sole active implementation item; move it to
  **In Progress** and keep MESP-31, MESP-92, MESP-93 and MESP-94 **To Do**.
- [x] Create `fix/MESP-91-verified-work-scope-authority` from the verified
  baseline `4eb1ef3ab094242cbb26ec9ab79b4037512e0d2d`; keep no Sprint active.
- [x] Add the narrow Identity-owned organization resolver for an untrusted
  scope request, exact Tenant -> Company -> Branch -> Warehouse ownership,
  downward containment and authorization-context-bound verified scopes.
- [x] Add live worker and outbox authority revalidation immediately before
  handler/effect dispatch, covering current User/session, authorization path,
  Membership or SupportGrant/SupportCase, exact Permission, scope and
  ownership.
- [x] Terminally dead-letter authority failures with safe
  `AuthorizationDenied` evidence and never call the handler or protected
  outbox effect after a failed check.
- [x] Close H91-03: require canonical explicit ordinary `Kind:GUID` scope,
  reject missing/malformed/marker/broader/sibling scope, and use the current
  case-bound stored SupportGrant scope as the only SupportGrant authority.
- [x] Close H91-04: use one exact binding for WorkItemId, Tenant, operation,
  correlation, Company/Branch/Warehouse boundary, execution TenantContext,
  path, Membership/SupportGrant, actor and session; defensively recheck it
  before handler/outbox execution.
- [x] Add the Identity-only structural issuer allow-list and make mandatory
  security evidence an operation-descriptor requirement enforced at creation,
  handler registration, dispatch and live revalidation.
- [x] Add focused organization-boundary, lifecycle, permission, support-path,
  dead-letter and no-effect regression tests; reconcile the safety catalogue
  and ADR/checkpoint/current-state documentation without changing MESP-48 or
  MESP-50 gates.
- [x] Complete full validation and review the complete task-related diff:
  focused durable-work 102/102, backend 360/360 including SQL 11/11, Angular
  27/27, Playwright 4/4, Release build 0 warnings/0 errors and production
  audit 0 vulnerabilities.
- [x] Commit and push the correction branch and update the existing non-draft
  PR #20 for focused ChatGPT security review.
- [x] Focused ChatGPT security review returned APPROVED TO MERGE (0 Critical,
  0 High, 0 Medium blockers); verify the approved head, rerun final
  validation, and merge PR #20 by normal merge commit at
  `f2cde57400fed470ab048776e05b56f353b36890`.
- [x] Validate merged `main`, reconcile documentation to the merged state,
  post Jira closure evidence, move MESP-91 to Done and delete the completed
  branch. Do not start MESP-31, MESP-92, MESP-93, MESP-94 or another
  implementation item in this context.

### Foundation Completion Opus 5 checkpoint (documentation-only)

- [x] Confirm MESP-90, MESP-61 and MESP-64 are Done on merged `main` and no
  implementation item or Sprint is active.
- [x] Create `docs/97_Foundation_Completion_Review_Checkpoint.md` with the
  complete Foundation sequence, traceability, capabilities, maturity
  boundaries, remaining gates and Opus 5 questions.
- [ ] Submit the documentation-only checkpoint for Opus 5 review; do not start
  MESP-31, Master Data/Catalog work, MESP-48/MESP-50 implementation or any
  other Jira item before review disposition.

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

**Phase 8 status: MESP-57, MESP-58, MESP-87, MESP-59, MESP-88, MESP-60, MESP-62,
MESP-89, MESP-63, MESP-90, MESP-61, MESP-64 and MESP-91 are Done on the
merged-main Foundation baseline. MESP-91 Correction Package 1 merged through
PR #20 at `f2cde57400fed470ab048776e05b56f353b36890` after focused ChatGPT
security review approval. No implementation item or Sprint is active;
MESP-92 is the next eligible correction, and MESP-93/MESP-94/MESP-31 remain
To Do.**

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
→ Lean Implementation Specification (domain model, journey, data, API, security, and test sections)
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
- Lean Implementation Specification drafting.
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

- [x] Approve `MESP-28 — Identity and Access BRD` v0.3 as the Release 1 baseline change-control update.
- [x] Approve `MESP-29 — Multi-Tenancy BRD` v0.2 as the Release 1 baseline, including the four Tenant-isolation clarifications; move MESP-29 to Done.
- [x] Merge the two approved MESP-29 documentation files to `main`; keep MESP-4 In Progress.
- [x] Complete `MESP-30 — Organization BRD` as the sequential requirements activity; v0.2 is Approved Release 1 Baseline and MESP-30 is Done outside all Sprints.
- [x] Approve and merge the v0.4 combined Foundation Release 1 Lean Implementation Specification; no application implementation started in this correction cycle.
- [x] Refine MESP-58 through MESP-64 against the approved Foundation sequence; no Sprint is required for the founder-authorized fast-track batch.
- [x] Reconcile MESP-59 and close its completed implementation/security correction sequence before starting the next implementation item.
- [x] Complete MESP-60 REST/OpenAPI foundation implementation and validation; PR #10 merged at `2569acbe6dc26223108f7ad539ca7db2bcdf5f93`.
- [x] Complete MESP-62 immutable audit and OpenTelemetry evidence; its checkpoint package is included in the merged delivery.
- [x] Complete MESP-89 host authentication, antiforgery, trusted context and evidence integration; PR #12 merged at `a1c5627b40e11b14a50736663c6da56cf11c9ef8` and MESP-89 is Done after focused ChatGPT approval.
- [x] Close the MESP-89 Jira/reconciliation record, complete the authorized MESP-63 Angular Foundation shell, and keep MESP-61 and MESP-64 outside parallel execution until the MESP-90 sequential gate was met.
- [x] Implement, merge and close MESP-63 through PR #14 on `feature/mesp-63-angular-wave-1-shell-rtl`; no Sprint is active.
- [x] Merge and close MESP-90 through PR #16 at `469ab863a5fc20f02d3ba674a97dceb969bbec75` after focused ChatGPT approval.
- [x] Implement and close MESP-61 on `feature/mesp-61-durable-work-private-files`; PR #17 merged at `7db49a88e11232f055c2016b8bb033a61de629ec` and the ADR index/Foundation evidence were updated for the bounded scope.
- [x] Implement and close MESP-64 on `feature/mesp-64-foundation-safety-harness`; PR #18 merged at `2002d1c25d39022b227e89b3d70f41a53de0408c`, ADR-018 and the exact 75-assertion safety evidence were updated without production/provider or later ERP scope.
- [x] Prepare the documentation-only Foundation Completion Opus 5 checkpoint at `docs/97_Foundation_Completion_Review_Checkpoint.md`; stop before MESP-31 or any later implementation.

### MESP-91 current correction activity (Done)

- [x] Move Jira MESP-91 to **In Progress** after confirming no other
  implementation item or Sprint is active.
- [x] Implement the verified organization-scope resolver, context binding,
  authorized-scope containment and current worker/outbox authority
  revalidation in Correction Package 1.
- [x] Add focused regression tests and reconcile `docs/96`, `docs/97`,
  `docs/ADR-008_SQL_Background_Workers_and_Ownership.md` and `.ai/CURRENT_STATE.md`.
- [x] Complete final validation: focused durable-work 102/102, backend
  360/360 including SQL 11/11, Angular 27/27, Playwright 4/4, Release build 0
  warnings/0 errors and production audit 0 vulnerabilities.
- [x] Commit/push, publish the non-draft PR #20 for focused ChatGPT security
  review.
- [x] Focused ChatGPT security review returned APPROVED TO MERGE; merge PR #20
  by normal merge commit at `f2cde57400fed470ab048776e05b56f353b36890`,
  validate merged `main`, reconcile documentation, post Jira closure evidence,
  move MESP-91 to Done and delete the branch.

## Completed MESP-57 outputs

- [x] `backend/MiniErp.sln`.
- [x] `backend/Directory.Build.props`.
- [x] `backend/Directory.Packages.props`.
- [x] `backend/src/MiniErp.Api`.
- [x] `backend/src/MiniErp.App`.
- [x] `backend/src/MiniErp.Contracts`.
- [x] `backend/tests/MiniErp.ArchitectureTests`.
- [x] Platform Administration module registration seam.
- [x] Public contracts separated from internal module code.
- [x] Minimal API startup evidence.
- [x] Targeted architecture validation.
- [x] Local build instructions.
- [x] Successful restore and Release build.
- [x] Six architecture tests passed.
- [x] `/health` validated successfully.
- [x] `/api/v1/module-registration` validated successfully.
- [x] Implementation commit `de6578f` created and pushed.
- [x] Pull Request #1 created, reviewed, and merged.
- [x] Merge commit `47be691cfbe4946139dcd55e55f5cbb1b86e257d` validated on `main`.
- [x] Jira evidence added.
- [x] MESP-57 moved to Done.
- [x] Sprint 1 completed.

## Closure confirmation

- [x] The full MESP-57 diff was reviewed.
- [x] The project dependency graph was verified.
- [x] No future-Enabler scope was implemented.
- [x] Build and architecture-validation evidence was verified.
- [x] No secret was identified in the reviewed change; historical generated IDE files under `.vs` remain tracked in the repository. Their cleanup is a separate bounded repository-hygiene task and is not performed here.
- [x] No MESP-28 behavior was implemented.
- [x] No persistence, authentication, authorization, tenant-isolation, downstream ERP transaction, or Retail POS scope was introduced.
- [x] `main` is synchronized with `origin/main`.

## Next requirements gate

- [x] Review the approved scope and source documents for `MESP-28`.
- [x] Produce and approve the Identity and Access BRD v0.2 without starting implementation.
- [x] Resolve the 22 historical IAM-OD records plus IAM-OD-023 (23 total) and four source-conflict records in the approved baseline.
- [x] Begin, approve, and close the single MESP-29 Multi-Tenancy BRD activity; v0.2 is merged to `main`.
- [x] Record the four Tenant-isolation clarifications and preserve MESP-48/MESP-50 Deferred Gates.
- [x] Keep future implementation items outside the active sequence in To Do; MESP-58, MESP-87, MESP-59, MESP-88, MESP-60, MESP-62, MESP-89, MESP-90, MESP-61, MESP-64 and MESP-91 are complete; no implementation item is currently active.
- [x] Keep `MESP-31` through `MESP-40` in To Do while the approved foundation requirements remain the current delivery boundary.
- [x] Keep `MESP-30` outside all Sprints and restrict it to business requirements only.
- [x] Complete founder approval of the MESP-30 baseline and resolve `ORG-OD-001` through `ORG-OD-007`.
- [x] Start, approve, and merge `MESP-86` v0.4 combined Foundation Release 1 Lean Implementation Specification on `docs/foundation-release1-lean-spec`; keep it outside all implementation Sprints.
- [x] Refine MESP-58 through MESP-64 and complete the MESP-58 Definition of Ready review.
- [x] No Sprint is required for the founder-authorized MESP-60/MESP-62 fast-track batch.
- [x] Start and complete MESP-62 only after MESP-60 was merged, validated and moved to Done; no Sprint was created.
- [x] Complete focused ChatGPT review of the MESP-89 PR, merge PR #12, validate merged `main`, and record the MESP-89 Done state; MESP-63 and MESP-90 subsequently completed sequentially.
- [x] Complete focused ChatGPT review of MESP-90 PR #16, merge the exact approved head, validate merged `main`, post Jira closure evidence and move MESP-90 to Done.
- [x] Complete the MESP-61 durable-work/private-file implementation and merged-main validation before starting MESP-64.
- [x] Complete the MESP-64 foundation safety harness PR, merged-main validation and Jira closure before creating the documentation-only Foundation Completion Opus 5 checkpoint.
- [ ] Obtain Opus 5 disposition of the complete Foundation checkpoint before starting Master Data and Catalog or any other core ERP BRD/implementation item.

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
| 1 August 2026 | MESP-57 implementation completed | Done | Commit `de6578f` created and pushed; Release build and 6 architecture tests passed |
| 1 August 2026 | Product Delivery Master Plan added | Done | Added in commit `d86e00f` as an approved project-governance addition |
| 1 August 2026 | Delivery plan aligned with review state | Done | Commit `c547f3c` |
| 1 August 2026 | Pull Request #1 merged | Done | Merge commit `47be691cfbe4946139dcd55e55f5cbb1b86e257d` |
| 2 August 2026 | Post-merge validation on `main` | Done | Restore and Release build passed; 6 architecture tests passed; required endpoints validated |
| 2 August 2026 | MESP-57 closed | Done | Jira status moved to Done |
| 2 August 2026 | Sprint 1 completed | Done | `S1-Solution Foundation` closed with MESP-57 completed |
| 2 August 2026 | MESP-28 BRD approved | Done | `docs/12_Identity_and_Access_BRD.md` v0.2 Approved Release 1 Baseline; original baseline approval, no implementation Sprint or Jira implementation work started |
| 2 August 2026 | MESP-29 BRD approved | Done | `docs/13_Multi_Tenancy_BRD.md` v0.2 Approved Release 1 Baseline; four Tenant-isolation clarifications incorporated; MESP-4 remains In Progress; no Sprint or implementation item started; approval commit merged to `main` |
| 2 August 2026 | MESP-30 BRD approved | Done | `docs/14_Organization_and_Company_Structure_BRD.md` v0.2 Approved Release 1 Baseline; ORG-OD-001 through ORG-OD-007 resolved; approval merged to `main` at `a1e5eb439bf6723efb5f0638cfc518ad044fce86`; no implementation started |
| 2 August 2026 | MESP-86 foundation specification started | In Progress | Governance Task under MESP-1; design/documentation only; no Sprint or implementation item |
| 3 August 2026 | MESP-28 IAM change-control and MESP-86 approval | Done | IAM BRD v0.3 records the founder global User/Membership decision; MESP-86 v0.4 approved and merged; no application code or implementation started |
| 3 August 2026 | MESP-58 trusted TenantContext and persistence isolation | Done | PR #6 merged; security correction and merged-main validation completed |
| 3 August 2026 | MESP-87 Tenant persistence guardrail hardening | Done | Completed in the MESP-58 correction sequence; cross-Tenant Modified/Deleted protections validated |
| 3 August 2026 | MESP-59 authentication and authorization seam | Done | PR #8 merged; status reconciled after MESP-88/PR #9 evidence; Jira reconciliation comment 10274 |
| 3 August 2026 | MESP-88 MESP-59 security correction | Done | PR #9 merged at `723dc8e28b0a927750230b51b9d05e26d039038c`; 161 tests passed |
| 3 August 2026 | MESP-60 REST/OpenAPI foundation | Done | PR #10 merged at `2569acbe6dc26223108f7ad539ca7db2bcdf5f93`; versioned contracts, trusted context, safe errors, correlation, idempotency, concurrency and antiforgery seam validated with 188 tests |
| 3 August 2026 | MESP-62 immutable audit and OpenTelemetry evidence | Done | Immutable path-aware evidence, append-before-effect coordinator, safe telemetry hooks and focused tests merged; checkpoint package included in the PR |
| 4 August 2026 | Foundation Backend Review Checkpoint reconciled | Done | `docs/95_Foundation_Backend_Review_Checkpoint.md`; PR #12 merged and MESP-63 authorized next after focused ChatGPT approval |
| 4 August 2026 | MESP-89 foundation host security integration | Done | PR #12 merged at `a1c5627b40e11b14a50736663c6da56cf11c9ef8` after focused ChatGPT approval; ADR-004 reconciled for catalog-backed exact permissions, mandatory protected-write evidence, composite idempotency replay and separate eligibility/selection versions; merged-main validation passed with 247 tests and a 0-warning/0-error Release build; MESP-63 is authorized next |
| 4 August 2026 | MESP-63 Angular Wave 1 shell | Done | Commits `798d15d1aa1e53781df3a2683305e95ac3143890` and `46bf2d30f91ef00e9e450b59b8de0b3a2d34dbab` merged through PR #14 at `ad9e6a7c40d229b564a7232ca62b3d70ec1fdc15`; Angular 22 shell, EN/AR RTL foundation, server session/antiforgery/context integration, safe states and accessibility baseline implemented; focused Angular tests 8/8, mocked Playwright smoke 1/1, backend regression 247/0/0 and Release build 0/0 passed; no Sprint active |
| 4 August 2026 | MESP-90 false-logout correction | Done | PR #16 merged by normal merge at `469ab863a5fc20f02d3ba674a97dceb969bbec75`; approved head preserved; 27 Angular tests, 4 Playwright journeys, backend 247-test regression, Release build and production dependency audit passed; MESP-63 remains Done and MESP-61 started only after closure |
| 4 August 2026 | MESP-61 durable work foundation | Done | PR #17 merged at `7db49a88e11232f055c2016b8bb033a61de629ec`; typed Tenant-bound work/outbox/inbox/worker, notification and private-file contracts; backend 285/0/0, Angular 27/0/0, Playwright 4/0/0 and production audit passed |
| 4 August 2026 | MESP-64 foundation safety harness | Done | PR #18 merged at `2002d1c25d39022b227e89b3d70f41a53de0408c`; ADR-018, disposable LocalDB SQL Server probes and exact 75-assertion report; targeted SQL 11/11, backend 296/0/0, Angular 27, Playwright 4 and production audit 0 vulnerabilities passed |
| 4 August 2026 | Foundation Completion Opus 5 checkpoint | Ready for review | Documentation-only `docs/97_Foundation_Completion_Review_Checkpoint.md`; no implementation item or Sprint active; MESP-48 and MESP-50 remain production gates |
| 6 August 2026 | MESP-91 Correction Package 1 merged | Done | Focused ChatGPT security review APPROVED TO MERGE (0 Critical/0 High/0 Medium blockers); PR #20 merged by normal merge commit at `f2cde57400fed470ab048776e05b56f353b36890`; merged-main validation passed 102/102 focused durable-work, 360/360 backend including 11/11 SQL Server LocalDB, 27/27 Angular, 4/4 Playwright, Release build 0 warnings/0 errors, production audit 0 vulnerabilities; implementation branch deleted; MESP-92 is the next eligible correction |

---

# 11. Change-control rules

- Update this file after each approved milestone.
- Do not mark an item Done based only on a model report.
- Mark Done only after checking the actual Jira state, repository change, generated artifact, or approval evidence.
- Do not change completed product decisions without an explicit decision record.
- Do not silently reorder domain dependencies.
- Do not create implementation scope from unresolved BRD, BPMN, or Lean Implementation Specification gaps.
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
| Phase 3 — Lean Implementation Specification: domain and behavior | **In Progress — MESP-86 v0.4 approved combined lean domain/behavior baseline; implementation refinement is limited to the approved Foundation sequence** |
| Phase 4 — Lean Implementation Specification: user journeys | **In Progress — MESP-86 v0.4 approved journeys, states and acceptance baseline** |
| Phase 5 — Lean Implementation Specification: logical data | **In Progress — MESP-86 v0.4 approved logical model, ERD and integrity baseline; physical design remains gated** |
| Phase 6 — Lean Implementation Specification: implementation readiness | **In Progress — MESP-86 v0.4 approved; MESP-57 through MESP-64 completed; the Foundation Completion checkpoint is ready for Opus 5 review** |
| Phase 7 — Jira Backlog | **Done for MESP-27 Wave 1** |
| Phase 8 — Implementation and Automated Testing | **MESP-57, MESP-58, MESP-87, MESP-59, MESP-88, MESP-60, MESP-62, MESP-63, MESP-89, MESP-90, MESP-61, MESP-64 and MESP-91 Done; MESP-92 In Progress (payload immutability and single-effect correction); MESP-93 and MESP-94 To Do; no Sprint active** |
| Phase 9 — Integration, UAT, Release, Operations | **Not Started** |

---

## Current next action

> MESP-92 (`Guarantee single-effect durable work execution and immutable
> typed payloads`) is **In Progress** on branch
> `fix/MESP-92-single-effect-immutable-payloads`. PR #22 received a focused
> ChatGPT security review (H92-01, H92-02, M92-01, M92-02); this overlay
> records the corrections. PR #22 remains open, non-draft and held unmerged
> pending a focused ChatGPT re-review. MESP-93, MESP-94 and MESP-31 remain
> To Do. Master Data and Catalog, Retail POS and future ERP transaction work
> remain out of scope. `MESP-48` and `MESP-50` remain explicit production
> gates. No Sprint is active, and MESP-93, MESP-94 and MESP-31 must not
> start before MESP-92 closes.

## MESP-92 In Progress — single-effect durable work and immutable payloads

MESP-92 corrects four MESP-91-review findings in the merged durable-work
seam: H-5 (mutable stored payload references), H-6 (duplicate protected
effect after a caught post-boundary interruption or uncertain completion),
M-2 (sequential tests presented as concurrency evidence) and L-1 (misleading
Relational store naming). Branch `fix/MESP-92-single-effect-immutable-payloads`
is based on merged-main baseline `32a91f27bc162685fc0db0f38b031d02ffbc99d2`.
A subsequent focused ChatGPT security review of PR #22 raised four further
findings — H92-01, H92-02, M92-01 and M92-02 — corrected below.

An explicitly registered `IDurableWorkPayloadRegistry`/`IDurableWorkPayloadCodec<TPayload>`
pair converts every submitted payload immediately into an immutable,
checksummed `DurableWorkPayloadEnvelope`. `DurableWorkItem` never retains the
caller's original payload reference; every external byte access and every
handler decode returns an independent defensive copy. Unknown payload types,
handler/payload mismatches, checksum tampering and oversized or malformed
payloads fail closed before a handler executes, and payload bytes never
appear in audit or evidence. M92-02 removes the production
`TamperForValidation()` fault-injection hook; checksum-corruption tests use
bounded reflection in the test project instead, and a custom codec's
encode/decode exception is always wrapped in the safe
`DurableWorkPayloadException` with no original message, CLR type name or
payload-controlled data exposed.

A server-owned `DurableWorkEffectKey` guards one protected effect.
H92-01 namespaces that key with an explicit `DurableWorkEffectPurpose`
(`Handler` or `Outbox`) and, for an outbox effect, the immutable `EventId`,
so a handler effect and an outbox effect for the identical
Tenant/WorkItemId/OperationId can never suppress each other even when both
are guarded by the same shared `IDurableWorkEffectExecutor`. H92-03 replaces
the removed `DurableWorkEffectComposition.CreateSharedExecutor()` (which
produced a new, independent ledger on every call) with
`DurableWorkLocalRuntime.Create(operationCatalogue, payloadRegistry)`, the one
approved composition entry point; it is the only place shipping code may
construct `InMemoryDurableWorkEffectGuard`, `DurableWorkEffectExecutor`,
`InMemoryDurableWorkStore` or `DurableWorkDispatcher` (all four constructors
are `internal`), and it supplies the identical executor instance to the store
and the dispatcher it returns. A syntax-tree architecture test scans all of
`src/MiniErp.App` and fails if any of those four types is constructed
anywhere outside `DurableWorkLocalRuntime.cs`. Reservation of that
key remains the single non-reversible boundary: every registered handler
invocation and every outbox effect is routed exclusively through
`ExecuteHandlerEffectAsync`, which a normal handler cannot bypass
(architecture-enforced). H92-02 replaces the generic `DurableWorkHandlerResult`
returned from inside that boundary with an explicit
`DurableWorkProtectedEffectResult` outcome — `Applied`, `NotAppliedRetryable`,
`OutcomeUnknown` or `TerminalNotApplied` — so a bare generic retry can no
longer release a reservation after an effect may already have run; only an
explicit `NotAppliedRetryable` outcome releases it. An interruption
discovered before the reservation boundary permits bounded retry; a caught
exception or cancellation observed inside the running process after that
boundary yields `OutcomeUnknown` and is never automatically retried.
Completed effects replay their exact recorded safe result on duplicate
dispatch. Outbox delivery now reports explicit `Delivered` (Applied),
`RetryScheduled` (NotAppliedRetryable), `DeadLettered` (TerminalNotApplied or
an exhausted retry budget) or `OutcomeUnknown` outcomes.

M92-01 makes `DurableWorkLifecycle.OutcomeUnknown` a dedicated, Tenant-scoped
reconciliation state for both handler work items and outbox messages: normal
polling never selects it, the generic outbox redelivery/replay hook refuses
to restart it, and audit records the safe `work.outcome-unknown`/
`outbox.outcome-unknown` events with no payload or provider exception text.
`IDurableWorkStore.ReadUncertainEffectsAsync` is a read-only reconciliation
port; H92-04 replaces its raw `TenantContext` parameter with a server-issued
`VerifiedDurableWorkReconciliationAuthorization`. `IdentityAuthorizationService`
(as the new `IDurableWorkReconciliationAuthorizer`) live-revalidates actor,
session, Membership-or-SupportGrant validity and a dedicated catalogue-backed
`work.reconciliation.read` permission, reusing the identical
organization-scope ownership/containment logic as MESP-91 dispatch
revalidation so a missing or malformed selected scope fails closed;
`TenantWorkScope.ContainsDescendant` then filters returned records to the
authorized Tenant/Company/Branch/Warehouse boundary and its verified
descendants only. A sibling organization and another Tenant are never
visible, and `PlatformGovernanceContext` has no path into this authorizer. No
production reconciliation UI or provider decision is implemented.

M92-03 gives every returned record an exact, safe identity: it now carries
the exact `DurableWorkEffectKey` (so `OperationId` is always present and
`EventId` is present only for an Outbox-purpose record) plus the exact
verified `TenantWorkScope`, the actual `OutcomeUnknownAt` transition time and
a preserved safe reason. `TenantOutboxMessage` gained explicit
`OutcomeUnknownAt`/`SafeFailureReason` fields, removing the prior reuse of
`NextAttemptAt` as the occurrence time and the hard-coded outbox reason.

M92-04 normalizes every exception a registered payload codec raises --
including one raised as `DurableWorkPayloadException` itself -- to one of
`DurableWorkPayloadRegistry`'s own fixed, safe messages, never attaching the
original exception as `InnerException`; `DurableWorkPayloadException`'s
constructor is now `internal` so only the envelope/registry seam can raise a
trusted one. `OperationCanceledException` still propagates unwrapped, and
checksum-mismatch/oversized-payload rejections keep their own fixed messages.

L92-01 corrects the `OutcomeUnknown`/`IDurableWorkEffectExecutor` documentation:
a caught post-boundary exception, a caught cancellation, provider-reported
uncertainty or a completion-recording failure observed by the running process
-- never an actual process crash, which instead loses this in-memory ledger
entirely and is never represented as a recorded outcome. Production durable
crash recovery for this local Foundation seam remains explicitly deferred.

Genuine concurrency evidence uses `Barrier`-synchronized concurrent Tasks to
prove: one lease winner under active and expired-lease contention; one
effect winner under concurrent reservation; stale-completion rejection after
lease reclaim; and one effect from concurrent duplicate submissions.

`IRelationalDurableWorkStore`/`InMemoryRelationalDurableWorkStore` are
renamed to `IDurableWorkStore`/`InMemoryDurableWorkStore`; the type no longer
implies relational, SQL-backed, process-crash-durable, production-ready or
distributed exactly-once behavior. This adapter preserves only a caught
post-boundary interruption as `OutcomeUnknown`; an actual process crash loses
its in-memory guard and lifecycle state entirely and is not represented as
`OutcomeUnknown` or any other recorded outcome. Production durable crash
recovery remains deferred to a future SQL/durable provider.

Validation on this branch after the second focused-review correction: Release
build **0 warnings/0 errors**; focused DurableWork suite **199/199** passed;
full backend regression **457/457** passed including **11/11** SQL Server
LocalDB probes with no `MiniErpFoundation_*` database remaining after
teardown; Angular unit tests **27/27** passed; Angular production build
succeeded; Playwright **4/4** passed; `npm audit --omit=dev
--audit-level=high` reported **0** vulnerabilities.

MESP-92 is **not** marked Done by this update. PR #22 is opened non-draft and
held unmerged for a further focused ChatGPT re-review of the structurally
enforced single effect ledger, the scope-authorized reconciliation read port,
the exact uncertain-effect identity, the custom codec exception
normalization and the corrected crash terminology. No broker, production SQL
work store, production worker deployment, migration, Master Data
implementation, MESP-48 or MESP-50 work was introduced.

## MESP-91 Correction Package 1 — merged and Done

The MESP-91 correction overlay was implemented on
`fix/MESP-91-verified-work-scope-authority`, based on merged-main baseline
`4eb1ef3ab094242cbb26ec9ab79b4037512e0d2d`. Source and regression corrections
are recorded through commit `92bd9fd38912a062cc3723f46867258d54ca8127`
(approved PR #20 head).

The durable-work contract now selects an operation from an authoritative
descriptor catalogue. The descriptor owns the exact permission code, allowed
authorization paths and scope policy; the same immutable descriptor is used by
submission identity, stored initiator facts, handler registration, live
Identity revalidation and worker/verified outbox dispatch. Unknown or
mismatched operation descriptors fail closed. Approved revalidation returns a
server-issued exact-scope execution authorization; the worker and outbox effect
receive that verified context rather than the broad caller context.

True authority denials remain terminal `AuthorizationDenied` outcomes. Provider
exceptions/timeouts are `ProviderUnavailable` bounded retries, and cancellation
is a distinct recoverable `Cancelled` outcome. No handler or protected outbox
effect runs after a failed authority check.

Validation evidence for this correction is **102/102** focused durable-work
tests, **360/360** complete backend tests, **11/11** SQL Server LocalDB
probes, **27/27** Angular tests, **4/4** Playwright journeys, Release build 0
warnings/0 errors, and production dependency audit 0 vulnerabilities — verified
both pre-merge on the approved head and again against merged `main`. The
disposable LocalDB/model collation observed was `SQL_Latin1_General_CP1_CI_AS`;
no disposable Foundation database remained after cleanup.

Approved head `92bd9fd38912a062cc3723f46867258d54ca8127` received a focused
ChatGPT security review disposition of **APPROVED TO MERGE** (0 Critical, 0
High, 0 Medium blockers), closing findings H-1, H-2, H-4, H91-01, H91-02,
H91-03, H91-04, M91-01 and M91-02. PR #20 was merged by normal merge commit at
`f2cde57400fed470ab048776e05b56f353b36890`. MESP-91 is **Done**; MESP-92 is
the next eligible correction; MESP-93, MESP-94 and MESP-31 remain **To Do**.
No Sprint, Master Data implementation, production provider, migration, Retail
POS, Wafra-core, MESP-48 or MESP-50 work was started.
