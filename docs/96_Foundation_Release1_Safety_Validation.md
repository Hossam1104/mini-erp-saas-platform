# Foundation Release 1 Safety Validation

| Field | Value |
|---|---|
| Status | Foundation checkpoint; not a production-readiness approval |
| Jira | MESP-64 — Local and critical-flow test harness |
| Catalogue | Foundation Release 1 Lean Implementation Specification v0.4, section 48 |
| Environment | SQL Server 2025-compatible LocalDB instance MSSQLLocalDB; disposable MiniErpFoundation_* database per run; Windows integrated authentication |
| Evidence date | 4 August 2026 |
| Production/shared data | Not accessed |

## Outcome

The deterministic command `scripts/validate-foundation.ps1` started the installed
LocalDB instance, created one disposable database, ran the targeted SQL Server
suite and the full backend regression, and removed the database in fixture
cleanup. The targeted suite passed **11/11** with zero skips. The complete
backend suite passed **296/296**, with zero failures and zero skips. Release
build passed with zero warnings and zero errors.

The Foundation checkpoint has **53 executable foundation assertions PASS**,
**21 assertions NOT APPLICABLE** because their approved Tenant lifecycle,
export, numbering or later-domain workflow is not implemented in this bounded
checkpoint, and **1 production gate DEFERRED**. No assertion failed. Not
Applicable is a scope boundary, not evidence that the later workflow is safe;
the owning implementation must add executable evidence before authorization.

## MESP-91 correction overlay — merged and Done

MESP-91 Correction Package 1 was approved by focused ChatGPT security review
(APPROVED TO MERGE; 0 Critical, 0 High, 0 Medium blockers) and merged to
`main` through PR #20 at commit `f2cde57400fed470ab048776e05b56f353b36890`,
now the current merged-main foundation baseline. It adds the missing
durable-work authority boundary on top of the prior MESP-64 checkpoint:
Identity now exposes a narrow organization ownership
resolver that converts an untrusted `TenantWorkScopeRequest` into a
resolver-issued, authorization-context-bound `TenantWorkScope`. The resolver
validates the exact Tenant -> Company -> Branch -> Warehouse ownership chain
and downward authorized-scope containment; missing ownership and foreign or
sibling targets fail closed.

The worker and outbox dispatch paths now call a narrow live authority
revalidator immediately before a handler or protected outbox effect. It
rechecks the initiating Tenant and authorization path, current User/session,
ordinary Membership or SupportGrant/SupportCase, exact Permission and current
scope/ownership. A failed check produces a terminal `AuthorizationDenied`
dead-letter and safe evidence; no handler or outbox effect is reached. The
correction evidence is in `DurableWorkAuthorityRevalidationTests` and
`DurableWorkTests`. Ordinary revalidation requires the canonical explicit
selected scope and never falls back to a Tenant-wide membership grant; a
SupportGrant revalidation uses the current case-bound stored grant scope rather
than a context marker. Verified authority binds the exact work, Tenant,
operation, correlation, organization boundary, execution context, path,
Membership/SupportGrant, actor and session. The SQL probes remain persistence,
lease, transaction and idempotency evidence only; they are not
worker-authorization evidence.

The current correction validation passes the focused durable-work suite 102/102,
the complete backend suite 360/360, the targeted SQL Server suite 11/11, the
Angular suite 27/27 and four Playwright journeys. The Release build has zero
warnings and zero errors, and the production dependency audit reports zero
vulnerabilities; the `npm ci` development install reports three moderate
development-only advisories outside that production audit.

MESP-48 supported-volume/capacity and MESP-50 retention, privacy, legal-hold,
purge, residency, backup and restoration gates remain unchanged and are not
authorized by this correction.

## SQL Server evidence

- The Tenant query filter was evaluated repeatedly and concurrently for Tenant B after Tenant A model initialization; Tenant A data was not visible.
- Stored-owner verification denied forged Tenant A modification and deletion of a Tenant B record; the stored Tenant B row remained unchanged.
- The SQL Server unique index permits the same BusinessKey in different Tenants and rejects a duplicate within one Tenant without exposing provider detail.
- The `Version` property is a SQL Server `timestamp`/`rowversion`; stale update and stale delete both return the safe persistence conflict.
- The required unique `(TenantId, BusinessKey)` index was inspected from SQL Server catalog metadata.
- Database collation was recorded without selecting an unapproved case-sensitivity policy, and an Arabic Unicode identifier round-tripped successfully.
- Test-only SQL probes proved transaction rollback removes work and outbox rows together, `(TenantId, EventId)` idempotency is Tenant-scoped, and a lease claim has one optimistic owner.

