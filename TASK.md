# MESP-130 — Sol Delta Acceptance Handoff: Stock Control Remediation

Reviewer: GPT-5.6 Sol

Repository: `D:\AI Tools\Hossam\mini-erp-saas-platform`

Capability: MESP-130 — Stock Adjustment, Inventory Count, Stock Issue, and
eligible stock-movement corrections.

Branch: `feat/MESP-130-stock-control-corrections`

Exact required starting SHA: `88eac382213c86e9d816fee0232b9e917c5d104d`

Exact main base: `6f6d204726cc4baf9979961ea6936c0d03e93e32`

Remediation implementation SHA: `3320cf284d64a58be7fb0f00ac654ee7a11d7b00`

Final branch SHA: the final documentation/runtime handoff tip is reported in
the completion response because Git cannot embed a commit's own SHA in that
commit's content.

Draft PR: `#74` — Open, Draft, Unmerged; base `main`.

Jira: read-only. No Jira writes were performed. MESP-130 remains In Progress
until Sol acceptance. Do not mark the PR Ready, merge, rebase, force-push,
create another PR, or start downstream work.

## Remediation delivered

### P1 corrections

- Approval state: Adjustment and Stock Issue approvals now persist distinct
  current-stage approver identities, reject duplicate same-stage approval,
  advance only after the configured distinct-approval count is met, preserve
  delegation evidence, and fail closed for invalid or missing configured
  policy. Count variance approval uses the same configured policy seam while
  every non-zero variance remains approval-required; no threshold was invented.
- Blind count: counter submission carries only `CountLineId` and physical
  `CountedQuantity`; normal assigned-counter list/detail reads redact expected,
  variance, and derived values before submission. Reviewer reads expose
  Expected/Counted/Variance only after observation submission.
- Cutoff/full count: snapshot cutoff is established after anchors and the
  authoritative expected read; cycle staleness remains identity-scoped;
  full-count staleness is warehouse-scoped and detects new identities; zero
  variance passes final stale validation; full resnapshot preserves old rounds
  and adds the current warehouse identity universe.
- Correction uniqueness: formal SQL/SQLite-compatible migration adds a
  Tenant-scoped unique filtered index on non-null
  `CorrectionOfMovementId`; duplicate races classify deterministically rather
  than becoming generic persistence-unavailable failures.

### P2 corrections

- Tests: added executable multi-stage Adjustment/Issue approval, distinct
  approver, blind count, cutoff, full-count new-identity/resnapshot, and SQL
  correction-index regressions; expanded Angular and browser coverage.
- UI: completed the existing Stock Control workspace with reason catalogue
  list/create/edit/activate/deactivate, physical blind count entry, reviewer
  variance reason, approve/reject, recount, resnapshot, lifecycle history,
  eligible correction reason/linkage, and Adjustment/Issue create-submit-
  approve/reject-post controls. Return-for-change is not exposed because the
  bounded UI has no edit/resubmit contract; this avoids a dead-end action.
- Bundle: removed the unused `ApiClientService.put()` seam; final initial
  production bundle is `499.97 kB`, within the `<= 500.00 kB` warning budget.
- Reason validation: UpdateReasonCode now rejects blank English/Arabic names,
  undefined categories, invalid versions, and preserves immutable code and
  historical snapshots through the existing server contract.

## Validation evidence

- Focused Inventory/MESP-130 tests: `6/6` passed.
- REST/OpenAPI structural tests: `33/33` passed within the backend suite.
- SQL Server safety: `30/30` passed through the disposable LocalDB runner;
  the new correction test proves one direct correction, deterministic duplicate
  rejection, one persisted correction row, and the unique filtered index.
- Full backend suite: `903/903` passed, `0` failed, `0` skipped.
- Release build: `dotnet build .\backend\MiniErp.sln --configuration Release`
  passed with `0` warnings and `0` errors.
- Angular unit tests: `245/245` passed across `33` spec files.
- Focused MESP-130 Chromium: `1/1` passed.
- Full Chromium: `27/27` passed.
- Production bundle: initial `499.97 kB`; Inventory lazy chunk `69.05 kB`;
  Supplier Quotation lazy chunk `91.94 kB`.
- `npm audit --omit=dev`: `0` vulnerabilities.
- `npm audit`: `0` vulnerabilities.
- `git diff --check`: clean before the documentation-only handoff commit.

## Runtime verification

- Backend URL: `http://localhost:5300`.
- Frontend URL: `http://localhost:4300`.
- Backend PID: `23588`.
- Frontend PID: `39252`.
- Backend health: `GET /health` returned HTTP `200`.
- Frontend status: `GET /` and `GET /main.js` returned HTTP `200`.
- Both processes were verified alive after the probes and remain running for
  Owner inspection. The supported loopback Development bypass was used by the
  launcher; no credentials were printed or persisted.

## Migration and boundaries

- Formal additive migrations:
  `20260822220126_MESP130SolAcceptanceRemediation` and
  `20260822220521_MESP130SolAcceptanceCountApproval`.
- MESP-128 immutable ledger, deterministic anchors, Serializable posting,
  reservation protection, MESP-129 physical movement history, Tenant and
  operational-context authorization, Pending valuation, idempotency, audit,
  and history remain authoritative.
- No MWA, Finance, GL, AP, AR, tax, payment, Sales, Reporting, external,
  statutory/ZATCA/FATOORA, DNS/TLS, migration/cutover, supplier portal, or
  Wafra-specific core implementation was added.
- `frontend/assets` has zero changes. Owner-managed source assets remain
  protected.

## Next exact step

Sol performs delta acceptance against the exact final branch tip and Draft PR
#74. Do not start MESP-131 or any downstream implementation automatically.
