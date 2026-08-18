# MINI ERP SAAS PLATFORM
# MESP-124 — CLAUDE OPUS 5 INDEPENDENT PRE-MERGE RE-REVIEW
# PURCHASE ORDER COMMERCIAL INTEGRITY, SOURCE UNIQUENESS, DURABLE REPLAY,
# ACCESSIBILITY, AND HTTP SEMANTICS

## Canonical current handoff — read before the retained review checklist

Sole executor: Claude Opus 5. Review mode: read-only independent pre-merge
review of `feat/MESP-124-purchase-order-confirmation` / Draft PR #68 against
the synchronized `main` baseline. Do not modify source, tests, migrations,
generated artifacts, documentation, `TASK.md`, Jira, the pull request, or any
database. Do not commit, push, merge, close the draft PR, start MESP-125, or
begin Goods Receipt, Inventory, Finance, AP/accounting, payments, supplier
portal, external integration, ZATCA/FATOORA, DNS/TLS, provider, production,
or Wafra-specific core work. Jira is read-only and `frontend/assets` are
owner-managed source assets that must remain untouched.

The retained checklist below is historical context. Its current validation
counts are superseded by `.ai/CURRENT_STATE.md` and `docs/staticts.md`; inspect
the complete current diff and rerun the required checks before deciding.

### Executor evidence handoff — 18 August 2026

The final bounded executor correction is present on the branch before this
read-only review: REST idempotency cache fingerprints now include the target
Purchase Order for edit, confirmation, and lifecycle routes; both impossible
confirmation-quantity error spellings map to HTTP 409; the Tenant-scoped
`(TenantId, SourceDecisionId)` unique-index model invariant has a focused test;
inactive tabpanel anchors keep every tab `aria-controls` relationship
rendered; and the frontend lockfile resolves `nanoid` 3.3.18. Current evidence
is recorded in `.ai/CURRENT_STATE.md` and `docs/staticts.md`: Release build
0/0, backend **790/790**, focused Purchase Order **11/11**, failure
classification **9/9**, Angular **215/215**, focused Chromium **7/7**, full
Chromium **15/15**, production bundle **492.02 kB initial / 75.74 kB PO lazy /
91.94 kB quotation lazy**, and both production-only and full `npm audit` at
zero vulnerabilities. The official runtime configuration smoke passed, live
API health/module registration and Angular root checks returned HTTP 200 on
5300/4300, and the repository-owned listeners remain running. This evidence
does not replace the independent review or authorize a merge.

### Mandatory P1/P2 gates

P1-A — Confirmation facts must survive commercial changes. Verify that the
same supplier response persists status, response date, reference/contact,
confirmed quantity, expected date, reason/notes, and line facts even when it
proposes quantity, price, or delivery changes. Approving or rejecting a
proposal must recompute ordered, confirmed, remaining, latest confirmation,
and resulting status from durable facts; rejected price/date changes retain
the prior commitment; approved quantity changes use the approved quantity.
Proposed quantity below already confirmed quantity must fail before any
confirmation, change, history, audit, line, version, or status mutation with
an explicit safe error. Verify full/partial/rejected/no-response, all change
types, approve/reject, impossible quantity, multi-line all-or-nothing, and no
duplicate evidence/history/audit tests.

P1-B — One PO consumes one source decision. Verify the Tenant-aware unique
index `(TenantId, SourceDecisionId)` and the new additive migration
`20260818103736_PurchaseOrderCommercialIntegrityAndDurableReplay`. Existing
`20260817143432_PurchaseOrderAndSupplierConfirmation` and
`20260817211222_AddPurchaseOrderAuditRequestFingerprint` must be unchanged.
No terminal-state reuse is allowed without an approved rule. Consumed source
options are hidden; create revalidates source, Tenant, Company/Branch,
quotation, supplier, currency, and lines server-side; preflight and database
races map to `purchase_order_duplicate` / HTTP 409, never 503; cross-Tenant
visibility/consumption remains impossible.

P2-C — Durable replay must return the exact original successful response, not
mutable current PO reconstruction. Verify versioned immutable serialized
`PurchaseOrderRecord` snapshots on audit evidence for create/edit/lifecycle,
confirmation, and supplier-change successes, with no raw request replay
payload. Current target/resource authorization must precede replay for existing
targets. Exact actor/operation/key/target/fingerprint matching, conflict 409s,
malformed snapshot fail-closed behavior, transaction ordering, and no replay
side effects must remain intact. Verify replay after later mutation for submit,
approve, issue, confirmation that created `ChangedPendingApproval`, supplier
change approval/rejection, and create after source drift; also verify cache
expiry/process restart durability and no duplicate history/audit/evidence/
confirmation/change/version effects.