## Safety assertion matrix

The rows below reproduce the exact approved 75-assertion catalogue. A PASS row
names executable evidence already present in the repository. A NOT APPLICABLE
row identifies an approved scope boundary and owning implementation gate. The
DEFERRED row is reserved for the MESP-48/MESP-50 production decisions.

| # | Required assertion | Evidence | Status | Deferred owner or gate |
|---:|---|---|---|---|
| 1 | Cross-Tenant read is denied | `TenantPersistenceTests`, `SqlServerSafetyTests`, `RestFoundationTests`, `DurableWorkTests` | PASS | Foundation evidence in this checkpoint |
| 2 | Cross-Tenant write is denied | `TenantPersistenceTests`, `SqlServerSafetyTests`, `RestFoundationTests`, `DurableWorkTests` | PASS | Foundation evidence in this checkpoint |
| 3 | Cross-Tenant search/report/export/file access is denied | `TenantPersistenceTests`, `SqlServerSafetyTests`, `RestFoundationTests`, `DurableWorkTests` | PASS | Foundation evidence in this checkpoint |
| 4 | Client Tenant ID cannot expand authority | `HostSecurityTests`, `RestFoundationTests`, `TenantPersistenceTests` | PASS | Foundation evidence in this checkpoint |
| 5 | Valid Tenant A working state is not visible in Tenant B | `HostSecurityTests`, `RestFoundationTests`, `TenantPersistenceTests` | PASS | Foundation evidence in this checkpoint |
| 6 | Valid Tenant A working state is not automatically deleted by a context switch | `HostSecurityTests`, `RestFoundationTests`, `TenantPersistenceTests` | PASS | Foundation evidence in this checkpoint |
| 7 | Returning to Tenant A re-evaluates the applicable path: Membership/Role/Permission/scope/session/lifecycle for ordinary access, or the complete SupportGrant path for exceptional support | `HostSecurityTests`, `RestFoundationTests`, `TenantPersistenceTests` | PASS | Foundation evidence in this checkpoint |
| 8 | Separate concurrent Tenant contexts, caches and workspaces remain isolated | `HostSecurityTests`, `RestFoundationTests`, `TenantPersistenceTests` | PASS | Foundation evidence in this checkpoint |
| 9 | Revoked Membership is denied | `IdentityAuthorizationTests` session and membership revocation tests | PASS | Foundation evidence in this checkpoint |
| 10 | Role/scope revocation invalidates affected sessions | `IdentityAuthorizationTests` session and membership revocation tests | PASS | Foundation evidence in this checkpoint |
| 11 | Suspended Tenant denies ordinary interactive work | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 12 | Suspended Tenant denies ordinary asynchronous business work | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 13 | Draft, Provisioning and Configuration Required guards reject premature ordinary work | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 14 | Ready for Activation requires all approved activation prerequisites | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 15 | Activation is allowed only from the approved Ready for Activation state | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 16 | Grace Period applies its approved guard and does not invent a duration | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 17 | Reactivation re-evaluates all affected access and does not auto-restore it | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 18 | Export Requested is scoped, authorized and evidenced | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 19 | Termination Pending blocks ordinary work until approved guards pass | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 20 | Terminated revokes ordinary access while preserving evidence | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 21 | Retained state remains subject to MESP-50 and no purge executes | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 22 | Parent Tenant/unit suspension blocks descendants | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 23 | Offboarded User is denied | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 24 | User suspension revokes affected sessions | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 25 | User reactivation does not automatically restore prior privileges | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 26 | Expired support grant is denied | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 27 | Support grant cannot reach another Tenant | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 28 | Support grant alone cannot export | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 29 | Privileged operation requires MFA and operation-bound fresh authentication | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 30 | Five-attempt lockout lasts 15 minutes | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 31 | Ordinary session absolute expiry cannot exceed 8 hours | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 32 | Cookie renewal cannot extend the original absolute maximum | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 33 | Inactivity at 30 minutes expires the session | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 34 | Every protected request validates server-side UserSession revocation | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 35 | Password reset invalidates affected sessions | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 36 | Missing or invalid antiforgery is denied | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 37 | Inactive or closed units reject new work | No later ERP unit/historical-reference workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 38 | Authorized historical reference remains readable | No later ERP unit/historical-reference workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 39 | Used parent ownership cannot be rewritten | No later ERP unit/historical-reference workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 40 | Same-Tenant composite relationships enforce Branch -> Company, Warehouse -> Branch and Department -> Company | `TenantPersistenceTests` and `SqlServerSafetyTests` representative same-Tenant relationship guard | PASS | Foundation evidence in this checkpoint |
| 41 | Tenant Membership, Role Assignment and Support Case/Grant cannot reference another Tenant | `TenantPersistenceTests`, `IdentityAuthorizationTests` | PASS | Foundation evidence in this checkpoint |
| 42 | Missing TenantId is rejected by the write pipeline | `TenantPersistenceTests`, `SqlServerSafetyTests` | PASS | Foundation evidence in this checkpoint |
| 43 | Mismatched TenantId versus trusted context is rejected | `TenantPersistenceTests`, `SqlServerSafetyTests` | PASS | Foundation evidence in this checkpoint |
| 44 | Restricted IgnoreQueryFilters/raw SQL/bulk/maintenance paths are unavailable to ordinary Tenant calls | `ModuleBoundaryTests`, `TenantPersistenceTests` | PASS | Foundation evidence in this checkpoint |
| 45 | Background/worker/outbox work revalidates exact initiating Tenant ownership, verified organization ownership and authorized scope, plus live User/session/Membership/SupportGrant/SupportCase/Permission lifecycle | `DurableWorkAuthorityRevalidationTests`, `DurableWorkTests` | PASS | MESP-91 correction evidence; SQL probes remain persistence/lease/transaction/idempotency evidence only |
| 46 | Denied cross-Tenant audit/telemetry records do not leak target data | `AuditObservabilityTests`, `DurableWorkTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 47 | Tenant-aware alternate/unique keys and architecture dependency direction remain valid | `ModuleBoundaryTests`, `SqlServerSafetyTests` schema/index evidence | PASS | Foundation evidence in this checkpoint |
| 48 | MESP-48/MESP-50 gates cannot be bypassed and no production purge is authorized | Scope gate recorded; no production threshold or purge test is authorized | DEFERRED | MESP-48 performance/volume and MESP-50 retention, residency, privacy, legal-hold, purge and provider gates |
| 49 | A Platform administrator using PlatformGovernanceContext without an applicable Tenant path is denied Tenant business data; Platform governance remains limited to purpose-bound control-plane records | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 50 | An ordinary Tenant operation requires an active Membership, applicable Role and Permission, explicit Access Scope, current session, and eligible lifecycle | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 51 | Support access requires a named active Support User/session, MFA and operation-bound fresh authentication, active case, Tenant-approved grant, exact Tenant/purpose/scope, applicable Permission, and no more than eight hours | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 52 | The SupportGrant path never creates or uses ordinary Membership or RoleAssignment, standing access, export authority, or ordinary Tenant business operations | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 53 | Identity owns Roles, RolePermissions, RoleAssignments, and AccessScopeGrants; Platform Governance owns the Permission catalogue/policy and system-Role approval/seed meaning | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 54 | A custom Role has a non-null Tenant owner, cannot be assigned to or have ownership changed to another Tenant, and a system Role remains Platform-owned and cannot itself grant Tenant business access | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 55 | A privileged RoleAssignment decision uses a separate named approver, rejects self-approval, and preserves approval/rejection evidence | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 56 | A RoleAssignment without a valid active explicit AccessScopeGrant grants no Tenant authority | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 57 | Scope grants flow only downward, never to parents or siblings, and multiple valid grants combine only within the same Tenant | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 58 | Revoking a RoleAssignment revokes all child grants and blocks further authority; scope changes are single-effect, concurrent-safe, and audited | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 59 | Tenant/User/Membership reactivation requires current revalidation and never automatically restores prior privileges or work | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 60 | `Export Requested` stores an explicit operational access decision; `RecordTenantExportDisposition` accepts only ReturnToPriorEligibleState, Suspend, or BeginTermination and never implicitly grants export or executes purge | No Tenant export disposition workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle/export implementation gate; MESP-50 for purge concepts |
| 61 | MESP-30 numbering is Company/Legal Entity plus Document Type; optional Branch subdivision requires later approved owning-domain/Saudi justification, Warehouse is excluded absent approval, and no automatic reset is assumed | MESP-30 numbering behavior is an approved later-domain implementation responsibility | NOT APPLICABLE | MESP-30 owning-domain implementation and later approved numbering validation |
| 62 | Numbers are never reused after cancellation, rejection, voiding, or reset; permitted gaps remain attributable and auditable | MESP-30 numbering behavior is an approved later-domain implementation responsibility | NOT APPLICABLE | MESP-30 owning-domain implementation and later approved numbering validation |
| 63 | Every command/query maps to exactly one public or internal operation and every operation maps back to exactly one command/query; no catalogue row is orphaned | `RestFoundationTests` operation/security-profile catalogue checks | PASS | Foundation evidence in this checkpoint |
| 64 | Every API operation has one named homogeneous security profile with explicit owner, actor, path, authentication, MFA/fresh-auth, context, Permission, scope, lifecycle, concurrency, idempotency, audit, safe errors, and response | `RestFoundationTests` operation/security-profile catalogue checks | PASS | Foundation evidence in this checkpoint |
| 65 | Role, assignment, grant, support-case, and support-grant TenantIds are integrity-consistent; cross-Tenant mutation or lookup is denied | `IdentityAuthorizationTests`, `TenantPersistenceTests`, `SqlServerSafetyTests` | PASS | Foundation evidence in this checkpoint |
| 66 | Support-grant approval/rejection evidence is distinct from revocation evidence, and rejection does not masquerade as revocation of active access | Support rejection evidence and Tenant lifecycle state machine remain later implementation scope | NOT APPLICABLE | Approved Tenant lifecycle/export implementation gate; MESP-50 for purge concepts |
| 67 | Each lifecycle command has an approved predecessor/guard or an explicit rejection outcome; no transition creates a dead-end, and deferred `Purge Approved`/`Purged` concepts remain non-executable in Release 1 | Support rejection evidence and Tenant lifecycle state machine remain later implementation scope | NOT APPLICABLE | Approved Tenant lifecycle/export implementation gate; MESP-50 for purge concepts |
| 68 | Wafra-specific behavior remains validation-only and Retail POS remains outside the foundation specification and its test scope | `docs/15_Foundation_Release_1_Lean_Implementation_Specification.md`, ADR-018 scope control | PASS | Foundation evidence in this checkpoint |
| 69 | A Tenant Administrator can suspend a Membership in Tenant A while an independently authorized User remains eligible for Tenant B | `IdentityAuthorizationTests`, `HostSecurityTests` | PASS | Foundation evidence in this checkpoint |
| 70 | A Tenant Administrator cannot execute global `SuspendUser`, `ReactivateUser`, or `OffboardUser` | `IdentityAuthorizationTests`, `HostSecurityTests` | PASS | Foundation evidence in this checkpoint |
| 71 | A Platform Security Administrator or Platform Administrator with the specific global User lifecycle Permission can execute a global lifecycle action only with active authentication, MFA, fresh authentication, reason, immutable audit, concurrency and idempotency controls | `IdentityAuthorizationTests`, `HostSecurityTests` | PASS | Foundation evidence in this checkpoint |
| 72 | Global User suspension revokes all affected sessions across the User's Tenant contexts | `IdentityAuthorizationTests`, `HostSecurityTests` | PASS | Foundation evidence in this checkpoint |
| 73 | Global User reactivation never automatically restores Membership, Role, scope, SupportGrant, support authority or prior privilege | `IdentityAuthorizationTests`, `HostSecurityTests` | PASS | Foundation evidence in this checkpoint |
| 74 | A SupportGrant alone cannot execute global User lifecycle operations and cannot use a Tenant path to reach global User state | `IdentityAuthorizationTests`, `HostSecurityTests` | PASS | Foundation evidence in this checkpoint |
| 75 | Export disposition rejection or incompleteness leaves the Tenant in `Export Requested` with safe evidence and no implicit transition | No Tenant export disposition workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle/export implementation gate; MESP-50 for purge concepts |

## Traceability and limitations

The evidence chain is: approved PRD/BRDs → Foundation Release 1 Lean
Implementation Specification v0.4 → ADR-006/007/008/009/018 → MESP-61
contracts and MESP-64 harness → xUnit/Playwright/backend evidence → this
report. The SQL harness validates provider behavior on this developer machine;
it does not prove production sizing, throughput, HA, backup/restore, network
identity, residency, retention, legal hold, purge, or deployment equivalence.

MESP-48 remains the performance/volume/capacity gate. MESP-50 remains the
retention, privacy, residency, legal-hold, purge, backup/restoration and
provider-decision gate. No production migration, physical purge, retention
execution, database-per-Tenant design, later ERP domain, Retail POS or
Wafra-specific core behavior was added.

## Reproduction

From the repository root on the supported developer machine:

```powershell
.\scripts\validate-foundation.ps1
```

The command fails closed when LocalDB is missing, the connection is unset or
unsafe, any assertion fails, or cleanup cannot complete. Docker/Testcontainers
CI execution remains deferred by ADR-018.

## MESP-91 focused correction validation overlay — merged and Done

The MESP-91 correction was validated on approved head
`92bd9fd38912a062cc3723f46867258d54ca8127` (source/test commit
`4ed4b0588b613d492ce6c446ae963001b28f0eca`) from the correction branch,
against merged-main baseline `4eb1ef3ab094242cbb26ec9ab79b4037512e0d2d`. PR #20
was approved by focused ChatGPT security review and merged to `main` by normal
merge commit at `f2cde57400fed470ab048776e05b56f353b36890`; this overlay is now
the current merged-main capability and does not change the maturity or
production-provider claims above. The same validation totals below were
re-verified against merged `main` after the merge.

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Focused durable-work/authority regression | 102 passed, 0 failed, 0 skipped | `dotnet test backend/tests/MiniErp.ArchitectureTests/MiniErp.ArchitectureTests.csproj --filter FullyQualifiedName~DurableWork` |
| Complete backend suite | 360 passed, 0 failed, 0 skipped | `powershell -ExecutionPolicy Bypass -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database |
| Backend Release build | 0 warnings, 0 errors | Same validation command |
| Angular suite | 27 passed, 0 failed, 0 skipped | `npm test -- --watch=false --no-progress` |
| Angular production build | Passed | `npm run build` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npm run test:e2e` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Structural issuer and mandatory-evidence architecture checks | Passed in focused suite | `DurableWorkTests` syntax-tree allow-list and descriptor policy tests |
| Safety catalogue | 53 PASS, 21 NOT APPLICABLE, 1 DEFERRED, 0 failed | Existing 75-assertion catalogue; no scope claim changed |
| Diff check | Passed | `git diff --check` |

The LocalDB validation used the approved `MSSQLLocalDB` instance and Windows
integrated authentication only. The actual LocalDB/model collation observed
for the disposable run was `SQL_Latin1_General_CP1_CI_AS`; no case-sensitivity
policy was inferred from that value. The test database name used the
`MiniErpFoundation_` prefix and teardown was verified with a server query:
zero `MiniErpFoundation_*` databases remained. No production or shared
database was accessed.

The `npm ci` development install reported three moderate development-tree
advisories and four blocked optional install scripts; these do not affect the
production audit result and were not force-fixed as part of this focused
correction. No migration, provider selection, production worker, notification
provider, object-storage provider or purge behavior was introduced.

## MESP-92 single-effect/immutable-payload validation overlay — In Progress

MESP-92 re-ran the same disposable-LocalDB Foundation validation from branch
`fix/MESP-92-single-effect-immutable-payloads`, based on merged-main baseline
`32a91f27bc162685fc0db0f38b031d02ffbc99d2`. It introduces no new SQL Server
schema, index, transaction or lease probe; the existing 75-assertion safety
catalogue and its 53 PASS / 21 NOT APPLICABLE / 1 DEFERRED disposition are
unchanged. The correction's own evidence is new focused DurableWork unit
coverage for payload immutability, effect single-execution semantics and
genuine concurrency, validated alongside the unchanged SQL probes:

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Focused durable-work regression (baseline + new) | 136 passed, 0 failed, 0 skipped | `dotnet test backend/tests/MiniErp.ArchitectureTests/MiniErp.ArchitectureTests.csproj --filter FullyQualifiedName~DurableWork` |
| Complete backend suite | 394 passed, 0 failed, 0 skipped | `powershell -ExecutionPolicy Bypass -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database |
| Backend Release build | 0 warnings, 0 errors | Same validation command |
| Angular suite | 27 passed, 0 failed, 0 skipped | `npm test -- --watch=false` |
| Angular production build | Passed | `npm run build` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npx playwright test` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Architecture enforcement (effect-guard bypass check) | Passed | `Handler_invocation_is_reachable_only_through_the_effect_executor` |
| Safety catalogue | 53 PASS, 21 NOT APPLICABLE, 1 DEFERRED, 0 failed | Existing 75-assertion catalogue; no scope claim changed |
| Diff check | Passed | `git diff --check` |

No `MiniErpFoundation_*` database remained after teardown. MESP-92 is **not**
marked Done by this validation overlay; the PR is opened non-draft and held
unmerged pending focused ChatGPT security review.

## MESP-92 focused ChatGPT re-review corrections — In Progress

A focused ChatGPT security review of PR #22 raised four further findings —
H92-01 (effect-purpose/EventId keying), H92-02 (explicit protected-effect
outcome contract), M92-01 (uncertain reconciliation state) and M92-02
(payload mutation-hook removal) — corrected on the same branch. This overlay
is backend-only: no Angular, Playwright or `npm` evidence changed, so the
frontend rows above remain the prior overlay's evidence and were not re-run
for this correction. Updated backend evidence:

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Focused durable-work regression (baseline + all corrections) | 159 passed, 0 failed, 0 skipped | `dotnet test backend/tests/MiniErp.ArchitectureTests/MiniErp.ArchitectureTests.csproj --filter FullyQualifiedName~DurableWork` |
| Complete backend suite | 417 passed, 0 failed, 0 skipped | `powershell -ExecutionPolicy Bypass -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database |
| Backend Release build | 0 warnings, 0 errors | Same validation command |
| Architecture enforcement (effect-guard bypass + no bare generic retry) | Passed | `Handler_invocation_is_reachable_only_through_the_effect_executor`, `Protected_effect_boundary_cannot_return_a_bare_generic_retry` |
| Diff check | Passed | `git diff --check` |

