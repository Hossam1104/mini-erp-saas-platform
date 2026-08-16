# CLAUDE OPUS 5 — INDEPENDENT MESP-123 CAPABILITY REVIEW

## Mission

You are the independent reviewer for the completed bounded MESP-123
Purchase Request, approval, Supplier Quotation, comparison, and source-decision
capability. Review the complete branch before anyone merges or closes the
capability. Produce an evidence-backed verdict and a prioritized finding list.

Do not automatically merge the pull request. Do not transition Jira issues,
post Jira comments, or broaden the implementation. If a serious defect is
found, report it with exact evidence, impact, and the smallest safe correction
recommendation.

## Repository and delivery state

- Repository: `D:\AI Tools\Hossam\mini-erp-saas-platform`
- Branch: `feat/MESP-123-purchase-request-approval`
- Pull Request: Draft PR #66 against `main`; it must remain open, Draft, and
  unmerged during this review.
- Capability: MESP-123 — Purchase Request, approval, Supplier Quotation,
  comparison, and source decision.
- Product: generic reusable multi-tenant B2B ERP; the legacy Wafra repository
  is visual reference only.
- Next review output: independent verdict for GPT-5.6 Sol / Owner decision.

## Mandatory reading order

Read the current versions, not historical assumptions, of:

1. `AGENTS.md` and `CLAUDE.md`;
2. `.ai/CURRENT_STATE.md`;
3. this `TASK.md`;
4. `docs/staticts.md`;
5. `README.md`, `Run.md`, `backend/README.md`, and `frontend/README.md`;
6. the approved Procurement BRD and relevant ADRs/contracts/entities;
7. the complete PR #66 diff and current branch status.

Inspect the legacy Wafra repository only as a read-only visual reference. Do
not modify it, copy its branding, copy its IDs/data, or introduce any
customer-specific branch into MESP.

## Review boundaries

The review covers the connected capability already present on the branch:

- Purchase Request creation, edit, submission, approval, rejection/return,
  cancellation, organization scope, lineage, and server-derived authority;
- Supplier Quotation list, approved-request create, Draft edit, detail,
  evidence references, server-resolved Supplier/Currency/Tax/Payment Term
  references, and lifecycle actions;
- Submit, Withdraw, Disqualify, optimistic concurrency, idempotency, safe
  errors, history, audit, and server capability flags;
- comparison totals/coverage/qualification issues, same-currency groups,
  mixed-currency grouping, explicit no-FX treatment, and the absence of a
  client-invented winner;
- current source selection, required rationale, comparison snapshot,
  supersession/history, and audit evidence;
- Tenant isolation, Company/Branch organization scoping, exact-Development
  authentication convenience, authorization, antiforgery, and public REST
  operation documentation;
- Angular EN/AR, RTL/LTR, accessibility, keyboard-safe dialogs/tabs,
  responsive layouts, loading/empty/error/retry states, light/dark styling,
  and generic Wafra-inspired ERP density;
- SQL Server Development runtime/provider behavior, migrations, persistence,
  and relevant row/audit evidence;
- regression tests, Playwright, generated OpenAPI, README, current-state,
  statistics, PR readiness, and scope discipline.

The following are explicitly outside this review’s implementation scope and
must not be started: Purchase Order, supplier confirmation, Goods Receipt,
Purchase Invoice, AP, accounting, payment, stock mutation, supplier portal,
external integrations/providers/credentials, migration cutover changes,
MESP-124, Retail POS, and Wafra-specific behavior.

## Review procedure

### 1. Establish clean evidence

Run read-only checks first:

```powershell
git status --short
git branch --show-current
git rev-parse HEAD
git log -5 --oneline
git diff main...HEAD --stat
git diff --check
git status --short -- frontend/assets
```

Confirm that no Owner-managed file under `frontend/assets` changed and no
generated build output, database backup, credential, log, SQLite/MDF/LDF file,
or Spec Kit artifact entered the PR.

### 2. Validate the contract boundary

Trace each public Supplier Quotation and source-decision route from:

1. Foundation operation catalogue metadata;
2. actual API endpoint mapping;
3. generated OpenAPI document and stable `operationId`;
4. architecture/contract test;
5. Angular service call and UI state.

Pay special attention to the persisted source-decision history read route,
Tenant scope, permission, antiforgery/unsafe-effect metadata, response
documentation, and whether Angular has duplicated any backend business rule.
Reject placeholder or handwritten second contracts.

### 3. Review business and security invariants

Verify from code and tests that:

- only eligible Approved Purchase Requests can be sourced;
- request/line/Product/UOM/quantity/need-by facts are server snapshots;
- Supplier, Currency, Tax, and Payment Term values are server-resolved;
- no client-provided Tenant, Company, Branch, actor, role, permission, or
  identity can expand authority;
