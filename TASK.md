# MINI ERP SAAS PLATFORM
# MESP-124 — PURCHASE ORDER + SUPPLIER CONFIRMATION
# APPROVAL / ISSUE / PARTIAL CONFIRMATION / CHANGES / REAPPROVAL
# COMPLETE BOUNDED PROCUREMENT CAPABILITY

Sole Executor:
GPT-5.6 Luna Max

Reasoning:
MAXIMUM

Mode:
IMPLEMENTATION

## Activation Gate

DO NOT EXECUTE this task unless GPT-5.6 Sol has confirmed:

- MESP-143 = Done in Jira;
- MESP-124 = In Progress in Jira.

If either condition is false:

STOP without changing product code.

## Mission

Implement MESP-124:

Purchase Order and Supplier Confirmation including partial changes.

Owning Epic:

MESP-7 — Procurement and Purchase-to-Pay.

The capability must continue the already-implemented commercial sourcing chain:

Purchase Request
→ approval
→ Supplier Quotations
→ comparison
→ Source Decision
→ Purchase Order
→ Supplier Confirmation

MESP-124 must NOT create:

stock
Goods Receipt
Purchase Invoice
AP
payment
accounting posting.

Supplier confirmation remains a Procurement commercial commitment/evidence
capability only.

## Mandatory Architecture Inputs

Read completely before implementation:

AGENTS.md
CLAUDE.md
.ai/CURRENT_STATE.md
TASK.md

Then relevant Procurement BRD:

docs/21_Procurement_and_Purchase_to_Pay_BRD.md

Then:

docs/ADR-019_Tenant_Host_Resolution_Workspace_Context_and_Branding.md
docs/MESP-143_Tenant_Aware_Entry_Execution_Plan.md

Review relevant decision evidence for:

MESP-42 — purchase approval workflow — Done
MESP-43 — supplier confirmation / partial confirmation — Done
MESP-55 — delegation / approval behavior — Done

Inspect current MESP-123 implementation in full before designing duplicate
capability.

## Permanent Product Rules

Generic SaaS only.

Never:

if tenant == Wafra

No Wafra-specific:

workflow
schema
permission
route
API
report
approval
supplier logic.

Tenant != Workspace.

MESP-143 host/Tenant authority remains mandatory for all new PO/confirmation
operations.

Company/Branch scope remains server-authoritative.

## Source Decision Prerequisite

A Purchase Order must originate only from an eligible approved sourcing result
according to the approved Procurement policy.

Do not allow the Angular client to invent:

Supplier
currency
commercial source
Purchase Request
quotation
selected lines

without server validation.

Persist immutable source lineage:

Purchase Request
Supplier Quotation
Source Decision
Supplier
currency
selected commercial facts.

## Purchase Order Lifecycle

Determine exact lifecycle from approved BRD/decision evidence.

Expected bounded concepts include:

Draft
Submitted / Pending Approval where policy requires
Approved
Issued
Supplier Confirmation pending
Confirmed / Partially Confirmed / Changed / Rejected
Cancelled where eligible

Do not invent lifecycle states if approved source uses different terminology.

Define state transitions explicitly.

No transition may silently bypass approval or SoD.

## Purchase Order Draft

Support creating a PO from the approved sourcing decision.

Snapshot relevant source facts:

Tenant
Company/Branch scope
Supplier
currency
Product
UOM
ordered quantity
commercial price
tax selection/evidence
payment term
requested/committed dates where defined
source quotation
source decision
source PR
commercial evidence reference.

Do not silently re-resolve mutable master-data values after order creation when
audit evidence requires snapshots.

## Lines

PO lines must retain deterministic source lineage.

Support only the approved PO behavior.

Do not introduce:

free arbitrary supplier-line divergence

unless approved by the BRD.

If quantity/price/date changes are permitted before issue:

control them through document version/concurrency and approval policy.

## Approval

Reuse the existing reusable approval capability.

Do NOT implement another independent approval engine.

Preserve:

configuration-led policy
SoD
no self-approval
delegation seams
approval evidence
policy snapshot
history
audit
server capability flags.

PO approval must use exact relevant PO version.

## Issue / Commit

An Approved PO may be issued/committed according to the approved workflow.

Issuing the PO means:

commercial commitment evidence.

It does NOT mean:

stock received
AP created
invoice posted
payment created
accounting posted.

No downstream side effect.

## Supplier Confirmation

Implement MANUAL supplier-confirmation capture.

No supplier portal/integration is required unless already approved elsewhere.

Confirmation must support approved cases such as:

full confirmation
partial quantity confirmation
supplier rejection
changed quantity
changed date
changed price
no response / pending evidence

according to MESP-43.

Do not invent auto-confirmation.

## Partial Confirmation

