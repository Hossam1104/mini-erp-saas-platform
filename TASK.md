# Next session - MESP-38 - Security, Audit, and Data Governance BRD only

## Session boundary

This is the exact next executable session after the completed MESP-37 Saudi
Localization/Core ERP BRD and the closed MESP-114 Pre-MESP-38 independent
review reconciliation. Execute only the bounded MESP-38 documentation-only
BRD session below in a fresh chat. Do not execute this prompt in the current
reconciliation session.

MESP-27 through MESP-37 are Done at their approved bounded BRD scopes.
MESP-23 remains In Progress as the living Open Questions Register. MESP-38 -
Produce Security, Audit, and Data Governance BRD - is the single next BRD
task, remains **To Do**, and must be activated only by the future MESP-38
session after fresh live-state verification. Do not activate MESP-39 or any
later task automatically.

## Objective

Produce one bounded Release 1 B2B ERP Security, Audit, and Data Governance
BRD at business-requirements level. The canonical artifact for this session
is `docs/29_Security_Audit_and_Data_Governance_BRD.md`, subject to a fresh
availability check before creation.

The BRD may define business requirements and business-testable acceptance
scenarios for security evidence, audit history, data-governance consequences,
files/private downloads, exports, monitoring, incident evidence, and
cross-module control handoffs. It must remain documentation-only. It must not
implement or prescribe application source, database schema, EF entities,
migrations, APIs, controllers, UI, providers, credentials, deployment,
production infrastructure, or production configuration.

## Required entry reading and live verification

Before changing scope or drafting, read completely and verify:

1. `AGENTS.md`;
2. `CLAUDE.md`;
3. `.ai/CURRENT_STATE.md`;
4. this `TASK.md`;
5. the canonical approved PRD `docs/MESP_PRD_v1.2.docx` structurally,
   attempting visual rendering under the documents-skill workflow when the
   local renderer is available; record unavailable tooling without making a
   visual claim;
6. `docs/94_Product_Delivery_Master_Plan.md`;
7. `docs/staticts.md`;
8. `docs/Decisions.md` and `docs/00_ERP_Business_Glossary.md`;
9. the approved upstream and adjacent BRDs, explicitly including:
   - `docs/11_SaaS_Platform_Administration_BRD.md`;
   - `docs/12_Identity_and_Access_BRD.md` (MESP-28 IAM);
   - `docs/13_Multi_Tenancy_BRD.md` (MESP-29 Multi-Tenancy);
   - `docs/14_Organization_and_Company_Structure_BRD.md` (MESP-30
     Organization);
   - `docs/15_Foundation_Release_1_Lean_Implementation_Specification.md`;
   - `docs/21_Procurement_and_Purchase_to_Pay_BRD.md`;
   - `docs/22_Inventory_and_Warehouse_Management_BRD.md`;
   - `docs/23_Finance_and_Accounting_BRD.md`;
   - `docs/24_Sales_and_Order_to_Cash_BRD.md`;
   - `docs/25_Reporting_and_Analytics_BRD.md`;
   - `docs/26_Saudi_Regulatory_Evidence_and_External_Validation_Readiness.md`;
   - `docs/27_Release_1_Saudi_Localization_Scope_Rebaseline.md`; and
   - `docs/28_Release_1_Saudi_Localization_BRD.md`;
10. `docs/01_Technology_Architecture_Baseline.md`, especially the
    actual sections covering:
    - §8 Multi-tenant isolation strategy, including request-context
      isolation and database/RLS gate language;
    - §9 Authentication and authorization strategy;
    - §12 File-storage strategy, including private objects and authorized
      downloads;
    - §13 Audit and observability strategy, distinguishing business audit
      from technical telemetry; and
    - §18 Security controls and §21 Architecture decision records required;
11. the ADR/dependency index and every available detailed record for:
    - ADR-002;
    - ADR-003;
    - ADR-004;
    - ADR-005;
    - ADR-006;
    - ADR-007;
    - ADR-008;
    - ADR-009;
    - ADR-010;
    - ADR-013;
    - ADR-014;
    - ADR-016; and
    - ADR-018.

    If an ADR is only an index entry or required future decision, do not
    manufacture a missing ADR document or treat it as approved. Record its
    current status as a named gate. ADR-011 may be read for localization
    consequences, but it is a cross-module localization dependency, not the
    primary Security/Audit/Data-Governance owner.
12. the live Jira items MESP-23, MESP-28, MESP-29, MESP-30, MESP-37,
    MESP-38, MESP-48, MESP-50, MESP-53, MESP-54, MESP-110, MESP-113, and
    the MESP-114 closure evidence; and
13. the current branch, worktree, verified `main`, and
    `origin/main`.

Record fresh MESP-38 activation evidence before drafting. Verify that
MESP-27 through MESP-37 are Done, MESP-23 is still In Progress, MESP-38 is
the single next To Do BRD and is not already being executed, MESP-113 remains
To Do/unapproved, and MESP-48, MESP-50, MESP-53, MESP-54, and MESP-110 remain
open. Do not create a duplicate MESP-38 issue.

