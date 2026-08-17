# MINI ERP SAAS PLATFORM
# CLAUDE OPUS 5 — INDEPENDENT MESP-124 PRE-MERGE REVIEW
# PURCHASE ORDER + SUPPLIER CONFIRMATION

Sole Executor:
Claude Opus 5

Review Mode:
READ-ONLY INDEPENDENT PRE-MERGE REVIEW

Repository:
D:\AI Tools\Hossam\mini-erp-saas-platform

## Review target

Review branch:

    feat/MESP-124-purchase-order-confirmation

against the synchronized main baseline and its draft pull request.

MESP-124 is the bounded Purchase Order and Supplier Confirmation capability
under Parent Epic MESP-7 — Procurement and Purchase-to-Pay. The implementation
is complete on the feature branch and has passed the executor's required
validation. This task is the independent checkpoint before any merge decision.

Do not implement fixes during this review. Do not modify source, tests,
migrations, documentation, TASK.md, generated artifacts, Jira, the pull
request, or the database. Do not merge, push, close the draft PR, start
MESP-125, or begin Goods Receipt, Inventory, Finance, AP, payments, or any
external integration. If a problem is found, report the exact file, line or
behavior, severity, evidence, and required correction; leave implementation to
a later bounded executor session.

## Governance and activation context

The bounded implementation was activated only after the following read-only
Jira facts were verified:

- MESP-143 — Tenant-aware Entry, Host Resolution, Workspace Context, Branding
  and SAR Presentation — is Done and squash-merged to main.
- MESP-124 — Purchase Order and Supplier Confirmation — is In Progress.
- MESP-42 — Purchase approval workflow — is Done.
- MESP-43 — Supplier confirmation, partial confirmation, and supplier-change
  behavior — is Done.
- MESP-55 — Approval delegation behavior — is Done.

Jira writes are owned by GPT-5.6 Sol. This review must not write Jira.

MESP-143 is the mandatory architecture prerequisite. Tenant is the security
boundary and is not an ERP workspace. Host resolution is only candidate
routing information; server-side authentication, exact-Tenant membership,
trusted Tenant context, and authorized Company/Branch scope remain the
authority. The MESP-124 implementation must not reintroduce a user-selectable
Tenant filter, raw GUID workspace entry, unrelated-Tenant enumeration, or a
parallel workspace authorization hierarchy.

## Required reading

Read the complete current versions of these files before forming a verdict:

1. AGENTS.md
2. CLAUDE.md
3. .ai/CURRENT_STATE.md
4. TASK.md
5. docs/21_Procurement_and_Purchase_to_Pay_BRD.md
6. docs/ADR-019_Tenant_Host_Resolution_Workspace_Context_and_Branding.md
7. docs/MESP-143_Tenant_Aware_Entry_Execution_Plan.md
8. docs/ADR-002_Backend_Project_Structure_and_Module_Enforcement.md
9. docs/ADR-006_Module_Schemas_EF_Core_Migrations_Transactions.md
10. the complete MESP-124 diff against main

Read materially dependent MESP-123 Purchase Request, Supplier Quotation,
Source Decision, approval-policy, delegation, audit, authorization, and
Foundation operation-catalogue code as needed to verify reuse and regression
behavior. Do not treat ticket count, documentation, or a passing unit test as
proof of production capability by itself.

## Bounded capability under review

The implementation must deliver only this bounded commercial Procurement
capability:

Purchase Request
  -> eligible approved Supplier Quotation
  -> approved Supplier Source Decision
  -> Draft Purchase Order
  -> Submitted / Pending Approval
  -> Approved
  -> Issued
  -> manual Supplier Confirmation
  -> Confirmed / Partially Confirmed / Rejected / No Response
  -> controlled supplier change and reapproval where required

Verify that the PO can be created only from an eligible server-validated source
decision. The client must not be able to invent or substitute the Supplier,
Currency, Purchase Request, quotation, selected lines, Company, Branch,
payment terms, or commercial facts. Verify persisted source lineage and
commercial snapshots include, where applicable:

- Tenant and authorized Company/Branch scope;
- Supplier and Currency;
- Purchase Request, Supplier Quotation, and Source Decision identifiers;
- payment-term snapshot;
- product, SKU, UOM, quantity, price, discount, tax, and requested/committed
  date facts;
- source rationale and source-selection time;
- deterministic source quotation line lineage.

Verify that draft edits, where approved, remain creator-controlled,
versioned, and auditable, and that organization scope cannot be edited after
commercial commitment.

## Independent review checklist

### 1. Lifecycle and source integrity

Review all backend commands, persistence transitions, API routes, Angular
actions, and tests for:

- eligible approved source decision prerequisite;
- explicit Draft, submit, approval, issue, confirmation, rejection,
  no-response, partial, cancellation, and supplier-change states;
