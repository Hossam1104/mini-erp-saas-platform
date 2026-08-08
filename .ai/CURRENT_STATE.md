# Current State

## Current authoritative position - 9 August 2026 (MESP-100 closed; MESP-99 active)

This is the authoritative live repository and Jira handoff after the bounded
MESP-100 readiness correction. Historical sections below are preserved for
provenance and are not executable current-state instructions.

| Current fact | Verified value |
|---|---|
| MESP-100 | **Done**; closure evidence is Jira comment `10663`. |
| MESP-99 | **In Progress**; activation evidence is Jira comment `10664`; it is the single active implementation item for M95-SL-02. |
| Reviewed starting baseline | `c948a4fba8cf1ac9620474b42d56ce95f9effd52`. |
| MESP-100 branch | `fix/MESP-100-m95-sl-02-readiness`. |
| Source/document correction commit | `a009616f5b5c3a46d9ea0b369b4f3e3a4c143129`. |
| Focused PR | **#32**, merged cleanly. |
| Functional merge commit | `511f6be9f005e54930f993aead9758d7a66b75a8`; local `main` and `origin/main` were synchronized to this merge before the final handoff metadata update. |
| MESP-96 / M95-SL-01 | **Done**; remains contract-only and non-persistent. |
| ADR-002 | Published at `docs/ADR-002_Backend_Project_Structure_and_Module_Enforcement.md`; actual four-project roles and project-reference direction are explicit and tested. |
| Production project direction | `MiniErp.Api -> MiniErp.Infrastructure -> MiniErp.App -> MiniErp.Contracts`; Api also references App and Contracts for host composition; no cycle, fifth project, or microservice was introduced. |
| Authorization correction | Immutable server-owned `MasterDataOperationCatalog`: View->View, Create->Create, Edit->Edit, Activate->Activate, Deactivate->Deactivate, Approve->Approve, Import->ImportMigrate, ViewAuditHistory->ViewAuditHistory. Unknown/unmapped operations fail closed and callers cannot pair an unrelated capability. |
| Validation | Release build 0 warnings/0 errors; focused MasterData + ModuleBoundary tests 39/39 passed; non-SQL architecture suite 582/582 passed; `git diff --check` clean. |
| SQL safety gate | 21 existing SQL Server safety tests require the explicitly configured `MESP_SQLSERVER_CONNECTION_STRING`; no credential or production infrastructure was invented. |
| Category/UOM implementation | None in MESP-100: no entity, table, DbContext, migration, repository, service, endpoint, persistence, or MESP-99 business behavior was added. |
| Owner bounds | MD-OD-001, MD-OD-005, MD-OD-008, MD-OD-002, and MD-OD-006 are recorded as Category/UOM-only bounds; the rest of MD-OD-001 through MD-OD-011 remains preserved and unresolved for other domains. |
| Open production gates | MESP-48, MESP-49, and MESP-50 remain open. |
| Root task | `TASK.md` contains only `MESP-99 — M95-SL-02 Category and UOM` and its exact implementation instructions. |
| Current branch | `main`; PR #32 is merged and no readiness PR remains open. |

## Historical execution position - 8 August 2026 (preserved)

This historical state section is preserved for provenance. The authoritative
live repository and Jira position is recorded in the current section above.

| Current fact | Verified value |
|---|---|
| MESP-31 | **Done**; the approved BRD v0.3 baseline remains unchanged. |
| PR #29 | **Merged** normally at actual merge commit `93f4e83992ef46f498cfbfacbb513cfc3d8dda7d`; approved final PR head `c465d660e49a254f2fffbb95e0d07c5fcf17a193`. |
| MESP-95 | **Done** in Jira; closure evidence comment `10654`; ChatGPT final review passed and M95-R01/M95-R02/M95-R03 are closed. |
| MESP-96 | **Done** in Jira; original completion evidence comment `10655`; post-merge correction evidence comment `10657`; the exact synchronized handoff main is recorded below. |
| M95-SL-01 | **Complete, contract-only, and non-persistent**; no Master Data persistence exists. |
| Original functional merge | PR #30 merged at actual merge commit `87f150d95f583168a86aa56200916343c6404f7f`; original final synchronized main before correction `f3ba1a498ad0df0d39307e75ba33bc6789e9d35b`. |
| Correction branch | `fix/mesp-96-optional-scope-hint`; source correction commit `85d3c48f20a97f8057e5960c305a3bcc0cb8d672` (`fix(MESP-96): accept optional scope hints`). |
| Correction Pull Request | **#31 merged** to `main` at actual merge commit `4eeefe0d1a9af209cc3e31608812ec35ef283fd9`. |
| Source boundary | Master Data/Catalog and Business Parties composition seams; server-derived Tenant context consumption; policy-neutral BusinessScope/scope-policy hook; capability, resource-policy, generic approval, stable-reference, and audit/evidence contracts. |
| Correction semantics | Empty and same-Tenant tenant-only selections are optional hints that preserve trusted server-derived Tenant/scope authority; exact trusted scope remains allowed; foreign Tenant and sibling/foreign scope remain denied. |
| Validation | Merged correction main: Release solution build 0 warnings/0 errors; focused `MasterDataBoundaryTests` + `ModuleBoundaryTests`: 34/34 passed; `git diff --check`, complete-diff review, prohibited-persistence/unresolved-behavior scans passed. |
| Next exact session | M95-SL-02 Category and UOM; not started, no Jira child active, and first-data-bearing MD-OD/ADR gates remain required. |
| Open decisions | MD-OD-001 through MD-OD-011 remain unresolved and preserved. |
| Production/external gates | MESP-48, MESP-49, and MESP-50 remain open; no production or external-validation decision is invented. |
| Source implementation | MESP-96 source implementation is now present only in the bounded non-persistent slice described above; no Product/Item, SKU/Barcode, tracking, availability, approval-catalogue, lifecycle, Wafra, Retail POS, migration, database, or endpoint behavior was added. |
| Current branch | `main`; the required state/task reconciliation content is published at `ecfe7f7` (`docs(MESP-96): reconcile correction handoff`), followed by the final metadata-only handoff update. |
| Main synchronization | The state/task handoff is synchronized through `e4f81c28de1728ea3a11a296c1547b3557b93311`; subsequent metadata-only handoff updates remain on `main`. The functional PR #31 merge is `4eeefe0d1a9af209cc3e31608812ec35ef283fd9`; the original PR #30 review thread is replied to and resolved, and no correction PR remains open. |

M95-SL-01 remains contract-only: no Master Data EF entities/tables, migration,
or `MESP` database creation/access solely for this slice; no Product/Item,
SKU/Barcode, tracking, business-availability, approval-catalogue, or
Draft/Active decision; no Wafra-specific behavior, Retail POS scope, or
M95-SL-02 work was added by the correction. The correction only repaired
optional target-hint handling in the existing resolver. ADR-002 and the actual
repository architecture remain authoritative; preserve the approved
`MiniErp.Api -> MiniErp.App -> MiniErp.Contracts` direction and do not invent a
new production project or topology.

Hossam has standing Owner approval for normal BRD/specification/readiness,
merge/closure, and next-session activation inside approved scope and
architecture. Each fresh Codex/Luna chat executes exactly one root `TASK.md`
session, validates, updates the handoff and affected Markdown/Jira, commits and
pushes, merges only when clean and unblocked, then STOPs for ChatGPT review.
Never automatically execute the next session. Independent Opus review is due
after every five completed sessions or earlier at a critical architecture,
security/Tenant-isolation, accounting, migration/data-model, or major
cross-module checkpoint.

## Current verified position — 8 August 2026 (MESP-31 closed; MESP-95 active)

The Stage-A and Stage-B gates are now sequenced and live. MESP-31 is closed
after its approved BRD merged, and MESP-95 is the single active
implementation-readiness item. The specification work remains documentation
only; no Master Data source implementation has started.