P2-D — PO Angular accessibility must be real rendered behavior: every table
uses `<th scope="col">`; tabs have stable IDs, tablist/tab roles,
`aria-selected`, `aria-controls`, keyboard navigation, and linked tabpanel
IDs/`aria-labelledby`; action dialogs have entry focus, Tab/Shift+Tab wrap,
Escape, safe backdrop behavior, disabled/saving safety, and opener focus
restoration after cancel/Escape/success. Verify LTR/RTL tab keys and focused
tests, using the Purchase Request pattern without unrelated UI scope.

### Mandatory P3 gates

P3-E: `creator_only`, `self_approval_denied`, and `approval_not_eligible` are
HTTP 403; `approval_duplicate`, `purchase_order_duplicate`, impossible
quantity, confirmation conflicts, and idempotency conflicts are HTTP 409.
P3-F: ISO money formatting explicitly uses 2 minimum/maximum fraction digits;
non-ISO fallback stays localized and retains the raw code, with no FX,
persisted amount, tax, or accounting effect. P3-H: tests cover eligible,
ineligible, valid active delegation, invalid/expired/wrong-scope or
ineligible-delegator delegation, and self-approval, including evidence.
P3-J: after supplier-change rejection, `LatestConfirmationStatus`, PO status,
ordered/confirmed/remaining quantities, and commercial values remain mutually
consistent; do not restore stale `StatusBeforeSupplierChange`.

Do not widen into P3-G retry UI or P3-I bundle optimization unless a direct
regression is proven. Findings must state exact file/line or behavior,
severity, evidence, impact, and smallest bounded correction. Stop after the
independent verdict; do not activate another session.

Sole Executor:
Claude Opus 5

Review Mode:
READ-ONLY INDEPENDENT PRE-MERGE REVIEW

Repository:
D:\AI Tools\Hossam\mini-erp-saas-platform

## Review target

Review branch:

    feat/MESP-124-purchase-order-confirmation

against the synchronized main baseline and Draft PR #68.

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

## Pre-Opus Sol findings correction (must be explicitly re-verified)

Before this review was activated, GPT-5.6 Sol raised two findings against the
completed MESP-124 implementation. Claude Sonnet 5 performed a single bounded
corrective session on `feat/MESP-124-purchase-order-confirmation` to resolve
both. This review must explicitly re-verify both corrections rather than
assume them proven by the executor's own report.

**F-1 (Currency Rendering Resilience).** `formatMoney` in
`frontend/src/app/features/procurement/purchase-order-workspace.component.ts`
previously called raw `Intl.NumberFormat` with `style: 'currency'` and no
fallback; a valid MESP-configured non-ISO currency code (e.g. `S2K`, `CUSTOM`)
could throw `RangeError` and break Purchase Order list/detail rendering. The
correction reuses the proven MESP-123 Supplier Quotation pattern: a try/catch
around the ISO currency-styled format, falling back to a localized decimal
format suffixed with the raw currency code (e.g. `1,234.56 S2K`), with the
PO-specific `currencyDisplay: 'code'` UX preserved. Verify:

- both the try branch and the fallback branch produce a stable 2-decimal
  render for standard ISO currencies (SAR, USD) and non-ISO configured codes
  (S2K, CUSTOM) without throwing;
- the raw currency code is retained in the fallback text for audit/comparison
  clarity;
- zero FX conversion, zero currency substitution, and zero hidden-amount
  behavior;
- regression coverage in
  `frontend/src/app/features/procurement/purchase-order-workspace.component.spec.ts`
  (direct `formatMoney` safety test and a list-rendering test with a
  non-ISO-currency row alongside a normal row) and that the existing
  Supplier Quotation `S2K` behavior remains untouched.

**F-2 (Idempotency Replay/Conflict Fidelity).**
`PurchaseOrderPersistence.FindReplayAsync` previously matched only Tenant +
ActorId + OperationId + IdempotencyKey and returned whichever PO that
combination last touched, without validating target resource identity or a
canonical request fingerprint — an identical key reused against a different
request or a different target could silently replay an unrelated result. The
correction threads a deterministic server-side SHA-256 request fingerprint
from the REST endpoint layer through `PurchaseOrderService` into
`PurchaseOrderPersistence`, and a 3-way `ReplayLookup` (NotFound / Replay /
Conflict) across every unsafe MESP-124 command (create, edit, submit,
approve, issue, confirmation capture, supplier-change approve/reject).
Verify:

- identical retry (same actor/operation/target/key/fingerprint) deterministically
  replays the original result without re-mutating, even if the entity has
  since moved on;
- same key + same target + different semantic payload is rejected with HTTP
  409 and code `idempotency_conflict`, without mutating the target;