## Binding ownership and consume-don't-redefine rule

Tenant isolation, Tenant Membership, server-derived authority, Company/Branch
scope, permission/scope semantics, separation of duties, and support-access
fundamentals are already owned by the approved Multi-Tenancy, Identity and
Access, and Organization BRDs. MESP-38 consumes and cites those baselines and
defines their Security, Audit, and Data-Governance consequences. It must not
restate, extend, weaken, renumber, or create a competing source of truth for
those controls. Any discovered gap or contradiction must be recorded against
the owning baseline, decision register, or Jira gate rather than silently
resolved inside MESP-38.

Apply the ownership boundary precisely:

- MESP-28 owns User, Tenant Membership, Role, Permission, access-scope,
  authentication/session business meaning, privileged-access fundamentals,
  support-access fundamentals, and its approved Release 1 SoD controls.
- MESP-29 owns Tenant meaning, Tenant lifecycle, Tenant context, isolation,
  default-deny cross-Tenant behavior, and Tenant-owned versus Platform-owned
  record boundaries.
- MESP-30 owns Company/Legal Entity, Branch, Warehouse, organization
  relationships, organization lifecycle, and downward organization scope.
- MESP-38 owns the security/audit/data-governance consequences, evidence
  requirements, control catalogue handoffs, and cross-module acceptance
  coverage that consume those baselines.
- MESP-33 owns physical Inventory events and its unresolved INV-OD-004 policy.
  MESP-113 is the durable decision gate; MESP-38 must not decide it.

## In scope

At business-requirements level only, cover as applicable:

- security and audit evidence for material business, configuration, access,
  denial, privileged, support, export, file, integration-boundary,
  reconciliation, and lifecycle outcomes;
- actor, Tenant, organization-scope, object, source-document, correlation,
  decision, before/after or safe-change-summary, and outcome evidence;
- immutable audit-history meaning, safe retrieval, authorized search, export
  and review boundaries, without specifying physical storage;
- business consequences of Tenant isolation, server-derived authority,
  authorization, scope, support access, and SoD while consuming the owning
  BRDs rather than redefining them;
- private attachment, download, export, quarantine, scan-state, expiry, and
  authorization boundaries at policy level;
- technical-observability and business-audit separation, safe telemetry
  properties, incident evidence, alert ownership, and failure/unknown
  outcomes without selecting a production telemetry provider or retention;
- data-governance policy boundaries for retention, deletion, legal hold,
  export, residency, privacy, offboarding, backup, and restoration while
  keeping unresolved operational values gated;
- cross-module control ownership and consequences across Platform, IAM,
  Multi-Tenancy, Organization, Master Data, Procurement, Inventory, Finance,
  Sales, Reporting, Saudi localization, Migration, and future Integrations;
- generic security/audit consequences for privileged Finance period
  close/reopen/reclose and future posting dimensions where MESP-110 affects
  evidence, without deciding Finance mechanics; and
- Given/When/Then scenarios that are business-testable and do not imply
  implementation authorization.

## Explicit exclusions and preserved gates

Do not implement or claim completion of:

- application source, tests, EF entities, tables, schemas, migrations,
  endpoints, API contracts, controllers, UI, Angular code, providers,
  infrastructure, credentials, deployment, or production configuration;
- legal advice, PDPL compliance, privacy certification, DPO/controller
  status, data-subject rights workflows, transfer-impact assessments,
  SCCs/BCRs, regulator approval, certification, or external validation;
- a retention duration, purge schedule, legal-hold duration, residency or
  hosting conclusion, backup schedule, restoration/DR behavior, RPO/RTO,
  support geography, or production deletion mechanics;
- closure of MESP-48 supported volume/performance/recovery gates;
- closure of MESP-50 retention, privacy, legal-hold, purge, residency,
  backup, restoration, hosting, or production-governance gates;
- closure of MESP-53. MESP-53 is **Report catalogue and reconciliation
  ownership**. It does not block the existence of Security/Audit
  requirements; it constrains final reporting, audit-report, export-catalogue,
  KPI/figure, schedule/distribution, and named reconciliation-owner detail;
- closure of MESP-54 exchange-rate source, cadence, effective-date,
  conversion, precision, rounding, Reporting Currency, or approval policy.
  A future material rate/configuration change may be treated generically as a
  permissioned and audited configuration action without choosing its policy;
- closure of MESP-110. MESP-38 may require generic evidence and SoD controls
  for privileged period close/reopen/reclose and future Finance posting
  dimensions, but must not decide fiscal close mechanics, retained earnings,
  Payment Term shape, aging, settlement, or posting-dimension catalogue;
- resolution of MESP-113 / INV-OD-004. MESP-38 may reference it only where
  the unresolved policy affects audit evidence, SoD, privileged Inventory
  operations, or event coverage;