| Current fact | Verified value |
|---|---|
| Current branch | `docs/MESP-95-master-data-lean-implementation-spec`, created from merged `main` at `1dc4d2092d6e9a5bf8f6cfc3347e552a5ddbad1b` |
| MESP-31 | **Done**. BRD v0.3 is the Hossam-approved Release 1 Business Baseline; approval comment `10649`; closure evidence comment `10650`. |
| PR #28 | **Merged**. Final PR head `8396197b54189cb550f07bd4bb6779fd38ac30cb`; actual merge commit `1dc4d2092d6e9a5bf8f6cfc3347e552a5ddbad1b`; approved reviewed BRD head is an ancestor of `main`. |
| MESP-95 | **In Progress**. `Produce Master Data and Product Catalog Lean Implementation Specification`; Jira item already existed and was activated after the Stage-A exit gate. |
| Specification | `docs/17_Master_Data_and_Product_Catalog_Lean_Implementation_Specification.md`, Draft - implementation-readiness review; proposed slices only, no Jira children activated. |
| MESP-95 branch | `docs/MESP-95-master-data-lean-implementation-spec` |
| MESP-95 PR | **#29** — Open, non-draft, documentation-only readiness review; initial draft head `dc550e1171e8f9d20cd7fdf5509dfffb7537b3bd`. |
| Open decisions | MD-OD-001 through MD-OD-011 remain preserved and unresolved; the specification classifies their slice impact without answering them. |
| Other In Progress task | MESP-23 is the governance/open-questions register, not an implementation or readiness item. |
| Production gates | MESP-48, MESP-49, and MESP-50 remain open; no supported-volume, retention, privacy, legal-hold, purge, residency, backup, restoration, or production topology decision is invented. |
| Source implementation | **None**. No entities, mappings, migrations, database, repositories, services, endpoints, controllers, Angular implementation, or source tests were created. |
| Canonical approved PRD | `docs/MESP_PRD_v1.2.docx`; protected Git blob `1f9163b9412cb343a19a98312eb642ad26c1efaa` |
| MESP-95 review corrections | **M95-R01, M95-R02, and M95-R03** are the only findings addressed in this documentation-only session; MD-OD-001 through MD-OD-011 remain open/unresolved and no source implementation, migration, database, secret, or Jira child was created. |

The remainder of this file preserves the earlier pre-merge and historical
checkpoint narratives for provenance. This current section supersedes their
older live-state claims.

### MESP-95 correction-session handoff — 8 August 2026

- Session starting head: `d44ea29992ce1b927265c7fee4438ff888eca4f1` on
  `docs/MESP-95-master-data-lean-implementation-spec`. The attachment's
  earlier expected head `f4e3131c8f733ac3a92c7e9f83d8f2b970564d07` was
  superseded by the newer empty `TASK.md` commit and was preserved.
- M95-R01 corrects the durable-work/outbox maturity wording in the
  implementation specification; production SQL/durable persistence remains a
  later provider/production gate.
- M95-R02 records the post-merge MESP-31/PR #28 state without changing the
  approved BRD requirements or Open Decision Register.
- M95-R03 reconciles the contract-only SL-01 gate, first data-bearing gates,
  affected-domain Open Decisions, ADR-002/ADR-011 timing, and the generic DoR.
- Final correction commit and final PR #29 branch head are the single pushed
  documentation-only commit produced by this session; the exact SHA is the
  final PR #29 head recorded in the session completion report. PR #29 remains
  open and non-draft pending ChatGPT re-review.
- No Opus review, PR merge, Jira transition, Jira child creation, source
  implementation, migration, database, or secret action is authorized in this
  session.

## Start here — verified position on 8 August 2026 (MESP-31 BRD v0.3 Owner Approved; PR #28 pending merge)

A new agent can begin from this section with no prior chat history.

