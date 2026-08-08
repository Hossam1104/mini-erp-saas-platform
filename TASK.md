# MESP-96 - M95-SL-01 Shared boundary and Tenant/scope contracts

## Current root-task handoff - 8 August 2026 (MESP-96 complete)

The MESP-96/M95-SL-01 execution recorded below is complete. The bounded source
slice adds only the Master Data/Catalog and Business Parties seams, trusted
server-derived Tenant consumption, policy-neutral scope and authorization
hooks, stable reference contracts, and audit/evidence integration. It creates
no Master Data persistence, endpoint, migration, database access, or business
decision for Product/Item identity, SKU/Barcode, tracking, availability,
approval catalogue, or lifecycle defaults. The implementation branch, PR,
merge evidence, and validation results are recorded in `.ai/CURRENT_STATE.md`
and the MESP-96 Jira closure comment.

The implementation commit is `aa413f7c9dadea036f1f8ab6a4f47fb5ed83b0f0` and
publication PR **#30** merged into `main` at
`87f150d95f583168a86aa56200916343c6404f7f`. Jira MESP-96 is **Done** with
completion evidence comment `10655`.

The next exact root-task session is M95-SL-02 Category and UOM. It is not
started in this chat and has no active Jira child slice. Its first-data-bearing
gates remain MD-OD-001, MD-OD-005, MD-OD-008, MD-OD-002, and MD-OD-006, with
ADR-002/ADR-006 and ADR-011 timing preserved.

## Executable session definition - 8 August 2026

This is the single next implementation session after the completed MESP-95
readiness item. A fresh Codex/Luna chat executes exactly this root `TASK.md`
session and no later session in the same chat.

- Jira: `MESP-96` - Implement Master Data shared boundary and Tenant/scope
  contracts (M95-SL-01), **In Progress**.
- Predecessor: `MESP-95`, **Done**; Jira closure evidence comment `10654`.
- Approved readiness head: `c465d660e49a254f2fffbb95e0d07c5fcf17a193`.
- Predecessor merge: PR #29 merged normally at
  `93f4e83992ef46f498cfbfacbb513cfc3d8dda7d`.
- Starting baseline: synchronized `main`; verify the exact local and remote
  `main` head in `.ai/CURRENT_STATE.md` before creating a dedicated branch.
- No MESP-96 source implementation has been performed in the handoff session
  that creates this task.

## Objective

Implement only the first bounded source slice from the approved MESP-95
specification: the shared Master Data/Catalog and Business Parties boundary,
trusted server-derived Tenant context, policy-neutral business scope,
authorization/resource hooks, audit/evidence contracts, stable reference
vocabulary, and the minimum architecture enforcement required for this slice.

## Required source and architecture inspection

Before changing backend structure, read and reconcile:

- `AGENTS.md`, `CLAUDE.md`, `.ai/CURRENT_STATE.md`;
- `docs/16_Master_Data_and_Product_Catalog_BRD.md`;
- `docs/17_Master_Data_and_Product_Catalog_Lean_Implementation_Specification.md`;
- `docs/01_Technology_Architecture_Baseline.md` and `docs/Decisions.md`;
- the applicable Foundation specifications/checkpoints and ADR-002, ADR-005,
  ADR-006, ADR-007, ADR-008, ADR-011, ADR-016, and ADR-018;
- the actual `backend/MiniErp.sln`, project references, and architecture tests.

Honor the existing modular-monolith application direction:

- `MiniErp.Api` is the host;
- `MiniErp.App` contains application/module internals;
- `MiniErp.Contracts` contains stable public contracts;
- the permitted application dependency direction is `Api -> App -> Contracts`.

The repository already contains infrastructure and test projects associated
with this baseline. Do not invent a new production project, a fourth project,
a microservice, or a new topology. Keep any architecture adjustment minimal,
explicitly justified by ADR-002 and covered by architecture tests.

## Hard boundaries - mandatory

This session is contract-only and non-persistent. It MUST NOT:

