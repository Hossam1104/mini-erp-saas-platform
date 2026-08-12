# Next session - MESP-39 - Integrations and External Services BRD only

## Session boundary

This is the exact next executable session after the completed MESP-38
Security, Audit, and Data Governance BRD and its focused PR #57 merge. Execute
only the bounded MESP-39 documentation-only BRD session below in a fresh chat.
Do not execute this prompt in the current MESP-38 completion and
state-synchronization session.

MESP-27 through MESP-38 are Done at their approved bounded BRD scopes.
MESP-23 remains In Progress as the living Open Questions Register. MESP-39 -
Produce Integrations and External Services BRD - is the single next BRD task
and remains To Do. It must be activated only by the future MESP-39 session
after fresh live-state verification. Do not activate MESP-40 or any later
task automatically.

## Objective

Produce one bounded Release 1 B2B ERP Integrations and External Services BRD
at business-requirements level. The canonical artifact for that session is
docs/30_Integrations_and_External_Services_BRD.md, subject to a fresh
availability check before creation.

The BRD may define the business contract, ownership, risk boundaries,
failure behavior, reconciliation expectations, and business-testable
acceptance scenarios for integrations and external services. It must remain
documentation-only. It must not implement or prescribe source code, database
schema, EF entities, migrations, endpoint/API design, controllers, UI,
providers, credentials, deployment, production infrastructure, or production
configuration.

## Required entry reading and live verification

Before changing scope or drafting, read completely and verify:

1. AGENTS.md;
2. CLAUDE.md;
3. .ai/CURRENT_STATE.md;
4. this TASK.md;
5. the canonical approved PRD docs/MESP_PRD_v1.2.docx structurally,
   attempting visual rendering under the documents workflow when the local
   renderer is available; record unavailable tooling without making a visual
   claim;
6. docs/94_Product_Delivery_Master_Plan.md;
7. docs/staticts.md;
8. docs/Decisions.md and docs/00_ERP_Business_Glossary.md;
9. the approved upstream and adjacent BRDs, including:
   - docs/11_SaaS_Platform_Administration_BRD.md;
   - docs/12_Identity_and_Access_BRD.md;
   - docs/13_Multi_Tenancy_BRD.md;
   - docs/14_Organization_and_Company_Structure_BRD.md;
   - docs/15_Foundation_Release_1_Lean_Implementation_Specification.md;
   - docs/21_Procurement_and_Purchase_to_Pay_BRD.md;
   - docs/22_Inventory_and_Warehouse_Management_BRD.md;
   - docs/23_Finance_and_Accounting_BRD.md;
   - docs/24_Sales_and_Order_to_Cash_BRD.md;
   - docs/25_Reporting_and_Analytics_BRD.md;
   - docs/26_Saudi_Regulatory_Evidence_and_External_Validation_Readiness.md;
   - docs/27_Release_1_Saudi_Localization_Scope_Rebaseline.md;
   - docs/28_Release_1_Saudi_Localization_BRD.md; and
   - docs/29_Security_Audit_and_Data_Governance_BRD.md;
10. docs/01_Technology_Architecture_Baseline.md, especially the actual
    sections covering:
    - Tenant isolation and server-derived context;
    - authentication, authorization, and support access;
    - private object storage and authorized downloads;
    - business audit versus technical telemetry;
    - external-service and integration boundaries;
    - security controls and production gates; and
    - the architecture decision record index;
11. docs/Decisions.md and every available detailed record relevant to
    integrations and external services, including ADR-002, ADR-003, ADR-004,
    ADR-005, ADR-006, ADR-007, ADR-008, ADR-009, ADR-010, ADR-013, ADR-014,
    ADR-016, ADR-017, and ADR-018. If an ADR is only an index entry or a
    required future decision, do not manufacture a missing record or treat it
    as approved. ADR-011 remains a localization dependency;