| Fact | Verified value |
|---|---|
| Current branch | `docs/MESP-31-master-data-product-catalog-brd`, created from verified `main` at `c86ecb851e88205f1d3907f5a5c36cfb59ce8b54` (PR #27 merge) |
| MESP-31 | **In Progress.** BRD v0.3 at `docs/16_Master_Data_and_Product_Catalog_BRD.md` is an **Approved Business Baseline**, approved by Hossam on 8 August 2026 in Jira comment `10649` at reviewed content head `1e2d055354f0ddde833190948d09fa426707484c`. Its Open Decision Register MD-OD-001 through MD-OD-011 is preserved; approval does not silently answer those decisions. No Master Data source implementation has begun. |
| MESP-31 Parent Epic | `MESP-6 — EPIC 06 - Master Data and Product Catalog` — verified against live Jira. |
| MESP-31 Owner authorizations and approval (in Jira) | Comment `10615` — BRD-entry authorization. Comment `10616` — future Master Data implementation authorization. Comment `10649` — approval of BRD v0.3 as the Release 1 business baseline at reviewed content head `1e2d055354f0ddde833190948d09fa426707484c`; the implementation authorization remains subject to the normal Definition of Ready and a dedicated active readiness item. |
| MESP-31 Jira Source Baseline | Primary anchor **PLT-003**; supporting anchors PLT-002, SAL-001, PROC-002, PROC-008, FIN-001, FIN-003, FIN-007, FIN-010, KSA-002, BR-013, ADM-003, plus the applicable PRD RULE set for master-data integrity. PLT-011–PLT-014 and BR-004 are Platform Administration anchors and are **not** MESP-31's baseline. |
| PR #28 | **Open, non-draft, mergeable, unmerged, approved for merge after approval-state reconciliation** — `docs(MESP-31): draft Master Data and Product Catalog BRD`, branch `docs/MESP-31-master-data-product-catalog-brd`, base `main` at `c86ecb851e88205f1d3907f5a5c36cfb59ce8b54`. Approved reviewed content head: `1e2d055354f0ddde833190948d09fa426707484c`; the approval-state reconciliation is the remaining repository step before merge. Review-thread count is currently zero unresolved. |
| Prior verified `main` | `main` (before this branch) |
| PR #26 | **Merged** to `main` — approved final head `2c7ed3dec4662672bb78967ceb70db7ed73eb7d4`, ChatGPT final merge review **APPROVED FOR MERGE** (0 Critical, 0 High, 0 Medium blockers); actual GitHub merge commit `06d837c958c1cb7977dc121e3aaea4e7278944fd` |
| PR #25 | Merged to `main` at `9f333c9734c767673e43a30d6b57c05793e1fb69` — MESP-93 post-merge Markdown reconciliation |
| MESP-94 | **Done** — closes H-2, H-3, M-3, M-6, M-10, M-12, M-13, M-14, M-15, L-2, L-3, L-5 (original round), R1-R7 (focused review round) and F1-F2 (concurrency-lock focused review round); see `docs/96_Foundation_Release1_Safety_Validation.md` for full evidence |
| MESP-93 | Done — PR #24 merged to `main` at `005c796629341ab9becfbc6d1abe2ae34b6a7332` (reviewed head `83b0c0ed547dcc1b41c873ed087ab4e62d49c50e`) after focused ChatGPT security re-review verdict **APPROVED FOR MERGE** |
| PR #23 | Closed as superseded (not merged) — its docs-only MESP-92 reconciliation content was already carried onto `main` through PR #24; see the PR #23 closing comment for file-by-file evidence |
| MESP-92 | Done — PR #22 merged to `main` at `322341e70e56270797d5770b4b90342c20b7833e` |
| MESP-91 | Done |
| Active Jira item | **MESP-31** (BRD finalization only; no source implementation) — after PR #28 merges and MESP-31 is closed, MESP-95 is the single next authorized implementation-readiness item |
| Foundation completion checkpoint | Performed 8 August 2026: MESP-92/93/94 Done; MESP-48/MESP-50 remain intentionally open production gates, not treated as blockers to MESP-31 BRD entry; no remaining Foundation correction ticket blocks BRD entry |
| MESP-31 (Master Data BRD) | **In Progress** — BRD v0.3 is an Owner Approved Business Baseline on open PR #28; MESP-31 is not yet Done until the PR actually merges and Jira closure evidence is posted. The eleven Open Decisions remain preserved and governed. No Master Data implementation has begun. |
| MESP-95 | **To Do** — `Produce Master Data and Product Catalog Lean Implementation Specification`; it becomes the single active item only after PR #28 merges, MESP-31 is confirmed Done in Jira, and no other implementation/readiness item is In Progress. |
| MESP-48 / MESP-50 | To Do — open production gates, preserved, intentionally not blocking BRD entry |
| Sprint | None active |
| Parallel implementation | None |
| Canonical approved PRD | `docs/MESP_PRD_v1.2.docx` |
| Hosted CI | None configured — all validation is local only |

### MESP-31 Owner-approval overlay — 8 August 2026 (pre-merge)

The historical review and correction sections below are preserved. The current
position is that Hossam approved MESP-31 BRD v0.3 as the Release 1 business
baseline in Jira comment `10649` at reviewed content head
`1e2d055354f0ddde833190948d09fa426707484c`. The approval preserves
MD-OD-001 through MD-OD-011 and silently resolves none of them; decisions
marked blocking remain implementation-slice gates. PR #28 is approved for
merge but remains open and unmerged until the approval-state reconciliation is
pushed and reverified. MESP-31 remains In Progress until its actual merge and
Jira closure. MESP-95 exists as To Do and is the next authorized item only
after the Stage-A closure gate. No Master Data source implementation has
started. MESP-48, MESP-49, MESP-50 and all qualified external-production gates
remain open.

### Post-merge focused verification (8 August 2026)

After PR #26 merged to `main` at `06d837c958c1cb7977dc121e3aaea4e7278944fd` (approved head `2c7ed3d` confirmed an ancestor, no divergence, no semantic merge edits), bounded focused verification was re-run directly on merged `main` rather than the full expensive suite (already run complete pre-merge at `037491cee8650bfd38c4fad4d58e3baa86a3e2a4` and targeted at final head `2c7ed3d`): `SafetyCatalogueValidationTests` + `SqlServerSafetyTests` **25/25** passed, `scripts/verify-foundation-validation-lock.ps1` **5/5** passed, `git diff --check` (working tree) and `git diff --check origin/main...HEAD` both passed, and 0 `MiniErpFoundation_*` databases remained after the run.

### MESP-31 BRD entry eligibility — RESOLVED 8 August 2026

`MESP-31 BRD ENTRY: ELIGIBLE — OWNER APPROVAL RECORDED.` The Foundation correction sequence blocking BRD entry (MESP-92, MESP-93, MESP-94) is complete, and MESP-48/MESP-50 are intentionally not entry blockers. `docs/94_Product_Delivery_Master_Plan.md`'s "Next authorized sequence" step 9 required the MESP-31 BRD's entry conditions to be "reconfirmed" before starting; the precedent for that reconfirmation (MESP-29, see `docs/13_Multi_Tenancy_BRD.md` SC-001) was a distinct founder/owner authorization statement, not an automatic consequence of Foundation completion. Hossam recorded that distinct authorization on 8 August 2026, explicitly scoping MESP-31 to cover Products, Product Categories, Units of Measure, Suppliers, Business Customers, Price Lists, Taxes, Payment Terms, Currencies, and Exchange Rates, and separately pre-authorized the later Master Data implementation phase (not yet executable — see below). MESP-31 moved to In Progress on branch `docs/MESP-31-master-data-product-catalog-brd`, and a v0.1 draft BRD was produced at `docs/16_Master_Data_and_Product_Catalog_BRD.md`. Both authorizations are recorded in live Jira — comments `10615` and `10616`. **This BRD draft is not yet Approved** and does not itself authorize implementation; do not start Master Data implementation until Hossam explicitly approves the BRD content and a dedicated implementation Jira item, separate from MESP-31, is identified and activated.

### MESP-31 BRD review round — PR #28 (8 August 2026)

The v0.1 draft was published as **PR #28** at head
`6d0aa80eef0a2860c85a141dd6f13ee38bf5760d` and received a
business-requirements review verdict of **CHANGES REQUIRED BEFORE OWNER
APPROVAL / MERGE**. A bounded, documentation-only correction round produced
**v0.2** on the same branch and the same Pull Request — no replacement PR was
opened. The corrections were:

- **MESP-41** (batch/lot/serial/expiry scope) reclassified from a confirmed
  requirement to a *Recommended Founder Decision Pack default — pending
  Hossam approval*, and raised as new Open Decision **MD-OD-010**, blocking
  the Master Data implementation baseline and jointly dependent on MESP-33
  Inventory.
- **MESP-54** (exchange-rate sourcing and Finance approval) reclassified as
  *Deferred Gate / Recommended Default — not yet approved*, owned by
  Finance/MESP-34 and not approved by this BRD.
- **Approval controls** corrected: no approved source establishes a
  separate-approver rule for Tax or Price List changes, so both were
  withdrawn from Confirmed status into Open Decision **MD-OD-005**. Only the
  generic control remains Confirmed (MD-BR-046 — where an approved policy
  requires separate approval, the requester may not self-approve and
  publication is blocked until the approval exists).
- **Draft-before-Active** (MD-OD-008) treated consistently as an Open
  Decision rather than simultaneously Confirmed and open; the "no Draft
  state for Release 1" position is retained as a recommendation.
- **Lifecycle wording** corrected — a deactivated record becomes *Inactive
  and unselectable for new use*, not "Active-unselectable".
- **Business Party** duplicate semantics clarified in the BRD and the
  glossary: duplicate detection runs within a party role; a cross-role
  identity match between Supplier and Business Customer is surfaced for
  review and optional linkage and never auto-rejects the second role, since
  the approved glossary confirms the same legal company may be both. No
  unified Party record is introduced.
- **Organizational scope** separated into two questions: the Tenant
  ownership/isolation boundary (Confirmed and mandatory) versus
  Company/Legal Entity business availability (undecided, MD-OD-001).
  "Tenant-owned" is not read as "Tenant-wide usable by every Company", and
  no cross-Tenant shared business data is introduced.
- Parent Epic, the two Jira Owner-authorization comments, and the corrected
  Jira Source Baseline recorded as verified facts.

The Open Decision register now holds **ten** decisions (MD-OD-001 through
MD-OD-010). PR #28 remains **open and unmerged**, MESP-31 remains **In
Progress**, the BRD remains **Draft and not Approved**, and **no Master Data
implementation has started or may start automatically**.

### MESP-31 BRD second correction round — PR #28 (8 August 2026)

The v0.2 draft was reviewed at head `865701128c86d358f6aa919162c91d91ae025f21`
and received a further business-requirements verdict of **CHANGES REQUIRED —
FINAL SMALL CORRECTION ROUND**, raising four findings. A second bounded,
documentation-only correction round on the same branch and the same Pull
Request closed all four and produced **v0.3**:

- **M31-R10 (Product/Item modelling)** — MD-BR-015 ("Release 1 treats
  Product and Item as one concept; no separate variant layer") was classified
  Confirmed even though the approved glossary marks Item, SKU, and Barcode
  "Draft for BRD Validation" and explicitly defers Product-versus-variant
  modelling to this BRD. MD-BR-015 is withdrawn from Confirmed status and
  raised as new Open Decision **MD-OD-011**, carrying the same one-concept,
  no-variant-layer position forward only as the recommended option pending
  Hossam's approval. §11, §8, §42, and §43 are updated to match; no variant
  implementation is invented.
- **M31-R11 (residual approval assumptions)** — §27's "Routine
  identity/contact-detail edit ... No approval required — Confirmed" row
  assumed a position not established by any approved source, contradicting
  §27's own statement that the full approval catalogue is Open Decision
  MD-OD-005. The row is restated as a recommendation ("recommended not to
  require separate approval; final policy is part of MD-OD-005") and
  reclassified Open Decision (MD-OD-005). MD-AC-016 is reworded from "an
  authorized Approver publishes" to "an authorized actor publishes ... after
  satisfying any approval policy applicable under MD-OD-005," removing the
  residual assumption that a dedicated Approver role or specific approval
  requirement already exists. The generic confirmed control, MD-BR-046, is
  unchanged.
- **M31-R12 (Saudi launch language)** — MD-OD-007's blocking rationale
  ("can launch with VAT registration only and add fields later") made a
  production-compliance claim outside this BRD's business-analysis scope.
  The rationale now distinguishes BRD approval and the bounded Master Data
  implementation baseline (not blocked by MD-OD-007) from production launch,
  which remains gated by MESP-49 and qualified Saudi legal/tax validation of
  the required statutory fields and tax treatment. The **External Validation
  Required** classification is preserved unchanged.
- **M31-R13 (unrelated `.vscode/settings.json`)** — the PR #28 branch delta
  included `.vscode/settings.json`, introduced by unrelated commit `c5506e1`
  (a local Bitbucket-integration editor setting with no business-requirements
  content). The file is removed from the PR #28 branch delta by this
  correction commit; the setting was not altered globally, only its presence
  in this PR.

The Open Decision register now holds **eleven** decisions (MD-OD-001 through
MD-OD-011, adding Product/Item modelling as MD-OD-011). PR #28 remains **open
and unmerged**, MESP-31 remains **In Progress**, the BRD remains **Draft and
not Approved**, and **no Master Data implementation has started or may start
automatically**. The new reviewed head is the correction commit on this
branch — check `git log` on `docs/MESP-31-master-data-product-catalog-brd`
for the exact SHA, since this entry is written before that commit exists.

**MESP-94 PR #26 focused review corrections (7 August 2026):** a focused
ChatGPT review of PR #26 at reviewed head
`88146a733a65bd6070ae80a3c1b6d17c4a456efa` returned CHANGES REQUIRED BEFORE
MERGE, raising R1 (final catalogue content needs its own validation at the
exact committed SHA), R2 (`git diff --check` must cover the branch delta,
not just the working tree), R3 (guarantee
`MESP_SQLSERVER_CONNECTION_STRING` restoration), R4 (protect concurrent
validation runs from dropping each other's disposable database), R5
(unambiguous SQL-configuration-test counts), R6 (safety-catalogue parser
column counting) and R7 (bound SQL tool discovery instead of a full
recursive Program Files scan). All seven are closed at implementation SHA
`ac65e204ca4f134d4c3ae98e7871b936fe01c613`; see
`docs/96_Foundation_Release1_Safety_Validation.md`'s "Focused review
corrections (R1-R7)" section for the exact resolution of each and the
complete validation totals re-run at that commit. That correction round was
superseded by the F1-F2 round below.

**MESP-94 PR #26 F1-F2 focused review corrections (8 August 2026):** a
second focused ChatGPT review of PR #26 at reviewed head
`809a4da0e6e3804a6461e55ce34fdfaec0df690e` returned CHANGES REQUIRED BEFORE
MERGE, raising F1 (the R4 lock was session-scoped `Local\`, which does not
coordinate two processes for the same Windows user running in different
logon sessions, even though the shared automatic LocalDB instance is scoped
by Windows user, not by session) and F2 (recover safely from an abandoned
validation lock left by a prior owner that terminated unexpectedly). Both
are closed at implementation SHA `037491cee8650bfd38c4fad4d58e3baa86a3e2a4`:
the lock is now `scripts/FoundationValidationLock.ps1`, a Global-namespace
named mutex suffixed with the current Windows user's SID and ACL-restricted
to that SID, coordinating every validation run for the same Windows user
across sessions without serializing unrelated Windows users or letting one
open/signal another's lock; `Wait-FoundationValidationLock` recovers
ownership from a genuine `AbandonedMutexException` instead of treating it as
an ordinary competing run. A new focused, automated, multi-process
verification harness, `scripts/verify-foundation-validation-lock.ps1`,
proves all five required behaviors (active owner blocks entry to cleanup;
a second same-user process cannot bypass the lock; an abandoned owner is
recovered safely; the lock is released after normal completion; the lock is
released after a simulated failure) — 5/5 passed, re-run twice for
stability. See `docs/96_Foundation_Release1_Safety_Validation.md`'s
"Focused review corrections (F1-F2)" section for the exact resolution of
each and the complete validation totals re-run at that commit. The
evidence-only documentation commit recording this correction and its
validation table is `a35e71a767abc124849bd70706722834517478ed`. At that
exact final head, `SafetyCatalogueValidationTests` + `SqlServerSafetyTests`
were re-run together (25/25 passed: 4 catalogue + 21 SQL configuration/
schema/probe tests, unchanged counts), `scripts/verify-foundation-validation-lock.ps1`
was re-run (5/5 passed), and both `git diff --check` (working tree) and
`git diff --check origin/main...HEAD` (branch delta) passed clean. MESP-94
remains In Progress pending a further focused review of PR #26 at its new
pushed head, which is this same commit unless a later commit supersedes it
— check `git log` on this branch for the true tip.

**MESP-94 started (7 August 2026):** transitioned Jira MESP-94 To Do ->
In Progress and created branch `fix/MESP-94-foundation-validation-evidence`
from `main` at `9f333c9734c767673e43a30d6b57c05793e1fb69` (PR #25 merge —
MESP-93 post-merge Markdown reconciliation, closing L-3's PR #25 provenance
gap since that merge SHA was not yet known when PR #25 itself was written).
MESP-94 makes the Foundation validation tooling, SQL evidence, safety-row
classifications (rows 40, 45, 66) and checkpoint documentation say exactly
what the repository proves; it closes H-2, H-3, M-3, M-6, M-10, M-12, M-13,
M-14, M-15, L-2, L-3 and L-5. See
`docs/96_Foundation_Release1_Safety_Validation.md`'s "MESP-94 correction"
section for the exact resolution of each finding and the source-implementation
SHA/validated-repository-SHA evidence model this correction introduces.
MESP-94 is **not** marked Done yet; it remains In Progress pending PR review,
merge and post-merge closure. MESP-31 remains To Do; no Master Data
implementation has started.

**MESP-93 closure (7 August 2026, historical — superseded by "Start here" above):** PR #24 merged to `main` at
`005c796629341ab9becfbc6d1abe2ae34b6a7332` after a focused ChatGPT security
re-review verdict of APPROVED FOR MERGE at reviewed head `83b0c0e`. Post-merge
validation on `main`, rerun (not copied from pre-merge): Release build **0
warnings/0 errors**; full backend regression **566/566** passed (0 failed, 0
skipped), including **11/11** SQL Server LocalDB probes with no
`MiniErpFoundation_*` database remaining after teardown; Angular unit tests
**27/27** passed; Angular production build succeeded (351.02 kB initial /
87.80 kB transferred, unchanged); Playwright **4/4** passed; `npm audit
--omit=dev --audit-level=high` reported **0** vulnerabilities; `git diff
--check` clean. All original findings (M-1, M-4, M-5, M-7, M-8, M-9, L-4) and
all focused re-review findings (H93-01, H93-02, M93-01, M93-02, L93-01) are
closed. MESP-93 is marked **Done** in Jira. PR #23 was investigated and found
fully superseded by PR #24's own reconciliation content already on `main`
(identical or newer for every one of its 11 changed files); it was closed
without merge rather than conflict-resolved. MESP-94 is now the next eligible
Foundation correction (not yet started); MESP-31 remains To Do. The sections
below this line are the preserved historical record of the MESP-93
implementation and re-review correction sequence and are not the current
state.

**MESP-93 focused re-review correction (7 August 2026, historical):** a focused
ChatGPT/Copilot re-review of PR #24 at head `759eb04` returned CHANGES
REQUIRED BEFORE MERGE, raising H93-01, H93-02, M93-01, M93-02 and L93-01.
All five are closed at head `1820416`:

- **H93-01 (High) — closed.** A wrong-Tenant `DeliverAsync` call no longer
  mutates the owner Tenant's `TenantNotificationIntent` at all -- no
  `DeliveryState`, `FailureCategory`, `AttemptCount` or idempotency-ledger
  change, and no automatic dead-letter on the owner's behalf. The read for
  the denial result is taken under the same `syncRoot` lock as every
  legitimate mutation, closing the unlocked-mutation data race a Copilot
  review comment flagged.
- **H93-02 (High) — closed.** `INotificationRecipientAuthorizer` now
  live-revalidates the caller's own Tenant authorization path -- a
  structurally valid `TenantContext` was not previously proof of current
  authority. Both `OrdinaryMembership` (exact live Membership, Active,
  correct Tenant, no `SupportGrant` present) and `SupportGrant` (exact live
  grant/case, Active actor, not revoked, not expired, case still active, no
  `Membership` present) paths are live-checked with no cross-fallback,
  reusing the same authorization semantics as durable-work dispatch and
  reconciliation revalidation.
- **M93-01 (Medium) — closed.** `INotificationRecipientAuthorizer` is now
  registered in `AddIdentityAuthorization()` against the same
  `IdentityAuthorizationService` singleton every other Identity-owned port
  uses.
- **M93-02 (Medium) — closed.** `PrivateFileContracts.EvaluateLifecycleOutcome`
  reports a previously recorded `ChecksumFailed` or `Disposed` disposition
  with its exact classification instead of folding every non-`Available`
  state into `Expired`. `PrivateFileAccessOutcome.Disposed` was added for the
  new classification, shared between `ReadAsync` and `OverwriteAsync`.
- **L93-01 (Low) — closed.** `SafeFileName` no longer rejects an embedded
  `".."` substring (only the exact reserved names `"."`/`".."` remain
  rejected -- path separators already block real traversal), and no longer
  rejects U+200C/U+200D (ZWNJ/ZWJ), which have legitimate Arabic-script
  shaping uses and were outside the documented rejection policy. A missing
  U+2060 (word joiner) rejection test case was added.

28 new focused tests added (73 total in the MESP-93 suite), resolving all
four open Copilot review comments on PR #24. Full validation at head
`1820416`: Release build **0 warnings/0 errors**; full backend regression
**566/566** passed (0 failed, 0 skipped), including **11/11** SQL Server
LocalDB probes with no `MiniErpFoundation_*` database remaining after
teardown; Angular unit tests **27/27** passed (unchanged); Angular production
build succeeded (351.02 kB initial / 87.80 kB transferred, unchanged);
Playwright **4/4** passed; `npm audit --omit=dev --audit-level=high` reported
**0** vulnerabilities; `git diff --check` clean. MESP-93 is **not** marked
Done; PR #24 is held open, non-draft and unmerged pending a further focused
ChatGPT security re-review at head `1820416`. MESP-94 and MESP-31 remain To
Do.

**MESP-93 implementation (7 August 2026):** closes seven findings against the
merged private-file (`PrivateFileContracts.cs`) and notification
(`NotificationContracts.cs`) seams, on branch
`fix/MESP-93-private-files-notifications` based on `main` at `322341e`.

- **M-1 (foreign vs missing file existence oracle) — closed.** `ReadAsync`
  and `OverwriteAsync` now return the identical `PrivateFileAccessOutcome.NotFound`
  for a foreign-Tenant object and a genuinely missing object.
  `PrivateFileAccessOutcome.TenantDenied` is preserved only as an internal
  safe audit-evidence classification recorded in the adapter's internal
  access-evidence list; it is never the outcome a caller observes.
- **M-4 (expired/invalid object overwrite) — closed.** `OverwriteAsync` fails
  closed with `Expired` or `ChecksumFailed` for any object whose disposition
  is not `Available`, whose `ExpiresAt` has passed, or whose live-recomputed
  checksum no longer matches the recorded hash, before the concurrency check
  is even reached. An invalid object is never silently replaced.
- **M-5 (unsafe Unicode filename controls) — closed.** `SafeFileName`
  normalizes to Unicode Normalization Form C, then rejects outright (rather
  than silently truncating) any filename containing a path separator,
  traversal sequence, control character, or one of the bidi/embedding/
  isolate/mark/zero-width format characters U+202A-E, U+2066-9, U+200E,
  U+200F, U+200B, U+2060, U+FEFF. Valid Arabic, mixed Arabic/English and
  normalized composed/decomposed filenames remain fully supported and compare
  equal after normalization.
- **M-7 (unbounded notification retry) — closed.** `TenantNotificationIntent.MaxDeliveryAttempts`
  (5) bounds retry; `InMemoryNotificationAdapter` transitions to a terminal
  `DeadLetter` state at the bound and never attempts delivery again
  afterward, regardless of further caller or duplicate-worker calls.
- **M-8 (unverified notification recipient) — closed.** `TenantNotificationIntent.Create`
  now requires a `VerifiedNotificationRecipient`, obtainable only through the
  new `INotificationRecipientAuthorizer` port. `IdentityAuthorizationService`
  implements it: a recipient must be an active `GlobalUser` with an active
  `TenantMembership` in the caller's exact Tenant; a foreign-Tenant, unknown,
  suspended, revoked or pending-invitation recipient is denied. The port
  takes a `TenantContext`, so `PlatformGovernanceContext` has no path to
  become Tenant notification authority.
- **M-9 (untested returned-content immutability) — closed.** New tests prove
  mutating a returned read/overwrite byte array, or the caller's own upload
  buffer after `StoreAsync` returns, never affects stored content or a
  subsequent read; the existing defensive-copy behavior was previously
  unverified by any test.
- **L-4 (dead enum member) — closed.** The unreachable
  `PrivateFileAccessOutcome.AnonymousDenied` member is removed; all
  consumers and tests updated.

45 new focused tests added in
`backend/tests/MiniErp.ArchitectureTests/PrivateFileAndNotificationSecurityTests.cs`.
Full validation at implementation head `85b9ec1`: Release build **0
warnings/0 errors**; full backend regression **538/538** passed (0 failed, 0
skipped), including **11/11** SQL Server LocalDB probes with no
`MiniErpFoundation_*` database remaining after teardown; Angular unit tests
**27/27** passed (unchanged, no frontend files touched); Angular production
build succeeded (351.02 kB initial / 87.80 kB transferred, unchanged);
Playwright **4/4** passed; `npm audit --omit=dev --audit-level=high` reported
**0** vulnerabilities; `git diff --check` clean. No production object
storage, public URL, signed download, malware scanner, production
notification provider or physical purge was introduced. MESP-93 is **not**
marked Done; the Pull Request for this branch is held open, non-draft and
unmerged pending a focused ChatGPT security review, the same standing gate
MESP-92 carried. MESP-94 and MESP-31 remain To Do.

**MESP-92 closure (7 August 2026):** PR #22 merged to `main` at
`322341e70e56270797d5770b4b90342c20b7833e` after a focused ChatGPT security
review verdict of APPROVED FOR MERGE at reviewed head `3ec6b45`. Post-merge
validation on `main`: Release build 0 warnings/0 errors; full backend
regression **493/493** passed (0 failed, 0 skipped), including **11/11** SQL
Server LocalDB probes with no `MiniErpFoundation_*` database remaining after
teardown; Angular unit tests **27/27** passed; Angular production build
succeeded (351.02 kB initial / 87.80 kB transferred, unchanged); Playwright
**4/4** passed; `npm audit --omit=dev --audit-level=high` reported **0**
vulnerabilities. MESP-92 is marked **Done** in Jira. The sections below this
line are the preserved historical record of the MESP-92 correction sequence
and are not the current state.

**H92-06/M92-07/L92-02 closure (7 August 2026):** a focused shipping-boundary
correction found that `MiniErp.App` still granted
`[assembly: InternalsVisibleTo("MiniErp.Api")]` even after the H92-05/M92-05
correction made the effect guard, effect executor and their interfaces
`internal` — a friend assembly sees another assembly's internal members
exactly as if they were public, so that grant alone let the shipping
`MiniErp.Api` host reach `EffectGuard`/`EffectExecutor`, construct the guard
or executor directly, and call `TryReserve`/`Release`/`RecordCompleted`/
`RecordOutcomeUnknown`/`GetOutcomeUnknownReason` on the raw key. **Making a
member `internal` does not by itself prevent shipping access when the
declaring assembly grants that shipping assembly `InternalsVisibleTo`** — any
prior documentation implying otherwise is corrected by this entry. Both
findings are now closed at head `e991641`:

- H92-06 is closed: `backend/src/MiniErp.App/Properties/AssemblyInfo.cs` now
  grants `InternalsVisibleTo` only to `MiniErp.ArchitectureTests`; the grant to
  `MiniErp.Api` is removed. Rebuilding the full solution with that single
  change surfaced exactly one compile break in `MiniErp.Api`, unrelated to the
  durable-work ledger: `Program.cs`'s sign-in endpoint read the internal
  `FoundationHostSignInResult.Principal` to call `HttpContext.SignInAsync`.
  That property is now public — a narrow, intentional seam that exposes only
  the `ClaimsPrincipal` this module already issues through
  `FoundationIdentityClaims`, never a raw credential or ledger type. No
  mutable ledger type, guard, or executor was made public or given back
  friend access.
- M92-07 is closed by the same correction: `GetOutcomeUnknownReason` is
  declared only on the already-internal `IDurableWorkEffectGuard` interface,
  so removing `MiniErp.Api`'s friend grant removes its only path to that
  raw-key evidence as well. The sole production uncertain-effect evidence path
  remains `IDurableWorkStore.ReadUncertainEffectsAsync(VerifiedDurableWorkReconciliationAuthorization)`.
- L92-02 is closed: `frontend/angular.json` is restored to the exact
  `origin/main` analytics state (no `analytics` key), removing the unrelated
  identifier commit `9e0999e` had added. Verified byte-for-byte identical to
  `origin/main` for this file.
- `backend/tests/MiniErp.ArchitectureTests/FriendAssemblyPolicyTests.cs` is new
  (5 tests): reflection asserts `MiniErp.App`'s `InternalsVisibleTo` allow-list
  is exactly `["MiniErp.ArchitectureTests"]` (and contains no non-test
  assembly), and a Roslyn in-memory compilation proves source compiled under
  the assembly name `MiniErp.Api` fails with `CS0122` when it tries to
  construct `InMemoryDurableWorkEffectGuard`/`DurableWorkEffectExecutor` or
  call `TryReserve`/`Release`/`RecordOutcomeUnknown`/`GetOutcomeUnknownReason`,
  while the identical source compiled under `MiniErp.ArchitectureTests`
  succeeds. These tests were verified to fail against the prior (vulnerable)
  `InternalsVisibleTo("MiniErp.Api")` state before being verified to pass
  against this correction — they are a genuine regression proof, not just a
  restatement of the fix.
- O92-01, O92-02, H92-05 and M92-05 remain closed; all previously added tests
  for those findings continue to pass unmodified.
- Validation at this head: focused DurableWork/ledger/composition/
  reconciliation suite **238/238** passed (up from 230, the 5 new tests plus 3
  incidentally matched by a broader filter); full backend regression via
  `validate-foundation.ps1` **493/493** passed with 0 failed and 0 skipped
  (up from 488, the 5 new tests), including **11/11** SQL Server LocalDB
  probes and no `MiniErpFoundation_*` database remaining after teardown;
  Release build **0 warnings/0 errors**; Angular unit tests **27/27** passed;
  Angular production build succeeded (351.02 kB initial / 87.80 kB
  transferred, unchanged); Playwright **4/4** passed; `npm audit --omit=dev
  --audit-level=high` reported **0** vulnerabilities. MESP-92 is **not** marked
  Done; PR #22 remains open, non-draft and unmerged pending a further focused
  ChatGPT security re-review at this head. MESP-93, MESP-94 and MESP-31 remain
  To Do; no Sprint is active; MESP-48 and MESP-50 remain explicit production
  gates. The `local-prd-rename-before-MESP-92` stash was preserved untouched
  throughout this correction, and the canonical PRD blob
  (`1f9163b9412cb343a19a98312eb642ad26c1efaa` at `docs/MESP_PRD_v1.2.docx`) was
  not modified.

**Exact next action (historical — superseded, see the closure entry above):**
obtain a further focused ChatGPT security review of PR #22 at head `e991641`.
Do not merge PR #22, do not close MESP-92, and do not start MESP-93, MESP-94
or MESP-31 until that review authorizes the next step. The merge hold is a
standing process gate. **Superseded 7 August 2026:** that review completed
with verdict APPROVED FOR MERGE, PR #22 is merged, MESP-92 is Done, and
MESP-93 is now active — see "Start here" above.

**H92-05/M92-05 closure (7 August 2026):** a focused ChatGPT security
re-review of PR #22 raised H92-05 (`DurableWorkLocalRuntime` publicly exposed
the mutable effect guard, letting a shipping caller reserve, release,
complete or mark an effect uncertain outside the approved executor -- for
example releasing an in-flight reservation so a second dispatch executes the
same protected effect twice) and M92-05 (`IDurableWorkEffectGuard.GetOutcomeUnknownReason`
was reachable from a raw `DurableWorkEffectKey` alone, bypassing the H92-04
authorized reconciliation port). Both are now closed at head
`576996f94ae9ddc251767445a7ebddd60c492c45`:

- H92-05 is closed: `DurableWorkLocalRuntime`'s public surface is now limited
  to `Store` and `Dispatcher`. `EffectGuard` and `EffectExecutor` are internal
  properties, and `IDurableWorkEffectGuard`, `InMemoryDurableWorkEffectGuard`,
  `IDurableWorkEffectExecutor`, `DurableWorkEffectExecutor` and their
  state/reservation/execution-result types (`DurableWorkEffectState`,
  `DurableWorkEffectReservationKind`, `DurableWorkEffectReservation`,
  `DurableWorkEffectExecutionKind`, `DurableWorkEffectExecution`) are internal
  to `MiniErp.App`. No shipping caller outside this assembly's approved
  `DurableWorkEffectExecutor` can reserve, release, complete or mark an effect
  uncertain; `Store` and `Dispatcher` still share the identical internal
  guard and executor instance.
- M92-05 is closed: `IDurableWorkEffectGuard.GetOutcomeUnknownReason` is no
  longer reachable from any public type -- the interface itself is internal.
  The guard still preserves the O92-01 safe reason on its own `EffectRecord`;
  it is inspectable only through the internal/test-only seam
  (`InternalsVisibleTo("MiniErp.ArchitectureTests")`). The only publicly
  reachable uncertain-effect evidence path remains
  `IDurableWorkStore.ReadUncertainEffectsAsync(VerifiedDurableWorkReconciliationAuthorization)`.
- `DurableWorkEffectKey`, `DurableWorkEffectPurpose`, `DurableWorkProtectedEffectResult`
  and `DurableWorkProtectedEffectOutcome` remain public: the first two are
  required by the public `DurableWorkUncertainEffectRecord` reconciliation
  evidence, and the latter two are the return-type contract a handler author
  implementing `IDurableWorkHandler<TPayload>` must produce.
- 14 new structural/architecture tests were added in
  `DurableWorkEffectLedgerSurfaceTests.cs`, including an executable
  attack-regression test that blocks a handler mid-effect, proves no publicly
  reachable member can release the in-flight reservation, then completes the
  handler and a duplicate dispatch to confirm the effect still executed
  exactly once.
- O92-01 and O92-02 remain closed; all previously added O92-01/O92-02 tests
  continue to pass unmodified.
- Validation at this head: focused DurableWork/composition suite **230/230**
  passed (up from 216, the 14 new tests); full backend regression via
  `validate-foundation.ps1` **488/488** passed with 0 failed and 0 skipped,
  including **11/11** SQL Server LocalDB probes and no `MiniErpFoundation_*`
  database remaining after teardown; Release build **0 warnings/0 errors**;
  Angular unit tests **27/27** passed; Angular production build succeeded
  (351.02 kB initial / 87.80 kB transferred); Playwright **4/4** passed;
  `npm audit --omit=dev --audit-level=high` reported **0** vulnerabilities.
  MESP-92 is **not** marked Done; PR #22 remains open, non-draft and unmerged
  pending a further focused ChatGPT security re-review at this head. MESP-93,
  MESP-94 and MESP-31 remain To Do; no Sprint is active; MESP-48 and MESP-50
  remain explicit production gates. The `local-prd-rename-before-MESP-92`
  stash was preserved untouched throughout this correction, and the canonical
  PRD blob (`1f9163b9412cb343a19a98312eb642ad26c1efaa` at
  `docs/MESP_PRD_v1.2.docx`) was not modified.

**PRD path:** the approved PRD binary is unchanged. It moved from
`docs/MiniERPSaaSPlatform_PRD_v1.2_Final_Approved_Baseline.docx` to
`MiniERPSaaSPlatform_PRD_v1.2.docx` and now to `docs/MESP_PRD_v1.2.docx`. All
three paths resolve to the identical Git blob `1f9163b9412cb343a19a98312eb642ad26c1efaa`;
the move is recorded as a Git `R100` rename in commit
`271e9dfedce8e0ea44ef9f8d3ab6e6b61d984ac4`. Historical documents may say
"formerly `<old-name>`, now maintained at `docs/MESP_PRD_v1.2.docx`".

**MESP-92 findings after the Opus 5 project-wide review of 6 August 2026:**
0 Critical, 0 High, 0 Medium, 2 Low, none merge-blocking. Both Low findings
were closed by the bounded correction at head
`9dc6cb82860b10215d05364f2f6e25f69df3b986` (7 August 2026). A subsequent
focused ChatGPT security re-review of PR #22 at that head then raised H92-05
(High) and M92-05 (Medium); both were closed by the bounded correction at head
`576996f94ae9ddc251767445a7ebddd60c492c45` (7 August 2026; see the H92-05/M92-05
closure entry above). A follow-up shipping-boundary correction then found that
closure incomplete — H92-06 (High) and M92-07 (Medium), plus the unrelated
L92-02 (Low) scope cleanup — all now **closed** by the bounded correction at
head `e991641` (7 August 2026; see the H92-06/M92-07/L92-02 closure entry
above). No known MESP-92 code finding remains open at this head, pending the
next focused ChatGPT security re-review.

- **O92-01 (Low) — closed.** `InMemoryDurableWorkEffectGuard.RecordOutcomeUnknown`
  used to accept a `safeReason` and discard it. The guard now persists the
  sanitized reason on its own `EffectRecord` and exposes it read-only through
  `IDurableWorkEffectGuard.GetOutcomeUnknownReason`; the existing
  Reserved-only write guard already makes the transition one-way, so a
  duplicate or different-reason call cannot replace an already-recorded
  reason. An unsafe, empty or unbounded reason fails closed with
  `ArgumentException`. No public mutation surface was added.
- **O92-02 (Low) — closed.** `InMemoryDurableWorkStore.ReadUncertainEffectsAsync`
  used to fall back to `message.NextAttemptAt` when `OutcomeUnknownAt` was
  null. `DurableWorkItem` now carries its own `OutcomeUnknownAt` (set only on
  the `OutcomeUnknown` transition, mirroring `TenantOutboxMessage`'s existing
  field), and the read port fails closed with a generic
  `InvalidOperationException` — no work item id, tenant id or internal type
  name — instead of substituting `NextAttemptAt` or any other timestamp.

**Verified maturity boundary:** `DurableWorkLocalRuntime`,
`InMemoryDurableWorkStore`, `DurableWorkDispatcher` and
`TenantDurableWorkWorker` are **not referenced by `MiniErp.Api`**, and as of
the H92-06 closure at head `e991641` `MiniErp.Api` also no longer has
`InternalsVisibleTo` friend access to `MiniErp.App`'s internal ledger surface
at all. The durable-work seam is a contract plus a local adapter with test
coverage; it is not composed into the running host and is not a production
capability.

## MESP-92 In Progress — single-effect durable work and immutable payloads

- MESP-92 (`Guarantee single-effect durable work execution and immutable typed
  payloads`) is **In Progress** on branch
  `fix/MESP-92-single-effect-immutable-payloads`, based on merged-main baseline
  `32a91f27bc162685fc0db0f38b031d02ffbc99d2` (MESP-91 Done through PR #20/#21).
  PR #22 received a first focused ChatGPT security review that raised H92-01,
  H92-02, M92-01 and M92-02 (closed in the prior overlay entry below), then a
  second focused ChatGPT review that raised H92-03, H92-04, M92-03, M92-04 and
  L92-01; this entry records that second round of corrections. PR #22 remains
  open, non-draft and unmerged pending a further focused ChatGPT re-review.
- H92-03 is closed: `DurableWorkEffectComposition.CreateSharedExecutor()` is
  removed. `DurableWorkLocalRuntime.Create(operationCatalogue, payloadRegistry)`
  is the single approved composition entry point; it is the only place
  allowed to construct `InMemoryDurableWorkEffectGuard`,
  `DurableWorkEffectExecutor`, `InMemoryDurableWorkStore` and
  `DurableWorkDispatcher` (all four constructors are now `internal`), and it
  supplies the identical executor instance to the store and the dispatcher.
  `InMemoryDurableWorkStore`'s optional self-creating executor parameter is
  removed; an executor is always required. A syntax-tree architecture test
  scans the whole `backend/src` tree — every shipping project, including
  `MiniErp.Api` — and fails if any of the four types is constructed anywhere
  outside `DurableWorkLocalRuntime.cs`. That test is load-bearing because it
  matches only direct `new` expressions rather than relying on accessibility
  alone. **Historical note, corrected by the H92-06 closure below:** at the
  time this paragraph was written, `MiniErp.App` still granted
  `InternalsVisibleTo("MiniErp.Api")`, so the `internal` constructors alone did
  not yet stop the shipping host from building an independent ledger; that
  friend-assembly grant is removed as of head `e991641`.
- H92-04 is closed: `IDurableWorkStore.ReadUncertainEffectsAsync` now takes a
  server-issued `VerifiedDurableWorkReconciliationAuthorization` instead of a
  raw `TenantContext`. `IdentityAuthorizationService` (as the new
  `IDurableWorkReconciliationAuthorizer`) live-revalidates actor, session,
  Membership-or-SupportGrant validity and the dedicated catalogue-backed
  `work.reconciliation.read` permission, and reuses the same
  organization-scope ownership/containment logic as MESP-91 dispatch
  revalidation (`IsCurrentScopeContainedUnsafe`) so a missing or malformed
  selected scope fails closed. `TenantWorkScope.ContainsDescendant` then
  filters returned records to the authorized Tenant/Company/Branch/Warehouse
  boundary and its verified descendants only; a sibling organization and
  another Tenant are never visible. `PlatformGovernanceContext` has no path
  into this authorizer.
- M92-03 is closed: `DurableWorkUncertainEffectRecord` now carries the exact
  `DurableWorkEffectKey` (so `OperationId` is always present and `EventId` is
  present only for an Outbox-purpose record), the exact verified
  `TenantWorkScope`, `OutcomeUnknownAt` and a preserved safe reason.
  `TenantOutboxMessage` gained explicit `OutcomeUnknownAt`/`SafeFailureReason`
  fields; the prior reuse of `NextAttemptAt` as the occurrence time and the
  hard-coded `"outcome_unknown"` outbox reason are both removed.
- M92-04 is closed: every exception a registered payload codec raises --
  including one raised as `DurableWorkPayloadException` itself -- is
  normalized by `DurableWorkPayloadRegistry` to one of its own fixed, safe
  messages; the original exception is never attached as `InnerException`.
  `DurableWorkPayloadException`'s constructor is `internal`, so only the
  envelope/registry seam can raise one with a trusted message.
  `OperationCanceledException` still propagates unwrapped; checksum-mismatch
  and oversized-payload rejections keep their own approved fixed messages.
- L92-01 is closed: `DurableWorkLifecycle.OutcomeUnknown` and
  `IDurableWorkEffectExecutor` documentation now say a caught post-boundary
  exception, a caught cancellation, provider-reported uncertainty or a
  completion-recording failure observed by the running process -- never an
  actual process crash, which instead loses this in-memory ledger entirely
  and is not represented as any recorded outcome. Production durable crash
  recovery for this local Foundation seam remains explicitly deferred.
- H-5 is closed: submission immediately snapshots every payload into an
  immutable, checksummed `DurableWorkPayloadEnvelope` through an explicit
  `IDurableWorkPayloadRegistry`/`IDurableWorkPayloadCodec<TPayload>` pair. No
  original caller payload reference is retained by `DurableWorkItem`; every
  external byte access and every handler decode returns an independent
  defensive copy. Unknown payload types, handler/payload type mismatches,
  checksum tampering and oversized/malformed payloads fail closed before a
  handler runs. Payload type selection is a bounded registry-table lookup, not
  CLR reflection over payload-controlled data, and payload bytes never appear
  in audit or evidence.
- H-6 is closed and H92-01/H92-02 correct it further: `DurableWorkEffectKey`
  now carries a server-owned `DurableWorkEffectPurpose` (`Handler` or
  `Outbox`) plus, for an outbox effect, the immutable `EventId`, so a handler
  effect and an outbox effect for the identical Tenant/WorkItemId/OperationId
  never collide even when both are guarded by the same shared
  `IDurableWorkEffectExecutor` (`DurableWorkLocalRuntime.Create()` is now the
  one application-level authoritative composition seam; see the H92-03 entry
  above). Reservation
  remains the single non-reversible boundary — every registered handler
  invocation and every outbox effect is routed exclusively through
  `ExecuteHandlerEffectAsync` (architecture-enforced). The protected callback
  now returns an explicit `DurableWorkProtectedEffectResult` outcome —
  `Applied`, `NotAppliedRetryable`, `OutcomeUnknown` or `TerminalNotApplied` —
  instead of a generic `DurableWorkHandlerResult`; a bare generic retry can no
  longer release a reservation after an effect may already have run. A
  caught exception or cancellation observed inside the running process after
  the reservation boundary yields `OutcomeUnknown` and is never automatically
  retried; only an interruption provably before the boundary permits bounded
  retry. Completed effects replay their exact recorded safe result on
  duplicate dispatch.
- M92-01 is closed: `DurableWorkLifecycle.OutcomeUnknown` is a dedicated,
  Tenant-scoped reconciliation state for both handler work items and outbox
  messages — normal polling never selects it, the generic outbox
  redelivery/replay hook refuses to restart it, and audit records the safe
  `work.outcome-unknown`/`outbox.outcome-unknown` events with no payload or
  provider exception text. `IDurableWorkStore.ReadUncertainEffectsAsync`
  is a read-only, scope-authorized reconciliation port (see the H92-04 entry
  above for the exact-scope authorization added on top of it). No production
  reconciliation UI or provider decision is implemented.
- M92-02 is closed: the production `DurableWorkPayloadEnvelope.TamperForValidation()`
  fault-injection hook is removed; checksum-corruption tests use bounded
  reflection over the private backing field in the test project instead. A
  custom payload codec's encode/decode exception is always wrapped in the
  safe `DurableWorkPayloadException`; the original message, CLR type name and
  any payload-controlled data are never surfaced or audited.
- M-2 is closed: `Barrier`-synchronized genuinely concurrent Tasks prove one
  lease winner under active/expired-lease contention, one effect winner under
  concurrent reservation, stale-completion rejection after reclaim, and one
  effect from concurrent duplicate submissions.
- L-1 is closed: `IRelationalDurableWorkStore`/`InMemoryRelationalDurableWorkStore`
  are renamed to `IDurableWorkStore`/`InMemoryDurableWorkStore`. The type and
  its documentation no longer imply relational, SQL-backed, process-crash
  durable, production-ready or distributed exactly-once behavior.
- Outbox delivery now reports explicit `Delivered` (Applied — never repeats),
  `RetryScheduled` (NotAppliedRetryable — bounded retry), `DeadLettered`
  (TerminalNotApplied or an exhausted retry budget — never repeats) or
  `OutcomeUnknown` (never automatically repeats; requires reconciliation)
  outcomes on `OutboxDispatchResult`.
- Maturity boundary, corrected: this Foundation adapter preserves a caught
  post-boundary interruption (an exception or cancellation observed inside
  the running process) as `OutcomeUnknown`. An actual process crash loses
  this adapter's in-memory guard and lifecycle state entirely — it is not
  represented as `OutcomeUnknown` or any other recorded outcome. Immutable
  payload snapshot and stable work/effect identities are Foundation-local
  guarantees; one automatic protected-effect execution is guaranteed only
  within this local, in-memory, non-crash-durable seam; production durable
  crash recovery and distributed exactly-once delivery remain deferred to a
  future SQL/durable provider; no production SQL work store, broker or
  production worker exists.
- Validation on this branch after the second focused-review correction:
  Release build **0 warnings/0 errors**; focused DurableWork suite
  **199/199** passed; full backend regression **457/457** passed, including
  **11/11** SQL Server LocalDB probes (no `MiniErpFoundation_*` database
  remained after teardown); Angular unit tests **27/27** passed; Angular
  production build succeeded; Playwright **4/4** passed; `npm audit
  --omit=dev --audit-level=high` reported **0** vulnerabilities. MESP-92 is
  not marked Done; PR #22 is open, non-draft and held unmerged for a focused
  ChatGPT re-review. MESP-93, MESP-94 and MESP-31 remain To Do; no Sprint is
  active; MESP-48 and MESP-50 remain explicit production gates.
- Validation rerun by the Opus 5 project-wide review at head
  `271e9dfedce8e0ea44ef9f8d3ab6e6b61d984ac4`, local only (no hosted CI exists):
  Release build **0 warnings/0 errors**; backend regression **457/457** passed
  with 0 failed and 0 skipped, including **11/11** SQL Server LocalDB probes;
  no `MiniErp%` database remained in `MSSQLLocalDB` after teardown; Angular
  unit tests **27/27** passed across 5 files; Angular production build
  succeeded at 351.02 kB initial / 87.80 kB transferred; Playwright **4/4**
  passed; `npm audit --omit=dev --audit-level=high` reported **0**
  vulnerabilities. This rerun covered the **complete frontend regression**,
  closing the earlier gap where it had not been rerun after the second MESP-92
  correction.
- O92-01/O92-02 bounded correction at head
  `9dc6cb82860b10215d05364f2f6e25f69df3b986` (7 August 2026): both Low findings
  from the Opus 5 project-wide review are closed (see above). Focused
  DurableWork suite **216/216** passed; full backend regression via
  `validate-foundation.ps1` **474/474** passed with 0 failed and 0 skipped,
  including **11/11** SQL Server LocalDB probes and no `MiniErpFoundation_*`
  database remaining after teardown; Release build **0 warnings/0 errors**;
  Angular unit tests **27/27** passed; Angular production build succeeded;
  Playwright **4/4** passed; `npm audit --omit=dev --audit-level=high`
  reported **0** vulnerabilities. No known MESP-92 code finding remains open.
  MESP-92 is **not** marked Done; PR #22 remains open, non-draft and unmerged
  pending a focused ChatGPT security re-review at this head. MESP-93,
  MESP-94 and MESP-31 remain To Do; no Sprint is active; MESP-48 and MESP-50
  remain explicit production gates. The `local-prd-rename-before-MESP-92`
  stash was preserved untouched throughout this correction.

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