- all reads/writes are Tenant and organization scoped as designed;
- lifecycle actions are gated by server-derived capability flags and backend
  policy, not by Angular-only assumptions;
- edits and source decisions use the correct If-Match version;
- unsafe mutations send idempotency keys and antiforgery headers;
- concurrency conflicts remain safe and recoverable;
- source selection requires a nonblank rationale and records comparison
  snapshot/policy/history evidence;
- mixed currencies never receive client FX conversion or cross-currency
  ranking/winner implication;
- audit/history is append-before-effect and does not expose unsafe technical
  authority to the browser.

### 4. Review the Angular journey as a user

Use the real Development runtime and a real browser where available. Verify
the following without typing or interpreting database GUIDs:

- sidebar navigation reaches `/app/procurement/supplier-quotations`;
- list search/status/currency filters operate only on bounded loaded data;
- empty, filtered-empty, loading, unauthorized/unavailable, and retry states
  are honest and usable;
- create begins from an Approved Purchase Request selector and renders
  human-readable organization, Supplier, Currency, Tax, and Payment Term
  labels;
- line facts remain read-only server lineage while commercial values are
  entered explicitly;
- Draft save/edit, evidence add/remove, submit, withdraw, and disqualify
  affordances match server capability flags;
- detail tabs expose summary, lines, commercial terms, evidence, comparison,
  lifecycle history, audit, and technical reference without making technical
  IDs primary business labels;
- comparison groups by currency, displays server totals/coverage/issues, and
  communicates no-FX/mixed-currency boundaries;
- source-decision radio selection, rationale, current selection, and history
  are clear; reselection/supersession is not claimed unless exercised;
- dialogs are labelled, keyboard usable, non-destructive by default, and safe
  under in-flight requests;
- EN/AR and RTL/LTR preserve meaning, focus, table readability, and action
  placement; narrow widths and reduced-motion preference remain usable;
- light and dark presentation is coherent and no Wafra branding is present.

### 5. Run the bounded validation suite

Use the repository’s pinned tooling and record exact counts, warnings, and
failures. Prefer Release configuration for backend validation:

```powershell
dotnet build .\backend\MiniErp.sln --configuration Release --no-restore --verbosity minimal
dotnet test .\backend\MiniErp.sln --configuration Release --no-restore --verbosity minimal
cd .\frontend
npm test -- --watch=false
npm run build
npm run test:e2e
npm audit --omit=dev
```

The SQL safety suite must be run with the explicitly configured local SQL
Server `MESP_SQLSERVER_CONNECTION_STRING` when available. Never print the
connection string or credentials. If SQL is unavailable, classify the result
as environment-gated rather than as a product pass.

### 6. Inspect runtime and persistence evidence

Restart only through `scripts/Start-MiniErpDevelopment.ps1` after the final
reviewed source is built. Expected local endpoints are MiniERP API 5300 and
Angular 4300; leave unrelated RMS 5000/5001 untouched. Verify health,
authenticated session/context, quotation routes, comparison/source-decision
routes, OpenAPI operation presence, and the real SQL Server provider. If a
supported Development journey creates or changes records, verify the
corresponding quotation, decision, snapshot, history, and audit rows in the
`MESP` database without exposing secrets.

### 7. Review documentation and PR readiness

Confirm that:

- `README.md` describes the actual product and bounded status without
  claiming production readiness;
- `docs/staticts.md` is conservative and records the current evidence;
- `.ai/CURRENT_STATE.md` points to the exact current branch/PR and next step;
- `TASK.md` remains this review prompt and no future capability started;
- PR #66 retains historical Phase A/B/B1/C/B2 evidence and the new UI section;
- no Jira operation or status transition was silently performed;
- scope exclusions remain explicit.

## Verdict format

Return a review report with:

1. Verdict: `APPROVE FOR MERGE`, `CHANGES REQUIRED`, or `BLOCKED`;
2. exact reviewed SHA and PR state;
3. evidence summary by backend/API, security/Tenant, persistence/SQL,
   Angular/UX, tests/browser, documentation, and scope;
4. findings ordered P0/P1/P2/P3 with file/line or command evidence;
5. explicit statement whether any finding risks Tenant leakage, accounting or
   stock integrity, data loss, unsafe migration, credential exposure, or legal
   misrepresentation;
6. merge recommendation and the smallest required follow-up;
7. confirmation that the next implementation must not begin automatically.

Do not merge, force-push, rewrite history, change Owner assets, modify Wafra,
activate Jira work, or start Purchase Order/downstream work as part of this
review. The review is the next exact session; stop after publishing the
evidence-backed verdict for GPT-5.6 Sol and the Owner.