12. live Jira MESP-23, MESP-38, MESP-39, MESP-40, MESP-48, MESP-50, MESP-53,
    MESP-54, MESP-110, MESP-113, and the MESP-38 closure evidence; and
13. the current branch, worktree, verified main, and origin/main.

Record fresh MESP-39 activation evidence before drafting. Verify that
MESP-27 through MESP-38 are Done, MESP-23 is still In Progress, MESP-39 is
the single next To Do BRD and is not already being executed, MESP-40 remains
To Do/not activated, and the named open gates remain open. Do not create a
duplicate MESP-39 issue.

## Binding ownership and consume-don't-redefine rule

MESP-39 owns the business meaning of approved integration and external-service
contracts only within its bounded scope. It consumes the already approved
Tenant, IAM, Organization, Security/Audit/Data Governance, and domain
baselines without creating competing sources of truth.

Apply the ownership boundary precisely:

- MESP-27 owns Platform administration, Tenant lifecycle, support,
  offboarding, export, and Platform governance;
- MESP-28 owns User, Tenant Membership, Role, Permission, authentication/
  session meaning, support fundamentals, and approved SoD;
- MESP-29 owns Tenant meaning, context, lifecycle, isolation, default deny,
  and Tenant-owned versus Platform-owned records;
- MESP-30 owns Company/Legal Entity, Branch, Warehouse, organization
  relationships, lifecycle, and downward scope;
- MESP-31 through MESP-37 own their approved master-data, Procurement,
  Inventory, Finance, Sales, Reporting, and Saudi-localization source facts;
- MESP-38 owns security/audit/data-governance evidence, private artifact
  consequences, incident evidence, and cross-module control handoffs;
- MESP-39 owns integration-specific business actors, contracts, lifecycle,
  statuses, failure/retry/idempotency/reconciliation consequences, and
  external-service boundaries;
- MESP-40 and later migration work own migration and cutover detail;
- MESP-48, MESP-50, MESP-53, MESP-54, MESP-110, MESP-113, and named ADRs
  remain separate gates; and
- MESP-39 must not turn a future integration candidate into a Release 1
  production commitment.

## Scope in focus

At business-requirements level only, cover as applicable:

- email and notification service boundaries;
- private object storage as a business service dependency, without choosing a
  provider or physical topology;
- public API and webhook business contracts without defining endpoint design;
- bank-feed and payment-service boundaries as future or gated capabilities;
- tax and e-invoicing exchange boundaries without implementing statutory
  behavior or selecting a Saudi regulator integration;
- identity-provider single sign-on as a future/gated external identity
  boundary;
- integration security, authorization, Tenant scope, secrets/key gates,
  privacy-oriented minimization, and external-data handling;
- request, delivery, retry, duplicate, timeout, unavailable, rejected,
  unknown, and reconciled outcomes;
- idempotency and no-silent-loss business rules for inventory, tax, invoice,
  payment, and accounting effects;
- reconciliation ownership, evidence, correction, and operational handoff;
- integration imports/exports, migration, reporting, audit, monitoring,
  localization, Finance, Inventory, Sales, and Platform consequences; and
- Given/When/Then scenarios that do not imply implementation authorization.

## Explicit exclusions and preserved gates

Do not implement or claim completion of:

- application source, tests, schema, persistence, EF, migrations, APIs,
  controllers, screens, Angular code, providers, credentials, deployment,
  infrastructure, production configuration, or external production setup;
- statutory tax treatment, ZATCA/FATOORA, e-invoice XML/QR/signing/
  certification, government submission, taxpayer applicability, or legal
  compliance;
- bank connectivity, payment-provider production use, production identity
  provider, or any external credential/key exchange;
- closure of MESP-23, MESP-48, MESP-50, MESP-53, MESP-54, MESP-110, MESP-113,
  ADR-010, ADR-013, ADR-014, ADR-016, ADR-017, or any other named gate;
- retention duration, residency/hosting, support geography, backup/
  restoration, RPO/RTO, privacy/legal certification, or purge mechanics;