No `MiniErpFoundation_*` database remained after teardown. No PRD file
appears in the branch diff and the unrelated `local-prd-rename-before-MESP-92`
stash remains untouched. MESP-92 is **not** marked Done by this overlay; PR
#22 remains open, non-draft and held unmerged pending a focused ChatGPT
re-review.

## MESP-92 second focused ChatGPT re-review corrections — In Progress

A second focused ChatGPT security review of PR #22 raised five further
findings — H92-03 (structurally enforced single effect ledger via
`DurableWorkLocalRuntime`), H92-04 (exact-scope reconciliation-read
authorization via `VerifiedDurableWorkReconciliationAuthorization`), M92-03
(exact uncertain-effect identity via `DurableWorkEffectKey`/`TenantWorkScope`),
M92-04 (custom codec exception normalization) and L92-01 (crash terminology
correction) — corrected on the same branch. This round re-ran the complete
backend and frontend validation pipeline:

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Focused durable-work regression (baseline + all corrections) | 199 passed, 0 failed, 0 skipped | `dotnet test backend/tests/MiniErp.ArchitectureTests/MiniErp.ArchitectureTests.csproj --filter FullyQualifiedName~DurableWork` |
| Complete backend suite | 457 passed, 0 failed, 0 skipped | `powershell -ExecutionPolicy Bypass -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database |
| Backend Release build | 0 warnings, 0 errors | Same validation command |
| Angular suite | 27 passed, 0 failed, 0 skipped | `npm test -- --watch=false` |
| Angular production build | Passed | `npm run build` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npm run test:e2e` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Architecture enforcement (single-ledger composition allow-list) | Passed | `Shipping_construction_of_the_ledger_types_occurs_only_inside_the_approved_composition` |
| Diff check | Passed | `git diff --check` |

New focused evidence lives in `DurableWorkLocalRuntimeCompositionTests`
(H92-03: structural construction restriction, one-shared-executor and
one-ledger-per-composition-call proof) and
`DurableWorkReconciliationAuthorizationTests` (H92-04 exact-scope
authorization matrix across Tenant/Company/Branch/Warehouse siblings, missing
permission, expired session, suspended Membership, expired/revoked
SupportGrant and missing selected scope; M92-03 exact identity, safe-reason
preservation and immutability). M92-04 evidence extends
`DurableWorkPayloadAndEffectTests`. No new SQL Server schema, index,
transaction or lease probe was introduced; the existing 75-assertion safety
catalogue disposition is unchanged.

No `MiniErpFoundation_*` database remained after teardown. No PRD file
appears in the branch diff and the unrelated `local-prd-rename-before-MESP-92`
stash remains untouched. MESP-92 is **not** marked Done by this overlay; PR
#22 remains open, non-draft and held unmerged pending a further focused
ChatGPT re-review.