- no hidden transition that bypasses approval or separation of duties;
- no silent replacement of the selected quotation or supplier;
- no silent deletion of a PO, upstream Purchase Request, or sourcing decision;
- no creation of a second PO when the source decision or request is reused;
- source data revalidation at persistence time, not only in Angular;
- exact selected-line set and source-line lineage validation;
- no cross-tenant or cross-Company/Branch source substitution.

### 2. Approval, separation of duties, and reapproval

Verify reuse of the existing configuration-led approval and delegation
capability rather than an independent approval engine:

- effective approval policy is resolved server-side;
- policy and version are snapshotted on the PO;
- stage and approver evidence are preserved;
- requester cannot self-approve;
- duplicate approval is rejected;
- only eligible approvers or valid delegated actors can approve;
- rejection and return-for-change require and preserve a reason;
- approval uses the exact PO optimistic-concurrency version;
- supplier changes requiring reapproval move to a controlled pending state;
- previous approval evidence remains history and is not overwritten;
- new approval evidence records policy, actor, delegation, stage, version, and
  resulting status;
- rejected supplier changes preserve the prior commercial commitment.

### 3. Supplier confirmation semantics

Verify that confirmation is manual and records immutable evidence, including:

- full confirmation;
- per-line partial quantity confirmation;
- exact ordered, confirmed, and remaining quantities;
- supplier rejection with explicit reason;
- no response / pending evidence;
- changed quantity;
- changed unit price;
- changed delivery date;
- supplier reference/contact and optional evidence references;
- actor, timestamp, PO version, correlation, and audit context.

Verify that server-side normalization recomputes remaining quantities and
rejects invalid status/quantity combinations. Confirmations must not create
stock reservations, goods receipts, warehouse movement, invoices, AP entries,
payments, accounting postings, or external supplier calls.

### 4. Supplier changes and commercial commitment safety

Verify that an issued or confirmed PO is never silently mutated by a supplier
proposal. Each proposed change must preserve:

- previous/current value;
- proposed value;
- line and confirmation lineage;
- reason;
- proposing actor;
- proposal timestamp;
- decision actor and timestamp;
- approval/rejection decision;
- resulting PO status.

Verify that approval applies only the approved proposed values, while rejection
leaves the prior values in force. Verify that line-level confirmation history
continues to reflect the exact commercial remainder after an approved change.

### 5. Tenant, organization scope, and authorization

Review every read and mutation path for:

- server-owned Tenant context;
- Tenant query filters and composite Tenant-owned relationships;
- exact membership and permission checks;
- Company/Branch scope checks independent of client-supplied identifiers;
- safe no-access behavior for another Tenant or unauthorized scope;
- no Tenant ID accepted as a scope-widening client filter;
- no raw GUID UX requirement;
- server-derived capability flags that do not replace authorization;
- audit evidence containing Tenant, Company/Branch, actor, authorization path,
  decision, correlation, and operation identity.

Pay special attention to direct object reads, source-decision lookup,
confirmation lookup, history, audit, replay, and idempotency paths.

### 6. Concurrency, idempotency, and failure behavior

Verify:

- SQL Server rowversion / optimistic concurrency is used for PO, line, and
  confirmation mutations as appropriate;
- If-Match / exact expected-version handling is enforced;
- stale edit, approval, issue, confirmation, and supplier-change requests
  return a safe conflict and do not partially mutate data;
- idempotency keys replay the original result rather than duplicate PO,
  confirmation, history, or audit events;
- replay lookup is Tenant- and actor/operation-scoped;
- transaction rollback protects failed multi-row confirmation/reapproval
  writes;
- duplicate identifiers and invalid line sets are rejected;
- persistence errors do not expose SQL, stack traces, or sensitive Tenant data.

### 7. Persistence and migration

Verify the four-project backend topology:

- MiniErp.Api
- MiniErp.App
- MiniErp.Contracts
- MiniErp.Infrastructure

Verify Procurement module ownership, the procurement SQL Server schema,
Tenant-owned entity coverage, composite Tenant foreign keys, appropriate
indexes, restrictive/cascading delete choices, and the formal committed EF
Core migration for Purchase Order and Supplier Confirmation. Verify that
production authority remains SQL Server with migrations and that SQLite is
only an explicit Development/test harness. No second database, competing
migration chain, production EnsureCreated, or cross-module persistence is
allowed.

### 8. REST, Foundation catalogue, OpenAPI, and operational safety

Verify every public operation:

- has a stable operationId;
- has a useful Summary and truthful response description;
- is present in the Foundation operation catalogue;
- declares the correct permission, security profile, scope policy, antiforgery,
  idempotency, audit, and concurrency behavior;
