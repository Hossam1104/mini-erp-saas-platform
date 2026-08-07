@AGENTS.md
# Mini ERP Delivery Rules

Authoritative sources are the approved PRD, owning BRDs, `docs/Decisions.md`,
approved ADRs, the active Lean Implementation Specification, Jira, and
`docs/94_Product_Delivery_Master_Plan.md` in that order. Read the active Jira
item and relevant approved documents before acting.

Keep Release 1 B2B-only; exclude Retail POS and Wafra-specific core behavior.
Use one implementation item at a time, review the full diff, run focused tests,
and never auto-start the next Jira item. MESP-48 and MESP-50 remain explicit
gates. Stop for Tenant leakage, auth weakness, data loss/purge, accounting
integrity, or an unresolved legal/privacy decision.

MESP-93 is Done: PR #24 merged to `main` at
`005c796629341ab9becfbc6d1abe2ae34b6a7332` (reviewed head
`83b0c0ed547dcc1b41c873ed087ab4e62d49c50e`) after focused ChatGPT security
re-review approval. PR #25 (docs) merged to `main` at
`9f333c9734c767673e43a30d6b57c05793e1fb69`. MESP-94 is now **In Progress** on
branch `fix/MESP-94-foundation-validation-evidence`, correcting Foundation
safety-catalogue classifications and validation-evidence accuracy; it uses
normal bounded review, not the MESP-92/MESP-93 manual security merge hold.
MESP-31 remains To Do and is not started; no Master Data implementation has
begun. MESP-92 carried the manual-hold exception earlier in the sequence; PR
#22 merged at `322341e70e56270797d5770b4b90342c20b7833e` after focused ChatGPT
approval and MESP-92 is Done, as are MESP-89, MESP-63, MESP-61 and MESP-64.

The canonical approved PRD is `docs/MESP_PRD_v1.2.docx`; older filenames name
the same unchanged file. Start from `.ai/CURRENT_STATE.md` for the verified
branch, head, active item, open Pull Request and open findings.
