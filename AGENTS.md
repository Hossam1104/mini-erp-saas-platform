# Repository Working Agreement

## Current checkpoint boundary

MESP-136 is accepted, merged, and Jira-closed. No implementation capability is
active. MESP-137 (reservation, partial fulfillment, Delivery, and Sales
Invoice), MESP-138, MESP-139, and later capabilities remain inactive until
GPT-5.6 Sol explicitly activates one bounded task.

MESP-48 (reference tenant volume) and MESP-50 (data residency and retention)
remain open production-readiness gates. Functional fast-track completion and
production readiness are separate measures. The current handoff is recorded in
`TASK.md` and `.ai/CURRENT_STATE.md`.

## Project statistics tracker

`docs/staticts.md` is the tracked living source of truth for production
progress. At the end of every bounded session that materially changes project
progress, implementation state, Jira counts, phase completion, blockers,
velocity, or forecast:

1. Read the current tracker.
2. Update it directly with conservative, validated capability evidence.
3. Update `Last Updated` and `Progress History` when applicable.
4. Commit and push the tracker update with the session.

Do not create `docs/statistics.md` or attach a separate tracker copy.

## Owner-managed asset protection

Files under `frontend/assets` are product source assets. Never delete, rename,
replace, regenerate, optimize, recolor, move, or restore them from Git without
explicit Owner instruction. Do not assume an untracked image there is
temporary. Full logos and icons use that directory as their source of truth;
`frontend/assets/brand` is reserved for necessary generated browser
derivatives.

## Safe execution and source control

- Verify branch, `HEAD`, worktree status, remotes, fetched `origin/main`, and
  relevant open PRs before mutation.
- Start from a clean worktree and a dedicated branch based on verified
  `origin/main`. If unrelated user changes are present, stop; do not reset,
  discard, stash, overwrite, rebase, or force-push them.
- Keep one bounded capability or checkpoint active at a time. Do not activate
  the next task automatically. Draft PRs remain Open/Draft/Unmerged until an
  independent authorized acceptance and merge.
- Preserve public routes, operation IDs, schemas, authorization, antiforgery,
  Tenant scope, audit, idempotency, optimistic concurrency, financial values,
  inventory quantities, migrations, and error contracts.
- A zero text-search result is not proof that a class, method, route, DTO,
  migration, localization key, or script is unused. Prove absence through
  semantic references, registrations, reflection/serialization, routing,
  generated contracts, tests, configuration, history, and public compatibility.
  When uncertain, keep it.
- Do not add external providers, credentials, retries/model fallback,
  production infrastructure, Jira governance transitions, or later feature
  behavior unless explicitly authorized by the bounded task.

## SQL safety

Destructive SQL tests may use only the repository safety runner and an isolated
`MiniErpFoundation_*` database through
`MESP_SQLSERVER_SAFETY_CONNECTION_STRING` on disposable LocalDB. Never point
the safety harness at the persistent `MESP` database. If the disposable
environment is unavailable, report SQL validation as GATED rather than
substituting another database or inventing a result.

## Ponytail

Use Ponytail FULL when available. It governs productivity, not safety: prefer
the smallest proven change, existing helpers, standard-library/native
features, and deletion of proven waste. Never simplify away validation,
Tenant isolation, authorization, accounting/inventory integrity, audit,
concurrency, idempotency, accessibility, or acceptance gates.

## Durable architecture rules (ADR-019)

### Tenant and operational context

Tenant is the SaaS security and data-isolation boundary, resolved and
authorized server-side before Tenant business data is accessible. Tenant is
not a user-selectable ERP workspace filter. An operational context exists
inside an authorized Tenant and represents approved Company/Branch scope.
Single-context users are auto-selected; multiple contexts use a server-backed
header switcher; never require raw GUID entry.

### Entry and host resolution

Hostnames provide candidate routing information, not authorization. The
tenant-host flow is `Host -> candidate Tenant -> authentication -> exact
membership authorization -> server-owned Tenant context -> Overview ->
optional Company/Branch context`. A common host chooses only among active
memberships. Platform administration is a separate control plane; its
administrator role grants no Tenant ERP data access without an explicit,
audited support grant or membership.

### UX, branding, and SAR

Authenticated entry is Overview-first. Workspaces are inside the authorized
Tenant, and ordinary-user navigation does not expose a parallel "switch
workspace" hierarchy. Tenant branding is generic, configuration-led data;
missing branding falls back to MESP branding. The Wafra logo remains an
Owner-managed asset and must never become branch-specific code. Saudi Riyal
presentation is a country-pack display concern only: it does not change FX,
tax, accounting, persisted amounts, or non-SAR formatting.

## Module and API rules

The backend dependency direction is `Api -> App -> Contracts`, with `Api ->
Infrastructure` for composition and `Infrastructure -> App/Contracts` for
module-owned persistence implementations. Keep business behavior in module
application services, contracts in Contracts, persistence behind module
interfaces, and endpoint registration in cohesive endpoint files.

Each public REST operation must have an exact route, stable operation ID,
permission, scope, antiforgery, audit, unsafe-effect metadata, explicit
response outcomes, generated OpenAPI documentation, and architecture/contract
coverage. Scalar is a developer-facing rendering of generated OpenAPI, not a
second public contract.

## Delivery and validation

Every fresh session executes the current root `TASK.md`, reviews the complete
diff, updates genuinely affected state, validates in proportion to risk, and
stops for review. Do not merge from an executor session unless separately
authorized. Before handoff, run the Release build, affected and full backend
tests, disposable SQL safety tests when available, EF model checks, frontend
unit/build/browser checks, dependency audits, OpenAPI/security checks, and
`git diff --check`. Report warnings and missing CI honestly.
