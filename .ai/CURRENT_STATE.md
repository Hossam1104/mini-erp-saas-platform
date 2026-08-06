# Current State

## MESP-91 correction overlay — merged and Done

- MESP-91 (`Enforce verified organization scope and worker authority revalidation in durable work`) is **Done**. No implementation item is currently active; MESP-92 is the next eligible correction.
- Branch: `fix/MESP-91-verified-work-scope-authority`, based on merged-main baseline `4eb1ef3ab094242cbb26ec9ab79b4037512e0d2d`; approved head `92bd9fd38912a062cc3723f46867258d54ca8127`; merged to `main` at `f2cde57400fed470ab048776e05b56f353b36890` (PR #20 normal merge). The branch was deleted after merge.
- The correction adds an Identity-owned verified Tenant -> Company -> Branch -> Warehouse resolver, authorization-context-bound scopes, and live worker/outbox authority revalidation immediately before handler/effect dispatch. Authority failure is a safe terminal `AuthorizationDenied` dead letter.
- PR #20 received a focused ChatGPT security review disposition of APPROVED TO MERGE (0 Critical, 0 High, 0 Medium blockers) before merge. MESP-31, MESP-92, MESP-93 and MESP-94 remain To Do; no Sprint is active and no next item was started before MESP-91 closure.
- No production provider, migration, broker, deployment, Retail POS, Wafra-core or ERP domain behavior is introduced. MESP-48 and MESP-50 remain explicit gates.

- Approved merged main baseline after MESP-91: `f2cde57400fed470ab048776e05b56f353b36890` (PR #20 normal merge; MESP-64/PR #18, MESP-61/PR #17, MESP-90/PR #16, MESP-89/PR #12 and MESP-63/PR #14 remain preserved in history).
- MESP-57: Done; Modular Monolith solution and module seam merged through PR #1.
- MESP-58: Done; trusted TenantContext and persistence isolation merged through PR #6, including the stored-owner security correction.
- MESP-87: Done; Tenant persistence guardrail hardening completed in the MESP-58 correction sequence.
- MESP-59: Done; authentication and authorization seam merged through PR #8 and reconciled after MESP-88/PR #9. Jira reconciliation comment: `10274`.
- MESP-88: Done; PR #9 merged at `723dc8e28b0a927750230b51b9d05e26d039038c`; the final reported baseline contained 161 passing tests.
- MESP-60: Done; PR #10 merged the bounded versioned REST/OpenAPI, trusted context, safe error, correlation, concurrency, idempotency and antiforgery foundation. No business transaction API is in scope.
- MESP-62: Done; immutable path-aware evidence, append-before-effect coordination, safe redaction, bounded telemetry hooks and the Foundation Backend Review Checkpoint package are merged.
- MESP-89: Done; PR #12 merged at `a1c5627b40e11b14a50736663c6da56cf11c9ef8` after focused ChatGPT approval and merged-main validation.
- MESP-63: Done; Angular 22 Wave 1 shell implementation merged through PR #14 at `ad9e6a7c40d229b564a7232ca62b3d70ec1fdc15` after the MESP-89 reconciliation cleanup.
- MESP-90: Done; the exact approved head was merged through PR #16 at `469ab863a5fc20f02d3ba674a97dceb969bbec75` after focused ChatGPT approval. MESP-63 remains Done and was not reopened.
- MESP-61: Done; PR #17 merged to `main` at `7db49a88e11232f055c2016b8bb033a61de629ec` after the typed durable-work/private-file foundation and merged-main validation.
- MESP-64: Done; PR #18 merged to `main` at `2002d1c25d39022b227e89b3d70f41a53de0408c` after disposable SQL Server LocalDB validation and merged-main regression.
- MESP-91: Done; PR #20 merged to `main` at `f2cde57400fed470ab048776e05b56f353b36890` after focused ChatGPT security review approval and merged-main validation. No implementation item is active; MESP-92 is the next eligible correction and no Sprint is active.
- No Sprint is active; MESP-63 was delivered outside a Sprint.
- MESP-48 and MESP-50 remain explicit performance, retention, privacy, legal-hold, purge, residency, backup and restoration production gates.
- No physical migration, production/shared database, durable audit provider, OpenTelemetry exporter, production worker, file-storage provider, deployment, Retail POS or future ERP transaction implementation was introduced. MESP-63 is limited to the Angular shell and does not implement business transactions.
- Current state: MESP-57, MESP-58, MESP-87, MESP-59, MESP-88, MESP-60, MESP-62, MESP-89, MESP-63, MESP-90, MESP-61, MESP-64 and MESP-91 are merged and closed in the repository baseline; no implementation item is currently active.
- MESP-63 implementation baseline: commits `798d15d1aa1e53781df3a2683305e95ac3143890` and `46bf2d30f91ef00e9e450b59b8de0b3a2d34dbab` were merged through PR #14 at `ad9e6a7c40d229b564a7232ca62b3d70ec1fdc15`. The Angular 22/TypeScript standalone workspace provides modular core/features/shared structure, server-issued cookie session bootstrap, in-memory antiforgery token, server-confirmed context loading/switching, bilingual EN/AR direction switching, responsive accessible shell and safe state components. Focused Angular tests pass 8/8; the mocked Playwright Wave 1 smoke journey passes 1/1; production deployment and provider work remain excluded.
- MESP-89 merged-main validation: Release build passed with 0 warnings and 0 errors; the complete solution suite passed 247 tests with 0 failures and 0 skips, including 17 direct/HTTP production-graph host-security tests and the endpoint metadata/coordinator guard. The merged correction covers catalog-backed exact operation permissions, mandatory protected-write evidence, composite idempotency replay and separate eligibility/selection versions.
- Production limitations remain explicit: in-memory Identity/session, local append-only audit seam, local idempotency, unavailable MFA/fresh-auth provider, no SQL migration or production provider selection, no durable exporter, no deployment work. MESP-64 provides disposable LocalDB/provider evidence only; MESP-48 and MESP-50 remain production gates.

## Completed MESP-90 security correction

- MESP-63 remains **Done**; it is not reopened.
- MESP-90 (`Prevent false logout when server session revocation fails`) is **Done** and is no longer active.
- Branch: `fix/mesp-63-signout-fail-closed`; PR #16 is merged to `main` at `469ab863a5fc20f02d3ba674a97dceb969bbec75` by normal merge after focused ChatGPT approval.
- The Angular correction preserves the authenticated session, selected context and current route when sign-out is unconfirmed; only confirmed HTTP 204 or server-confirmed HTTP 401 clears local state and navigates to `/login`.
- Validation record: 27 Angular unit/component tests passed; 4 Playwright journeys passed; backend scope is unchanged and the existing 247-test/0-warning/0-error baseline remains the required regression gate.
- No backend contract, provider, migration, database, business-domain, Retail POS, Wafra-core, MESP-61 or MESP-64 implementation work was introduced by MESP-90. No Sprint is active.

## Completed MESP-61 durable-work foundation

- MESP-61 is **Done**. Branch `feature/mesp-61-durable-work-private-files` was
  based on merged main `469ab863a5fc20f02d3ba674a97dceb969bbec75` and PR #17
  merged to `main` at `7db49a88e11232f055c2016b8bb033a61de629ec`.
- The bounded scope adds typed Tenant-aware durable-work identity, organization
  scope, initiator, lifecycle, lease, retry, dead-letter and optimistic
  concurrency contracts; a deterministic local relational outbox/inbox store;
  a typed dispatcher and one-item worker seam; provider-neutral notification
  intents/local adapter; and a private-file metadata/access/local adapter
  boundary.
- MESP-91 extends this merged seam with Identity-issued verified organization
  scope and live worker/outbox authority revalidation. This correction is now
  a merged-main capability (PR #20, `f2cde57400fed470ab048776e05b56f353b36890`).
- Local adapters are test/development seams only. No broker, production
  notification provider, object-storage provider, production SQL provider,
  migration, retention, residency, legal-hold, purge, scanning or deployment
  behavior is selected. MESP-48 and MESP-50 remain explicit gates.
- Merged-main validation passed: backend Release build 0 warnings/0 errors and
  285 backend tests; Angular 27 tests, Playwright 4 journeys and production
  dependency audit also passed. No production provider, migration, purge or
  later ERP work was introduced.

## Completed MESP-64 foundation safety harness

- MESP-64 is **Done**. Branch `feature/mesp-64-foundation-safety-harness` was
  based on merged main `7db49a88e11232f055c2016b8bb033a61de629ec`; PR #18
  merged to `main` at `2002d1c25d39022b227e89b3d70f41a53de0408c`.
- ADR-018 defines the current-machine SQL Server LocalDB strategy: one
  disposable `MiniErpFoundation_*` database, Windows integrated authentication,
  fixture cleanup, no committed secret and no production/shared database.
- The harness adds provider-specific schema/index/rowversion/collation,
  Tenant-filter, stored-owner, relationship, transaction, idempotency and
  lease probes, plus the exact 75-assertion evidence report in
  `docs/96_Foundation_Release1_Safety_Validation.md`.
- Docker/Testcontainers CI compatibility, production sizing, migrations,
  retention, residency, legal hold, purge, provider selection and deployment
  remain deferred. MESP-48 and MESP-50 are explicit production gates. No
  implementation item or Sprint is active and MESP-31 through MESP-40 remain
  outside scope.

## Foundation Completion Opus 5 checkpoint

- `docs/97_Foundation_Completion_Review_Checkpoint.md` records the complete
  sequential Foundation chain from MESP-57 through MESP-64, its PR/merge
  evidence, test totals, capability status, exact maturity boundaries and
  remaining production gates.
- The checkpoint is the historical documentation baseline. MESP-91 is merged
  and Done through PR #20; its merge does not authorize MESP-31, packages 2/3,
  Master Data/Catalog work, a Sprint, MESP-48/MESP-50 implementation or
  production deployment.
- MESP-48 and MESP-50 remain explicit production gates; no core ERP BRD is
  implemented and no implementation item is currently active. MESP-92 is the
  next eligible correction.

## MESP-91 focused correction overlay — merged and Done

- The focused correction is implemented in source/test commit
  `4ed4b0588b613d492ce6c446ae963001b28f0eca`, with final evidence recorded
  through approved head `92bd9fd38912a062cc3723f46867258d54ca8127` on the
  merged `fix/MESP-91-verified-work-scope-authority` branch. It closes H91-03 by requiring
  OrdinaryMembership revalidation to receive a canonical explicit
  `Tenant:GUID`, `Company:GUID`, `Branch:GUID` or `Warehouse:GUID` scope;
  missing, malformed, marker, broader and sibling scopes fail closed. A
  SupportGrant context does not authorize from its display marker; its current
  case-bound stored SupportGrant scope remains authoritative.
- H91-04 is closed by one reusable exact-binding validator covering WorkItemId,
  Tenant, operation descriptor, correlation, exact Company/Branch/Warehouse
  boundary, execution TenantContext scope, authorization path, Membership or
  SupportGrant, actor and session. DurableWorkExecutionContext repeats the
  same defensive check. Only the Identity issuer is allowed by the structural
  architecture test to issue shipping verified authority, and the operation
  descriptor's mandatory security-evidence flag cannot be bypassed at work
  creation, handler registration, dispatch or live revalidation.
- The focused durable-work and authority regression set passes **102/102** with
  zero skips. The complete Foundation validation on this overlay passes
  **360/360** backend tests, **11/11** SQL Server LocalDB probes, **27/27**
  Angular tests, **4/4** Playwright journeys, Release build with 0 warnings
  and 0 errors, and production dependency audit with 0 vulnerabilities.
- SQL evidence used the disposable `MSSQLLocalDB` instance with Windows
  integrated authentication; the LocalDB/model collation observed during the
  run was `SQL_Latin1_General_CP1_CI_AS`. No `MiniErpFoundation_*` test
  database remained after teardown, both pre-merge and on merged `main`.
- PR #20 was approved by focused ChatGPT security review (APPROVED TO MERGE;
  0 Critical, 0 High, 0 Medium blockers) and merged by normal merge commit at
  `f2cde57400fed470ab048776e05b56f353b36890`. MESP-91 is **Done**; MESP-92 is
  the next eligible correction; MESP-93 and MESP-94 remain **To Do**; MESP-31,
  Master Data implementation, Sprint work, production providers, migrations,
  MESP-48 and MESP-50 work remain outside this correction.
