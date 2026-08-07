# Repository Working Agreement

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
- The active implementation item is **MESP-93**, and it carries the explicit
  security exception to automatic merge: its Pull Request must stay open,
  non-draft and unmerged while MESP-93 stays In Progress, and work stops for
  focused ChatGPT review. Do not merge the MESP-93 Pull Request, do not close
  MESP-93, and do not start MESP-94 or MESP-31 until that review authorizes
  the next step. (MESP-92 held the same exception earlier in the sequence; PR
  #22 merged to `main` at `322341e70e56270797d5770b4b90342c20b7833e` after
  focused ChatGPT approval and MESP-92 is now Done, as are MESP-89, MESP-63,
  MESP-61 and MESP-64.)
- The canonical approved PRD is `docs/MESP_PRD_v1.2.docx`. Older references to
  `MiniERPSaaSPlatform_PRD_v1.2.docx` or
  `MiniERPSaaSPlatform_PRD_v1.2_Final_Approved_Baseline.docx` name the same
  unchanged file.
- `.ai/CURRENT_STATE.md` is the entry point for the verified current branch,
  head, active item, open Pull Request and open findings.