- Currency implementation, statutory tax behavior, ZATCA/FATOORA,
  e-invoicing, banking, payment-provider, government, or other external
  production integration behavior;
- Retail POS, consumer checkout, cashier, cash drawer, restaurant, retail
  shift, or Wafra-specific core behavior; or
- automatic activation of MESP-39 or any later task.

Preserve PD-023 and the Saudi-localized Core ERP Release 1 B2B positioning.
Release 1 contains no production external integrations and no statutory,
legal, privacy-certification, or regulator-integrated claim.

## Architecture and ADR gate discipline

Use the current ADR index status rather than inferring approval:

- ADR-002 is the published four-project project/module enforcement record;
  preserve the actual topology and do not invent projects or module ownership.
- ADR-003 is the approved shared-database Tenant-isolation baseline; detailed
  implementation/provider validation remains gated.
- ADR-004 is the accepted Foundation Release 1 identity/cookie/session/
  antiforgery/context baseline; production providers and policy values remain
  separately gated.
- ADR-005 is the approved policy/resource-authorization baseline; consume it
  rather than redefining permission semantics.
- ADR-006 is the Foundation persistence/module-schema/transaction baseline;
  production provider, migration, and SQL validation remain gated.
- ADR-007 is the Foundation internal-event/outbox/inbox baseline; broker and
  operational production delivery/retention remain deferred.
- ADR-008 is the Foundation worker seam; deployment topology, capacity, and
  hosting remain deferred.
- ADR-009 is the private-object-storage contract baseline; provider, region,
  scanning, retention, purge, and residency remain production gates.
- ADR-010 is a required production decision for OpenTelemetry exporter,
  operational-data access, and retention; do not resolve it here.
- ADR-013 is a required production decision for secrets and encryption-key
  management; do not select a secret/key provider or lifecycle here.
- ADR-014 is a required production decision for residency, retention, legal
  hold, export, and purge; do not resolve those policy values here.
- ADR-016 is an index entry only for SQL Server Row-Level Security adoption or
  formal deferral; no ADR document may be manufactured and no RLS position
  may be silently selected.
- ADR-018 records the Foundation testing harness and validation boundary;
  production equivalence, Docker/Testcontainers CI, and production-like gates
  remain deferred.
- ADR-011 is a cross-module localization dependency for runtime localization,
  Arabic search/collation/tokenization, RTL details, and bilingual documents;
  it is not a primary Security/Audit/Data-Governance ownership decision.

Production decisions for telemetry retention, secrets/keys,
retention/residency/legal hold/export/purge, and RLS must remain named gates.
The MESP-38 BRD may state required control outcomes and evidence needed before
those gates close, but may not silently resolve any of them.

## Documentation and Jira discipline

Use one focused documentation branch and the canonical BRD file named above.
Keep all requirements at business level. Preserve approved PRD, BRD, ADR,
Product Decision, and MESP-23 identifiers. Do not promote a recommendation or
close an unresolved decision by wording.

Use the standing Owner approval for normal bounded BRD work. Record live Jira
activation, validation, Owner approval, MESP-23 handoff, final audit, closure,
and exact reviewed-content evidence on MESP-38. Keep MESP-23, MESP-48,
MESP-50, MESP-53, MESP-54, MESP-110, and MESP-113 open unless a separate
authorized decision changes them. Do not create MESP-38 implementation
Stories, parallel implementation work, or MESP-39 work.

## Validation and handoff

Before finishing the future MESP-38 session:

1. Validate the BRD against the approved scope, the consume-don't-redefine
   ownership rule, the ADR statuses, the open-gate boundaries, traceability,
   non-claims, and business acceptance scenarios.
2. Run `git diff --check` and focused Markdown/reference checks. No full
   application test suite is required for a documentation-only BRD unless
   governance requires a lightweight check.
3. Review the complete base-to-final diff. Verify that no source, tests,
   EF/schema/migration, endpoint/API, UI, provider, credential, integration,
   production configuration, Currency, tax/ZATCA/FATOORA, privacy/legal
   workflow, Retail POS, or Wafra-specific file changed.
4. Update `.ai/CURRENT_STATE.md`, `docs/94_Product_Delivery_Master_Plan.md`,
   `docs/staticts.md`, and every genuinely affected Markdown state/plan
   file conservatively. Do not increase production-capability percentages for
   a BRD. Update the root task only with the next exact session after MESP-38
   is genuinely complete.
5. Update live Jira with the exact activation, validation, approval, handoff,
   closure, reviewed-head, merge, and final-main evidence. MESP-38 must remain
   the only active task in that future session.
6. Commit and push the focused documentation branch, merge the focused PR
   only when clean and unblocked, verify `main` and `origin/main` agree,
   verify the worktree is clean, and record the final synchronized SHA.

Stop after handing off the completed MESP-38 BRD session for independent
ChatGPT review. Do not execute the next task in the same chat.
