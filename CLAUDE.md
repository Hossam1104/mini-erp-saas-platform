# Mini ERP execution guidance

This repository is a reusable, bilingual, multi-tenant B2B ERP foundation.
Read `TASK.md` for the current bounded session and `.ai/CURRENT_STATE.md` for
the concise project truth. MESP-136 is accepted and merged; MESP-137 and later
capabilities are inactive. Do not start a later capability, merge the Draft
PR, or make Jira governance changes outside the current checkpoint.

## Operating rules

- Verify the branch, exact remote baseline, worktree, open PRs, and live Jira
  state before changing files.
- Use one dedicated branch and one reviewable Draft PR. Never rebase or
  force-push. Preserve unrelated user changes.
- Keep `frontend/assets` untouched without explicit Owner instruction.
- Do not delete migrations, snapshots, accounting/inventory/audit evidence,
  public contracts, dynamic registrations, localization keys, or test
  infrastructure without multi-source proof of non-use.
- Preserve Tenant isolation, server-owned authorization, antiforgery, audit,
  idempotency, optimistic concurrency, financial/inventory semantics, public
  routes and OpenAPI operation IDs.
- Use only disposable `MiniErpFoundation_*` LocalDB targets for destructive SQL
  safety tests via `MESP_SQLSERVER_SAFETY_CONNECTION_STRING`; never use the
  persistent `MESP` database for that harness.
- Keep fast-track implementation progress separate from production readiness;
  MESP-48 and MESP-50 remain open unless independently evidenced otherwise.
- Use Ponytail FULL when installed, but never trade safety or validation for a
  smaller diff.

## Architecture

The intended dependency direction is `Api -> App -> Contracts`, with
`Infrastructure` implementing App persistence ports and referenced by the API
composition root. Modules own their contexts, schemas, migrations, contracts,
authorization seams, and audit/evidence behavior. Host resolution is routing
input only; exact Tenant membership and operational Company/Branch context are
server-authoritative and Overview-first under ADR-019.

## Handoff

The executor must update the tracked statistics file when project progress or
implementation state changes, run the full available validation, document
known warnings and gates, push the dedicated branch, and stop for independent
GPT-5.6 Sol acceptance. Jira checkpoint items remain In Progress until their
owner decides closure.
