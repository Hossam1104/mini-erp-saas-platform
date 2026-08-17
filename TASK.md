# CLAUDE OPUS 5 — TARGETED SECURITY AND INTEGRATION REVIEW OF MESP-143

## Mission

Perform an independent, read-only review of the completed bounded MESP-143
implementation on branch `feat/MESP-143-tenant-aware-entry`, Draft PR against
`main`. Do not merge, force-push, perform Jira operations, or start the next
product capability.

Review the complete MESP-143 diff and the approved architecture inputs:

- `AGENTS.md`, `CLAUDE.md`, `.ai/CURRENT_STATE.md`;
- `docs/ADR-019_Tenant_Host_Resolution_Workspace_Context_and_Branding.md`;
- `docs/MESP-143_Tenant_Aware_Entry_Execution_Plan.md`;
- current REST/Identity/Tenancy/organization-scope contracts and Angular shell.

## Required review gates

1. Host safety: normalized configuration-led bindings, active/disabled and
   collision behavior, unknown/malformed hosts, trusted-proxy-only forwarded
   headers, preserved browser Host through the Development proxy, and no
   client Tenant header or route/query identifier authority.
2. Tenant authority: exact Tenant-host membership, common-host single/multiple/
   zero membership routing, no unrelated Tenant disclosure, platform-admin
   separation, support-path compatibility, and no cross-Tenant context leakage.
3. Overview/context UX: Tenant Overview is the first business surface;
   `/app/workspaces` is compatibility/management only; singular Company/Branch
   context auto-selects; multiple contexts use the header; stale/unauthorized
   operational switches fail closed with optimistic concurrency.
4. Branding/SAR: generic Tenant configuration and MESP fallback, light/dark
   fallback, accessible identity, semantic `SAR` fallback, non-SAR resilience,
   and zero FX/tax/accounting/persisted-amount mutation. Confirm
   `frontend/assets` was not changed.
5. Regression: MESP-123 procurement and Supplier Quotation routes, auth bypass
   invariants, REST/OpenAPI catalogue coverage, SQL/provider boundaries, and
   no downstream Purchase Order/receipt/invoice/AP/accounting/stock scope.

## Validation

Run the bounded available suite and record exact results:

```powershell
dotnet build .\backend\MiniErp.sln --configuration Release --no-restore --verbosity minimal
dotnet test .\backend\tests\MiniErp.ArchitectureTests\MiniErp.ArchitectureTests.csproj --configuration Release --no-restore --verbosity minimal
cd .\frontend
npm test -- --watch=false --no-progress
npm run build
npm run test:e2e
npm audit --omit=dev
```

If the dedicated SQL safety connection is unavailable, report it as an
environment-gated validation item; do not substitute the persistent `MESP`
runtime connection or alter the database. Inspect the complete diff and
`git diff --check` before the verdict.

## Required verdict

Return `APPROVE FOR MERGE`, `CHANGES REQUIRED`, or `BLOCKED`, with:

- reviewed SHA and Draft PR state;
- host/Tenant/platform findings ordered P0/P1/P2/P3;
- exact backend, Angular, build, E2E, and audit evidence;
- explicit Tenant-isolation, accounting/stock-integrity, asset, and scope
  statements;
- merge recommendation for the MESP-143 Draft PR.

Do not execute any subsequent capability after this review.
