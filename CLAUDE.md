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

MESP-89 is a security-task merge exception: after implementation, validation,
and a non-draft Pull Request, stop for focused ChatGPT review. Do not merge or
close MESP-89, and do not start MESP-63, MESP-61, or MESP-64 until that review
authorizes the next step.