Preserve per-line state.

Example concept:

ordered 100
confirmed 60
unconfirmed 40

must not be collapsed into:

confirmed 100.

Track exact commercial remainder.

Do not create stock reservations or receipts from confirmation.

## Supplier-Initiated Changes

For supplier-proposed changes to:

quantity
price
delivery date

apply the approved MESP-43 behavior.

Where reapproval is required:

the PO must return to the correct controlled state.

Do not silently mutate an approved/issued commercial commitment.

Persist:

previous value
proposed/accepted value
reason/evidence
actor
timestamp
resulting approval consequence.

## Reapproval

If a supplier change crosses a reapproval condition:

invalidate/supersede prior approval evidence as appropriate without deleting it.

Create new approval evidence.

Preserve complete history.

Do not overwrite old approval records.

## Rejection

Supplier rejection must be explicit and auditable.

Do not:

delete the PO
silently cancel upstream PR
silently choose another quotation
automatically create another PO

unless an approved rule explicitly requires it.

Return usable sourcing state/evidence for subsequent user decision.

## Confirmation Evidence

Support bounded evidence references/attachments using existing project seams.

Do not implement a new external document service.

Evidence may include:

supplier confirmation reference
supplier document number
communication reference
notes
attachment metadata

according to current platform support.

## Concurrency

All editable/mutating PO and confirmation operations require proper optimistic
concurrency.

Use actual document version/ETag semantics.

Stale client:

409 concurrency_conflict

No last-write-wins.

Failed conflict must:

not mutate PO
not mutate confirmation
not append false history
not append false audit success.

## Idempotency

All unsafe commands that can be retried must follow the project's idempotency
contract.

Duplicate Idempotency-Key:

same request
→ replay-safe deterministic result.

Different payload with same key:
→ conflict according to current standard.

Do not create duplicate PO/confirmation events.

## Audit / History

Persist immutable evidence for:

creation
edit
submit
approval
return/reject where applicable
issue
confirmation
partial confirmation
supplier rejection
supplier-proposed change
reapproval
eligible cancellation
concurrency rejection where project's audit policy requires.

No history rewriting.

## Tenant Security

Every PO/confirmation query/command must enforce:

MESP-143 candidate-host/Tenant authority
exact Tenant membership
organization scope
server authorization
Tenant ownership.

Tenant A actor must never read/write Tenant B PO.

Client Tenant IDs cannot widen scope.

## Company / Branch

Honor current organization-scope model.

Do not require raw GUID UX.

Use server-resolved human-readable organization choices.

PO organization scope must remain immutable after commitment where approved
business meaning requires it.

## Permissions

Use explicit Foundation permissions.

At minimum distinguish appropriately:

read
create/edit
submit
approve
issue
supplier-confirmation capture
cancel

Do not treat UI visibility as authorization.

## REST / OpenAPI

Every new public operation must:

exist in Foundation operation catalogue
have stable operationId
have useful Summary
have security/permission metadata
have antiforgery classification
have idempotency classification
have audit classification
have concurrency documentation
appear truthfully in generated OpenAPI/Scalar.

Do not repeat the MESP-143 P3 generic "Use Auth" summary problem.

## Angular

Implement complete bilingual PO workspace:

list
create
edit where allowed
detail
history/audit
approval actions
issue action
supplier confirmation
partial confirmation
supplier-change review
reapproval status
rejection status.

Use server-provided canX flags as authoritative.

Do not infer lifecycle authority client-side.

## PO List

Provide practical bounded filters:

PO number/reference
Supplier
status
date where current design supports it
organization context

Do not load an unrestricted cross-Tenant catalogue.

## Create UX

Create from eligible sourcing/source decision.

Do NOT require users to manually type:

Purchase Request GUID
Supplier Quotation GUID
Source Decision GUID
Supplier GUID
Company GUID
Branch GUID.

Use server-provided business choices and labels.

## Detail UX

Show:

PO identity
Supplier
organization
currency
commercial totals
lines
source references
approval
issue state
confirmation state
supplier-change state
history/audit.

Technical GUIDs may appear only as secondary technical references.

## Confirmation UX

Allow users to record supplier response per approved business rules.

For partial confirmation provide clear ordered / confirmed / remaining values.

For supplier-proposed changes clearly distinguish:

original
supplier proposed
accepted/current
approval consequence.

Never hide changed commercial terms.

## EN / AR / RTL

All new UI:

English
Arabic
RTL/LTR.

No raw translation keys.

Use correct Procurement terminology.

Do not equate Tenant with Workspace.

## Accessibility

Keyboard navigation
focus
labels
headings
native controls where suitable
not color-only
error announcements
accessible line-grid/actions.

## Responsive UX

Validate:

desktop
medium
narrow.