- persist a Master Data business record;
- create Master Data EF entities or Master Data database tables;
- create a Master Data migration;
- no MESP database creation/access solely for this slice;
- make a Product/Item decision;
- make a SKU/Barcode decision;
- make a batch/lot/serial/expiry tracking decision;
- make a business availability assumption (Tenant-wide, Company, Branch, or
  other scope);
- make an approval catalogue assumption or create an approval catalogue;
- make a Draft/Active creation or lifecycle assumption;
- add Wafra-specific behavior;
- add Retail POS scope or consumer behavior;
- start `M95-SL-02` or any later slice.

No unresolved MD-OD decision may be silently answered. In particular, keep
MD-OD-001, MD-OD-003, MD-OD-005, MD-OD-008, MD-OD-010, and MD-OD-011 outside
implementation behavior, and preserve the complete MD-OD-001 through
MD-OD-011 register.

## In-scope implementation

Implement only the minimum safe structures for:

- the Master Data/Catalog module boundary;
- the Business Parties boundary required by shared contracts;
- trusted server-derived Tenant context consumption;
- a policy-neutral `BusinessScope` contract;
- resource and authorization-policy inputs/hooks;
- audit/evidence contract integration;
- stable reference contracts and vocabulary for later Master Data slices;
- approved architecture dependency enforcement required for this slice.

Do not broaden into Product persistence, lifecycle rules, approval workflows,
localized search/forms/documents, migrations, database provisioning, or any
downstream slice. ADR-011 remains a dependency before affected localized
search/form/document implementation.

## Validation and Definition of Done

Run targeted validation for the touched source only:

- architecture and dependency rules, including ADR-002 enforcement;
- Tenant-positive and Tenant-negative contract behavior;
- same-code/different-Tenant isolation at the contract/policy level without
  persistence;
- proof that client-supplied Tenant data cannot expand server authority;
- authorization/audit contracts preserving Tenant and applicable organization
  scope;
- proof that no persistent Master Data record is created;
- proof that no unresolved MD-OD is implemented;
- targeted build/tests required by the touched code;
- `git diff --check`, complete task-diff review, and source/scope scans.

Avoid broad unrelated test suites. Do not create a database, migration,
credential, production provider, or external integration for this slice.

The session is Done only when the bounded contracts are implemented and
validated, the exact diff is reviewed, the branch/PR is pushed, and the
repository handoff and affected Markdown state are updated.

## Standing execution governance

Hossam has standing Owner approval for normal BRD, specification, readiness,
merge, closure, and next-session activation while work remains inside the
approved project scope and architecture. Do not stop for ceremonial approval.
Stop only for a real blocker: security or Tenant-isolation weakness;
accounting or data-integrity risk; destructive migration or data-loss risk;
an unresolved decision that would require invented business behavior; legal or
external-validation requirements; credential or production-infrastructure
risk; or a material scope/architecture deviation.

Every fresh Codex/Luna chat executes exactly one root `TASK.md` session. At
session end: validate, review the complete task diff, update this `TASK.md`
with the next exact session, update `.ai/CURRENT_STATE.md`, update every
genuinely affected Markdown state/plan file, update Jira, commit/push, merge
only when the completed session is clean and no real blocker exists, then
STOP and return the completion report to Hossam for ChatGPT review.

Never automatically execute the next `TASK.md` in the same chat. Run an
independent Opus project review after every five completed execution sessions,
or earlier at a critical architecture, security/Tenant-isolation,
accounting/financial-posting, migration/data-model, or major cross-module
checkpoint.

## Exact next action

Start a fresh Codex/Luna chat from the synchronized `main` after the MESP-96
merge, re-read this task and the required baselines, inspect the approved
MESP-31 BRD, M95 implementation specification, ADR-002/ADR-005/ADR-006,
ADR-011, and the actual project structure, then execute only M95-SL-02
Category and UOM. Before any data-bearing source work, confirm that
MD-OD-001, MD-OD-005, MD-OD-008, MD-OD-002, and MD-OD-006 are resolved or
explicitly Owner-bounded for the slice. Do not execute M95-SL-03 or any other
`TASK.md` session automatically, and do not create a Jira child slice without
its own active Definition of Ready.
