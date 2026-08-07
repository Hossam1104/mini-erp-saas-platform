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
gates that do not block MESP-31 BRD entry. MESP-31 remains **To Do** and is
not started: Foundation completion alone does not satisfy its BRD entry
criteria, and a distinct owner approval is required before it may move to
In Progress — see `.ai/CURRENT_STATE.md`'s "MESP-31 BRD entry eligibility"
section for the exact evidence and precedent. No Master Data implementation
has begun, and none may start automatically even once MESP-31 is approved to
enter BRD drafting.

The canonical approved PRD is `docs/MESP_PRD_v1.2.docx`; older filenames name
the same unchanged file. Start from `.ai/CURRENT_STATE.md` for the verified
branch, head, active item, open Pull Request and open findings.
