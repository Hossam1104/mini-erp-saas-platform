# Repository Working Agreement

## Current execution overlay - 8 August 2026 (MESP-95 closed; MESP-96 active)

MESP-31 is **Done** as the approved BRD baseline. PR #29 is **merged** at
actual merge commit `93f4e83992ef46f498cfbfacbb513cfc3d8dda7d`; its approved
final head is `c465d660e49a254f2fffbb95e0d07c5fcf17a193`. MESP-95 is **Done**
with Jira closure evidence in comment `10654`. ChatGPT passed the final review,
and M95-R01, M95-R02, and M95-R03 are closed. MESP-96 is the single next
executable implementation item and is **In Progress** in Jira for M95-SL-01
only. This overlay supersedes older live-state claims below.

M95-SL-01 is contract-only and non-persistent. It may not create Master Data
EF entities/tables, migrations, or an `MESP` database/access solely for this
slice, and it may not decide Product/Item identity, SKU/Barcode, tracking,
business availability, approval catalogue, or Draft/Active behavior. Retail
POS and Wafra-specific core behavior remain excluded; M95-SL-02 and later
slices remain out of scope. No Master Data persistence exists yet.

Hossam has standing Owner approval for normal BRD, specification, readiness,
merge, closure, and next-session activation while work remains inside the
approved project scope and architecture. Do not stop for ceremonial approval.
Stop only for a real security/Tenant-isolation, accounting/data-integrity,
destructive migration/data-loss, unresolved-business-decision,
legal/external-validation, credential/production-infrastructure, or material
scope/architecture blocker.

Each fresh Codex/Luna chat executes exactly one root `TASK.md` session. At the
end of every session, validate, review the complete task diff, update `TASK.md`
with the next exact session, update `.ai/CURRENT_STATE.md`, update every
genuinely affected Markdown state/plan file, update Jira, commit/push, merge
only when clean and unblocked, then STOP and return the completion report to
Hossam for ChatGPT review. Never execute the next `TASK.md` automatically in
the same chat. Run an independent Opus project review after every five
completed sessions, or earlier at a critical architecture/security,
accounting, migration/data-model, or major cross-module checkpoint.

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

- Read the active Jira item and the relevant approved PRD, BRD, ADRs, glossary,
  foundation specification, and Product Delivery Master Plan before changing
  scope.
- Release 1 is B2B ERP only. Retail POS and Wafra-specific core behavior are
  prohibited; Wafra is validation-only.
- Keep one implementation item active at a time. Do not automatically start the
  next Jira issue or create parallel work.
- Review the complete task-related diff and run targeted tests before commit or
  merge. Do not change source code for documentation/Jira-only work.
- Preserve MESP-48 supported-volume and MESP-50 retention, privacy, legal-hold,
  purge, residency, backup and restoration gates.
- Stop and escalate on Tenant leakage, authentication/authorization weakness,
  data loss or purge, accounting-integrity risk, or a legal/privacy decision
  that cannot be safely deferred.
- MESP-94 is Done: PR #26 merged to `main` at the actual merge commit
  `06d837c958c1cb7977dc121e3aaea4e7278944fd` (approved final head
  `2c7ed3dec4662672bb78967ceb70db7ed73eb7d4`) after a ChatGPT final merge
  review verdict of APPROVED FOR MERGE; it used normal bounded review, not
  the MESP-92/MESP-93 manual security merge hold. MESP-93 is Done: PR #24
  merged to `main` at `005c796629341ab9becfbc6d1abe2ae34b6a7332` (reviewed
  head `83b0c0ed547dcc1b41c873ed087ab4e62d49c50e`) after focused ChatGPT
  security re-review approval. PR #25 (docs) merged to `main` at
  `9f333c9734c767673e43a30d6b57c05793e1fb69`. MESP-92 carried the same
  manual-hold exception earlier in the sequence; PR #22 merged to `main` at
  `322341e70e56270797d5770b4b90342c20b7833e` after focused ChatGPT approval,
  and MESP-92 is Done, as are MESP-89, MESP-63, MESP-61 and MESP-64. The
  Foundation completion checkpoint following MESP-94 confirms MESP-92/93/94
  Done and MESP-48/MESP-50 as intentional open production gates, not BRD
  entry blockers. PR #27 merged that closure to `main` at
  `c86ecb851e88205f1d3907f5a5c36cfb59ce8b54`, the current merged-main
  baseline.
- MESP-31 is **In Progress** under Parent Epic `MESP-6 — EPIC 06 - Master
  Data and Product Catalog`, on branch
  `docs/MESP-31-master-data-product-catalog-brd`. Both Owner authorizations
  are recorded in live Jira: comment `10615` (BRD entry) and comment `10616`
  (future Master Data implementation, conditional). MESP-31's Jira Source
  Baseline is primary anchor PLT-003 with supporting anchors PLT-002,
  SAL-001, PROC-002, PROC-008, FIN-001, FIN-003, FIN-007, FIN-010, KSA-002,
  BR-013, ADM-003 and the applicable PRD RULE set for master-data integrity;
  PLT-011 through PLT-014 and BR-004 are Platform Administration anchors and
  are not MESP-31's baseline.
- `docs/16_Master_Data_and_Product_Catalog_BRD.md` covers Products, Product
  Categories, Units of Measure, Suppliers, Business Customers, Price Lists,
  Taxes, Payment Terms, Currencies and Exchange Rates, and is published on
  open **PR #28**. Its v0.1 head `6d0aa80` drew a business-requirements
  verdict of CHANGES REQUIRED BEFORE OWNER APPROVAL / MERGE; a bounded
  correction round produced v0.2 with ten Open Decisions (MD-OD-001–010),
  reviewed at head `8657011`, which drew a further verdict of CHANGES
  REQUIRED — FINAL SMALL CORRECTION ROUND: M31-R10 (Product/Item modelling
  needed an Owner decision rather than Confirmed status), M31-R11 (residual
  approval assumptions), M31-R12 (Saudi launch language) and M31-R13 (an
  unrelated `.vscode/settings.json` change in the PR delta). A second bounded
  correction round closed all four and produced v0.3, whose Open Decision
  register now holds eleven decisions (MD-OD-001–011, adding Product/Item
  modelling as MD-OD-011).
  The BRD is **v0.3 Approved Business Baseline**, approved by Hossam in Jira
  comment `10649` at reviewed content head
  `1e2d055354f0ddde833190948d09fa426707484c`. The Open Decision Register
  MD-OD-001 through MD-OD-011 remains preserved; approval does not silently
  resolve any of those decisions. Two Founder Decision Pack defaults remain
  explicitly unapproved and must not be treated as requirements: MESP-41
  (batch/lot/serial/expiry, now MD-OD-010) and MESP-54 (exchange-rate sourcing
  and Finance approval, owned by MESP-34). Hossam also authorized the later
  Master Data implementation phase, subject to the normal Definition of Ready
  and the dedicated MESP-95 readiness item. No Master Data implementation has
  begun and none may start automatically — see `.ai/CURRENT_STATE.md` for the
  exact live position.
- The canonical approved PRD is `docs/MESP_PRD_v1.2.docx`. Older references to
  `MiniERPSaaSPlatform_PRD_v1.2.docx` or
  `MiniERPSaaSPlatform_PRD_v1.2_Final_Approved_Baseline.docx` name the same
  unchanged file.
- `.ai/CURRENT_STATE.md` is the entry point for the verified current branch,
  head, active item, open Pull Request and open findings.