Do not break commercial tables/actions on common business laptop resolutions.

## MESP-143 Regression

Explicitly preserve:

Tenant-host entry
common host
Overview-first flow
operational context
host authority
branding fallback
SAR presentation.

PO implementation must not bypass the MESP-143 server context model.

## MESP-123 Regression

Preserve:

Purchase Requests
approval
Supplier Quotations
comparison
mixed-currency no-FX
source decisions
source-decision concurrency
history/audit.

PO must consume those facts, not rewrite them.

## Database

Use SQL Server runtime through:

MESP_SQLSERVER_CONNECTION_STRING

Formal EF migration if persistence/schema is added.

Correct module ownership.

No EnsureCreated production shortcut.

No SQLite authority.

No destructive reset.

## SQL Safety

Use ONLY:

scripts/Test-MiniErpBackend.ps1

for final full backend evidence.

This runner creates the disposable:

MESP_SQLSERVER_SAFETY_CONNECTION_STRING

target automatically.

Do not treat gated SQL safety as green.

Persistent MESP must never be used as the destructive safety target.

## Backend Validation

Run:

dotnet build .\backend\MiniErp.sln --configuration Release --no-restore --verbosity minimal

Then:

.\scripts\Test-MiniErpBackend.ps1

Starting accepted baseline:

770/770

Final suite will increase.

Required:

ALL PASS
0 skipped SQL safety cases
disposable cleanup PASS.

## Angular Validation

Run:

npm test -- --watch=false --no-progress
npm run build
npm audit --omit=dev

Starting baseline:

204/204
23 spec files
490.85 kB initial
91.94 kB Supplier Quotation lazy
0 vulnerabilities.

Do not raise budgets merely to pass.

## Playwright

Extend E2E.

Starting baseline:

8/8.

Cover at minimum:

PO create from eligible source decision
approval / issue
full confirmation
partial confirmation
supplier rejection
supplier-proposed change/reapproval path
stale concurrency behavior
Tenant/organization denial
EN/AR smoke
MESP-123 regression.

Use deterministic fixtures.

## Browser Validation

Use Chromium/Playwright for visual/runtime proof.

Clearly distinguish:

automated E2E
manual interactive browser
API/HTTP evidence.

Do not claim manual browser review if not performed.

## Scope Exclusions

DO NOT implement:

Goods Receipt
stock mutation
warehouse movement
Purchase Invoice
AP
payment
accounting posting
three-way matching
supplier portal
external supplier integration
ZATCA/FATOORA
production DNS/TLS
MESP-125
MESP-126
MESP-127
MESP-128+
customer-specific Wafra logic.

## Git

Start from synchronized main after MESP-143 closure.

Create:

feat/MESP-124-purchase-order-confirmation

Open one Draft PR against main.

Suggested title:

feat(MESP-124): implement purchase order and supplier confirmation

No force push.

No merge.

## Jira

ZERO Jira writes.

GPT-5.6 Sol owns Jira.

Return:

activation evidence
implementation evidence
validation evidence
remaining gaps
review recommendation.

## Documentation

At completion update current/live:

AGENTS.md
CLAUDE.md
.ai/CURRENT_STATE.md
TASK.md
README.md
Run.md
backend/README.md
frontend/README.md
docs/staticts.md

and genuinely affected architecture/Procurement docs.

Preserve historical evidence.

## TASK.md Lifecycle

If MESP-124 implementation is complete and fully validated:

replace TASK.md with the FULL prompt for:

CLAUDE OPUS 5
INDEPENDENT MESP-124 PRE-MERGE REVIEW

The review must cover:

PO lifecycle
source lineage
approval/SoD
supplier confirmation
partial confirmation
changed commercial terms
reapproval
Tenant isolation
concurrency
idempotency
history/audit
no stock/AP/accounting effects
EN/AR/RTL
SQL safety
MESP-143 and MESP-123 regressions.

If MESP-124 is incomplete:

TASK.md must instead contain the FULL next bounded implementation prompt.

Never leave TASK.md pointing to completed MESP-143 work.

## Final Report

Return:

# MESP-124 Purchase Order + Supplier Confirmation: IMPLEMENTATION REPORT

with:

Repository
PR
PO lifecycle
Source lineage
Approval
Issue
Supplier confirmation
Partial confirmation
Supplier changes
Reapproval
Tenant/organization security
Concurrency/idempotency
Audit/history
REST/OpenAPI
Angular
EN/AR/RTL/accessibility
Database/migrations
Backend validation
Angular validation
Playwright/browser validation
MESP-143 regression
MESP-123 regression
Scope exclusions
Git
Jira payload
Next exact session

Then STOP.

Do NOT merge.
Do NOT update Jira.
Do NOT start MESP-125.
