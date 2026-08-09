@AGENTS.md

## Current execution overlay - 9 August 2026 (MESP-101 Product readiness)

MESP-100 is Done with closure evidence `10663`. MESP-99 / M95-SL-02 is Done
through PR #33, correction PR #34, and final audit-semantics correction PR #35,
with final correction merge `3e51f98f8c80b9989632499632605894c18570cf`.
MESP-101 is the single active bounded readiness item for M95-SL-03 Product
identity, activated with owner evidence in Jira comment `10671`.
The Product-only bounds are MD-OD-001, MD-OD-003, MD-OD-005, MD-OD-008,
MD-OD-010, and MD-OD-011 as recorded in `docs/18_Product_Identity_M95_SL_03_Readiness.md`.
This readiness session adds no Product source behavior, persistence, migration,
endpoint, or UI. The root `TASK.md` is being handed off to the exact next
Product identity implementation session.
The actual backend consists of `MiniErp.Api`, `MiniErp.App`,
`MiniErp.Contracts`, and `MiniErp.Infrastructure`; detailed project/module
enforcement is in ADR-002 and shared SQL Server/Tenant/module persistence
controls remain governed by ADR-006. MESP-48, MESP-49, and MESP-50 remain open.

## Historical execution overlay - 8 August 2026 (preserved)

MESP-31 is **Done**. PR #29 merged at actual commit
`93f4e83992ef46f498cfbfacbb513cfc3d8dda7d` from approved head
`c465d660e49a254f2fffbb95e0d07c5fcf17a193`. MESP-95 is **Done** with Jira
closure evidence `10654`; ChatGPT passed the final review and M95-R01,
M95-R02, and M95-R03 are closed. MESP-96 is the single next executable item,
**In Progress** in Jira, and is limited to M95-SL-01. This overlay supersedes
older live-state claims below.

M95-SL-01 is contract-only and non-persistent. Do not create Master Data EF
entities/tables, migrations, or an `MESP` database/access solely for this
slice. Do not decide Product/Item, SKU/Barcode, tracking, business
availability, approval catalogue, or Draft/Active behavior. Retail POS,
Wafra-specific core behavior, and M95-SL-02 remain out of scope. No Master
Data persistence exists yet.

Hossam has standing Owner approval for normal BRD/specification/readiness,
merge/closure, and next-session activation within approved scope and
architecture; stop only for a real security, Tenant-isolation,
accounting/data-integrity, destructive migration/data-loss,
legal/external-validation, credential/infrastructure, unresolved-business-
decision, or material scope/architecture blocker. One fresh Codex/Luna chat
executes exactly one root `TASK.md` session, validates, updates all genuinely
affected state, updates Jira, commits/pushes, merges only when clean, then
STOPs for ChatGPT review. Never automatically execute the next task. Run an
independent Opus review after every five sessions or earlier at a critical
architecture/security/financial/data-model checkpoint.
# Mini ERP Delivery Rules

## Current approval overlay — 8 August 2026

MESP-31 BRD v0.3 is an **Approved Business Baseline** and is now **Done** in
live Jira. Hossam's approval is recorded in comment `10649`; final closure
evidence is in comment `10650`. PR #28 is merged at actual merge commit
`1dc4d2092d6e9a5bf8f6cfc3347e552a5ddbad1b`, with final PR head
`8396197b54189cb550f07bd4bb6779fd38ac30cb` confirmed as an ancestor of
`main`. The Open Decision Register MD-OD-001 through MD-OD-011 remains
preserved; approval resolves none of those decisions, and blocking decisions
remain implementation-slice gates. MESP-95 is the single active Jira item,
In Progress, on branch `docs/MESP-95-master-data-lean-implementation-spec`.
Its Draft implementation-readiness specification is documentation-only; no
Master Data source implementation has started, no Jira child slice is active,
and no next coding item may start automatically. The review is published as
PR #29 (Open, non-draft) from initial draft head
`dc550e1171e8f9d20cd7fdf5509dfffb7537b3bd`; it must not be merged or followed
by source implementation automatically.

Authoritative sources are the approved PRD, owning BRDs, `docs/Decisions.md`,
approved ADRs, the active Lean Implementation Specification, Jira, and
`docs/94_Product_Delivery_Master_Plan.md` in that order. Read the active Jira
item and relevant approved documents before acting.

Keep Release 1 B2B-only; exclude Retail POS and Wafra-specific core behavior.
Use one implementation item at a time, review the full diff, run focused tests,
and never auto-start the next Jira item. MESP-48 and MESP-50 remain explicit
gates. Stop for Tenant leakage, auth weakness, data loss/purge, accounting
integrity, or an unresolved legal/privacy decision.