- same key reused against a different target (cross-target replay) is
  rejected with the same conflict semantics rather than ever replaying an
  unrelated Purchase Order's result;
- the new `RequestFingerprint` column on `PurchaseOrderAudit` is delivered as
  an additive EF Core migration
  (`20260817211222_AddPurchaseOrderAuditRequestFingerprint`), not a rewrite of
  an already-applied migration;
- Tenant scoping of replay lookup is preserved (the `ProcurementDbContext`
  Tenant query filter applies transparently to `FindReplayAsync`) — no
  cross-Tenant replay or conflict leakage;
- optimistic concurrency (`If-Match` / expected version), transactions, audit,
  and history semantics are unweakened by the fingerprint check;
- regression coverage in `PurchaseOrderTests.cs`
  (`Distinguishes_identical_retry_replay_from_cross_target_and_same_target_fingerprint_conflicts`)
  exercises all three outcomes (replay, same-target conflict, cross-target
  conflict) with an explicit zero-mutation assertion on the untouched target.

**F-2 completeness follow-up (durable replay ordering).** A second bounded
correction closed the remaining F-2 completeness gap. The fingerprint design
and the persistence-side conflict detection above were accepted and were not
redesigned; the defect was **ordering** in `PurchaseOrderService`. Several
commands performed lifecycle-state, optimistic-concurrency, approval-stage,
approval-policy, delegation, supplier-change, and reapproval checks *before*
persisted replay could be consulted, so an identical retry stopped being
replayable once the original success advanced state — and permanently so once
the volatile ten-minute `LocalMasterDataIdempotencyStore` REST cache expired
or the API process restarted. The correction adds a bounded read-only probe
`IPurchaseOrderPersistence.ProbeReplayAsync` returning NotFound / Replay /
Conflict over the already-stored Tenant-scoped audit evidence, and calls it
from `PurchaseOrderService` in the correct position. No schema change was
introduced and the accepted additive migration was not rewritten. Verify:

- **durability**: replay is resolved from persisted `PurchaseOrderAudit`
  evidence, not from the in-memory REST idempotency cache, so it survives
  cache expiry and an API process restart; confirm the regression tests
  exercise `PurchaseOrderService` plus real persistence and genuinely bypass
  `LocalMasterDataIdempotencyStore`;
- **ordering is not an authorization bypass**: the probe runs only after the
  trusted Tenant context, the current target resource, and the current
  actor's authority over that resource are established, and before lifecycle
  state, current version comparison, approval stage, approval-policy
  re-resolution, delegation resolution, supplier-change current-state
  validation, and reapproval-policy re-resolution. Confirm that a caller whose
  authorization has genuinely been revoked cannot use idempotency to reveal
  the old resource, and that separation of duties still holds (replay is
  matched on the exact actor);
- **identical replay after state advanced** for: submit after the order
  reached `PendingApproval` (must not return `submit_not_allowed`), approve
  after `Approved`, issue after `Issued`, a Rejected supplier confirmation
  after the order became `Rejected`, a confirmation that created
  `ChangedPendingApproval` (the exact original confirmation must replay, not
  fail `confirmation_not_allowed`), supplier-change approval after the order
  left `ChangedPendingApproval`, and supplier-change rejection after the order
  left `ChangedPendingApproval`. In each case the original version/result must
  be returned even though the expected version supplied by the original
  request is now stale;
- **no duplicated evidence** on any replay: no duplicate history entry, no
  duplicate audit success entry, no duplicate confirmation, no duplicate
  supplier change, and no second mutation;
- **create**: an identical create retry replays the originally created
  Purchase Order even after the mutable sourcing state has drifted, while
  remaining Tenant- and actor-authorized against the replayed order's own
  scope; a genuinely new create with a different key still runs full current
  source-decision validation and fails closed
  (`source_quotation_not_eligible`);
- **conflict semantics are unweakened**: same key + different fingerprint and
  same key + different target still return `idempotency_conflict` (HTTP 409)
  and leave the target unmutated;
- **defense in depth retained**: the in-transaction persistence-side replay
  check inside `MutateAsync` / `RecordConfirmationAsync` / `CreateAsync` was
  not removed, and a probe failure falls through to the normal path rather
  than authorizing a mutation on its own;
- regression coverage in `PurchaseOrderTests.cs`
  (`Replays_durable_submit_approve_and_issue_after_the_original_request_advanced_state`,
  `Replays_durable_confirmation_and_supplier_change_approval_after_the_order_left_the_eligible_state`,
  `Replays_durable_supplier_change_rejection_after_the_order_left_changed_pending_approval`,
  `Replays_durable_create_after_source_state_drift_without_weakening_new_create_validation`)
  is genuinely load-bearing rather than tautological.

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