- is represented truthfully in generated OpenAPI/Scalar;
- uses safe problem details without leaking implementation internals;
- has unsafe mutation protection and the established correlation/audit seam.

Review all unsafe handlers for explicit antiforgery, permission, Tenant/scope,
expected-version, idempotency, and audit behavior.

### 9. Angular UX and regression safety

Verify the bounded Angular workspace:

- follows the MESP-143 Overview-first authenticated shell and current
  operational context;
- has list, eligible-source selection, immutable source review, create/edit,
  detail, approval, issue, cancel, confirmation, partial-confirmation,
  rejection, supplier-change, reapproval, history, and audit surfaces;
- does not expose downstream Inventory, Goods Receipt, Finance, AP, payment,
  or external-integration UI;
- displays ordered/confirmed/remaining quantities clearly;
- disables or hides actions based on server capability while relying on the
  server for authorization;
- sends If-Match, idempotency, and antiforgery headers for mutations;
- presents stale conflicts and safe access errors clearly;
- keeps the main bundle within the existing budget and isolates the PO feature
  appropriately;
- preserves the MESP-123 Purchase Request, quotation, comparison, and source
  decision journeys.

Review EN, AR, RTL direction, accessible labels, keyboard/focus behavior,
semantic tables or grids, role/status messaging, and the absence of hardcoded
Wafra-specific core behavior. Owner-managed files under frontend/assets must
remain untouched.

## Required validation

Run the following from the repository root, using only safe/disposable test
state. Do not run a broad destructive command and do not use the owner's
persistent MESP database for test seeding. The backend runner is the required
safe harness and must preserve the MESP data-integrity sentinel.

1. Inspect the complete diff and working tree:

       git status --short
       git diff --check
       git diff main...HEAD --stat
       git diff main...HEAD -- backend/src backend/tests frontend/src frontend/e2e

2. Backend Release build and required safety runner:

       dotnet build .\backend\MiniErp.sln --configuration Release --no-restore --verbosity minimal
       .\scripts\Test-MiniErpBackend.ps1

   The runner must pass with zero failures and must report that MESP data is
   intact. Do not substitute an unsafe direct full-suite run for the runner.

3. Targeted backend tests, if needed for diagnosis:

       dotnet test .\backend\tests\MiniErp.ArchitectureTests\MiniErp.ArchitectureTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PurchaseOrderTests|FullyQualifiedName~RestFoundationTests" --verbosity minimal

4. Angular:

       Set-Location .\frontend
       npm test -- --watch=false --no-progress
       npm run build
       npm audit --omit=dev

   Do not raise bundle budgets to make this pass.

5. Playwright Chromium:

       npx playwright test e2e/purchase-order.spec.ts --project=chromium
       npm run test:e2e -- --project=chromium

   Confirm the suite covers PO creation from an eligible source decision,
   approval/issue, full confirmation, partial confirmation, rejection,
   supplier change/reapproval, stale concurrency, Tenant/organization denial,
   EN/AR/RTL behavior, and MESP-123 regression. Distinguish automated
   Chromium evidence from manual browser signoff; do not claim manual signoff
   unless it actually occurred.

## Verdict standard

Return exactly one of these verdicts:

### APPROVE FOR MERGE

Use only when the complete diff is bounded, the lifecycle and source lineage
are correct, Tenant and organization isolation is secure, persistence and
migration ownership are sound, all required validation is green, no material
security/accounting/data-integrity/regression issue remains, and the
implementation does not cause excluded downstream side effects.

### CHANGES REQUIRED

Use when a concrete correctable implementation, test, documentation, UX,
OpenAPI, concurrency, audit, or migration issue remains. List each finding
with severity P0/P1/P2/P3, exact location, evidence, impact, and the smallest
bounded correction. Do not edit the repository.

### BLOCKED

Use only for a genuine unresolved Tenant-isolation, accounting/data-integrity,
destructive-migration/data-loss, legal/external-validation,
credential/production-infrastructure, or material architecture blocker that
cannot be safely corrected inside the bounded MESP-124 follow-up.

## Required review report

The review report must include:

- verdict;
- reviewed branch, commit, and PR;
- exact commands run and their results;
- lifecycle and source-lineage assessment;
- approval/SoD/delegation/reapproval assessment;
- confirmation, partial, rejection, and supplier-change assessment;
- Tenant and Company/Branch isolation assessment;
- concurrency/idempotency/transaction assessment;
- audit/history/evidence assessment;
- REST/Foundation/OpenAPI assessment;
- persistence/migration assessment;
- Angular EN/AR/RTL/accessibility and bundle assessment;
- MESP-123 regression assessment;
- scope-exclusion assessment;
- every finding with severity and exact evidence;
- explicit statement that no files, Jira, PR state, database, merge, or next
  capability were changed by the review.

After the report, STOP. Do not execute any future root task automatically.