- Currency policy, exchange-rate source, Finance period mechanics, Inventory
  tracking policy, or Reporting catalogue;
- Retail POS, consumer checkout, cashier, cash drawer, restaurant, retail
  shift, or Wafra-specific core behavior; and
- automatic activation of MESP-40 or any later item.

Preserve PD-023 and the Saudi-localized Core ERP Release 1 B2B positioning.
Release 1 contains no production external integrations and no statutory,
legal, privacy-certification, regulator-integrated, bank, or payment-provider
claim.

## Architecture and ADR gate discipline

Use current index status rather than inferring approval:

- ADR-002 remains the published four-project structure; do not invent
  projects or integration modules;
- ADR-003/004/005/006 remain the Tenant, identity, authorization,
  persistence, and transaction baselines;
- ADR-007/008 define Foundation internal events, durable work, and worker
  seams while broker, hosting, topology, delivery, and retention remain
  deferred;
- ADR-009 defines the private-object-storage contract while provider, region,
  scanning, retention, purge, residency, and keys remain gated;
- ADR-010 requires a production decision for telemetry exporter, operational
  data access, and retention;
- ADR-013 requires a production decision for secrets and encryption keys;
- ADR-014 requires a production decision for residency, retention, legal
  hold, export, and purge;
- ADR-016 is index-only for SQL Server RLS adoption or formal deferral;
- ADR-017 is the external partner/API authentication dependency and must not
  be manufactured if only an index entry is available;
- ADR-018 preserves the Foundation testing/production-equivalence boundary;
  and
- ADR-011 remains the localization/search/RTL dependency.

The BRD may state required business outcomes and evidence before these gates
close, but may not select a provider, credential, protocol implementation,
region, exporter, key lifecycle, retention value, or production topology.

## Documentation and Jira discipline

Use one focused documentation branch and the canonical BRD file named above.
Keep requirements at business level and preserve PRD, BRD, ADR, Product
Decision, and MESP-23 identifiers.

Use the standing Owner approval for normal bounded BRD work. Record live Jira
activation, validation, Owner approval, MESP-23 handoff, final audit, closure,
reviewed-content, merge, and final-main evidence on MESP-39. Keep MESP-23,
MESP-48, MESP-50, MESP-53, MESP-54, MESP-110, and MESP-113 open unless a
separate authorized decision changes them. Do not create implementation
Stories, parallel work, or MESP-40 work.

## Validation and handoff

Before finishing the future MESP-39 session:

1. Validate the BRD against the approved source scope, ownership matrix, ADR
   statuses, open-gate boundaries, traceability, non-claims, failure/
   unknown/idempotency rules, and business acceptance scenarios.
2. Run git diff --check and focused Markdown/reference checks. No full
   application test suite is required for a documentation-only BRD unless
   governance requires a lightweight check.
3. Review the complete base-to-final diff and verify no source, schema,
   migration, endpoint/API, UI, provider, credential, external production,
   statutory/tax, legal/privacy workflow, Retail POS, or Wafra-specific file
   changed.
4. Update .ai/CURRENT_STATE.md, docs/94_Product_Delivery_Master_Plan.md,
   docs/staticts.md, and every genuinely affected Markdown state/plan file
   conservatively. Do not increase production-capability percentages for a
   BRD. Rewrite root TASK.md only with the next exact session after MESP-39
   is genuinely complete.
5. Update live Jira with exact activation, validation, approval, MESP-23
   handoff, final audit, closure, reviewed head, merge, and final-main
   evidence. MESP-39 must remain the only active task in that future session.
6. Commit and push the focused documentation branch, merge the focused PR
   only when clean and unblocked, verify main and origin/main agree, verify
   the worktree is clean, and record the final synchronized SHA.

Stop after handing off the completed MESP-39 BRD for independent ChatGPT
review. Do not execute the next task in the same chat.
