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

MESP-92 is the active implementation item and the current security-task merge
exception: after implementation, validation, and a non-draft Pull Request, stop
for focused ChatGPT review. Do not merge PR #22 or close MESP-92, and do not
start MESP-93, MESP-94 or MESP-31 until that review authorizes the next step.
MESP-89 held this exception earlier in the sequence and is now Done, as are
MESP-63, MESP-61 and MESP-64.

The canonical approved PRD is `docs/MESP_PRD_v1.2.docx`; older filenames name
the same unchanged file. Start from `.ai/CURRENT_STATE.md` for the verified
branch, head, active item, open Pull Request and open findings.
