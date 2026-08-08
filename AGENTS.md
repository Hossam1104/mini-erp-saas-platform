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
  entry blockers. Hossam recorded the required distinct owner authorization
  on 8 August 2026, and MESP-31 moved to **In Progress** on branch
  `docs/MESP-31-master-data-product-catalog-brd`, producing a v0.1 draft of
  `docs/16_Master_Data_and_Product_Catalog_BRD.md` covering Products, Product
  Categories, Units of Measure, Suppliers, Business Customers, Price Lists,
  Taxes, Payment Terms, Currencies and Exchange Rates. The draft is pending
  Hossam's business-owner review and is not Approved. Hossam also
  pre-authorized the later Master Data implementation phase, but that
  authorization only becomes executable after this BRD is approved and a
  dedicated implementation Jira item, separate from MESP-31, is identified
  and activated. No Master Data implementation has begun and none may start
  automatically — see `.ai/CURRENT_STATE.md` for the exact live position.
- The canonical approved PRD is `docs/MESP_PRD_v1.2.docx`. Older references to
  `MiniERPSaaSPlatform_PRD_v1.2.docx` or
  `MiniERPSaaSPlatform_PRD_v1.2_Final_Approved_Baseline.docx` name the same
  unchanged file.
- `.ai/CURRENT_STATE.md` is the entry point for the verified current branch,
  head, active item, open Pull Request and open findings.