MESP-94 is Done: PR #26 merged to `main` at the actual merge commit
`06d837c958c1cb7977dc121e3aaea4e7278944fd` (approved final head
`2c7ed3dec4662672bb78967ceb70db7ed73eb7d4`, correcting Foundation
safety-catalogue classifications and validation-evidence accuracy) after a
ChatGPT final merge review verdict of APPROVED FOR MERGE; it used normal
bounded review, not the MESP-92/MESP-93 manual security merge hold. MESP-93
is Done: PR #24 merged to `main` at
`005c796629341ab9becfbc6d1abe2ae34b6a7332` (reviewed head
`83b0c0ed547dcc1b41c873ed087ab4e62d49c50e`) after focused ChatGPT security
re-review approval. PR #25 (docs) merged to `main` at
`9f333c9734c767673e43a30d6b57c05793e1fb69`. MESP-92 carried the manual-hold
exception earlier in the sequence; PR #22 merged at
`322341e70e56270797d5770b4b90342c20b7833e` after focused ChatGPT approval and
MESP-92 is Done, as are MESP-89, MESP-63, MESP-61 and MESP-64.

A Foundation completion checkpoint following MESP-94 confirmed MESP-92,
MESP-93 and MESP-94 Done, and MESP-48/MESP-50 as intentional open production
gates that do not block MESP-31 BRD entry. PR #27 merged that closure to
`main` at `c86ecb851e88205f1d3907f5a5c36cfb59ce8b54`, the current merged-main
baseline.

MESP-31 is **In Progress** under Parent Epic
`MESP-6 — EPIC 06 - Master Data and Product Catalog`, on branch
`docs/MESP-31-master-data-product-catalog-brd`. Both Owner authorizations are
recorded in live Jira: comment `10615` (BRD entry) and comment `10616`
(future Master Data implementation, conditional). MESP-31's Jira Source
Baseline is primary anchor PLT-003 with supporting anchors PLT-002, SAL-001,
PROC-002, PROC-008, FIN-001, FIN-003, FIN-007, FIN-010, KSA-002, BR-013,
ADM-003 and the applicable PRD RULE set for master-data integrity; PLT-011
through PLT-014 and BR-004 are Platform Administration anchors and are not
MESP-31's baseline.

`docs/16_Master_Data_and_Product_Catalog_BRD.md` (Products, Product
Categories, Units of Measure, Suppliers, Business Customers, Price Lists,
Taxes, Payment Terms, Currencies, Exchange Rates) is published on open
**PR #28**. Its v0.1 head `6d0aa80` drew a business-requirements verdict of
CHANGES REQUIRED BEFORE OWNER APPROVAL / MERGE; a bounded correction round
produced v0.2 (ten Open Decisions, MD-OD-001–010), reviewed at head
`8657011`, which drew a further verdict of CHANGES REQUIRED — FINAL SMALL
CORRECTION ROUND: M31-R10 (Product/Item modelling needed an Owner decision
rather than Confirmed status), M31-R11 (residual approval assumptions),
M31-R12 (Saudi launch language) and M31-R13 (an unrelated
`.vscode/settings.json` change in the PR delta). A second bounded correction
round closed all four and produced v0.3, whose Open Decision register now
holds eleven decisions (MD-OD-001–011, adding Product/Item modelling as
MD-OD-011). The BRD is **v0.3 Approved Business Baseline**, approved by
Hossam in Jira comment `10649` at reviewed content head
`1e2d055354f0ddde833190948d09fa426707484c`. The Open Decision Register
remains preserved; approval silently resolves none of MD-OD-001 through
MD-OD-011. Two Founder Decision Pack defaults remain explicitly **not**
approved and must not be treated as requirements: MESP-41
(batch/lot/serial/expiry — now MD-OD-010) and MESP-54 (exchange-rate sourcing
and Finance approval — owned by MESP-34). Hossam also authorized the later
Master Data implementation phase, subject to the normal Definition of Ready
and the dedicated MESP-95 readiness item. No Master Data implementation has
begun, and none may start automatically — see `.ai/CURRENT_STATE.md` for the
exact live position.

The canonical approved PRD is `docs/MESP_PRD_v1.2.docx`; older filenames name
the same unchanged file. Start from `.ai/CURRENT_STATE.md` for the verified
branch, head, active item, open Pull Request and open findings.
