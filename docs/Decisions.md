# Mini ERP SaaS Platform - Architecture Decision Record Index

This file is the lightweight ADR index for Release 1. The approved architecture direction is documented in [Technology Architecture Baseline](01_Technology_Architecture_Baseline.md). A full ADR is created only immediately before the related implementation or production decision becomes due. Every full ADR must record the decision, alternatives, rationale, consequences, owner, approval date, status, and superseding ADR.

## Current Product-readiness state - 9 August 2026

MESP-99 / M95-SL-02 Category and UOM is Done through PR #33, correction PR
#34, and final audit-semantics correction PR #35. MESP-101 is **Done** for the
documentation/readiness gate for M95-SL-03 Product identity after PR #36
merged at `c7392a55e0b60fd83e48447e3f9218f82cfaccea`; closure evidence is Jira
comment `10672` and activation/owner evidence is `10671`. MESP-102 is **Done**
for the bounded Product implementation through PR #37, merged at
`202d59068caac5d1fac402794627e41d7f452456`, with Jira evidence `10675`,
`10676`, and `10677`. Its Product-only bounds are
MD-OD-001, MD-OD-003, MD-OD-005, MD-OD-008, MD-OD-010, and MD-OD-011. The
readiness note records the one Product/Item Release-1 identity, hybrid
Tenant-unique SKU/barcode boundary, Product-side tracking configuration only,
Active-on-create lifecycle, Product-owned authorization/audit/concurrency, and
Tenant isolation. The implementation introduced no new ADR; ADR-002 is the
published four-project enforcement record;
ADR-005 remains the approved baseline authorization policy record; ADR-006
remains authoritative for shared SQL Server/module-owned persistence; ADR-011
is still required before localized search, forms, or bilingual/RTL document
behavior. The remaining decision register is preserved, and MESP-48, MESP-49,
and MESP-50 remain open.

## Current Supplier implementation state - 10 August 2026

MESP-103 is **Done** as the bounded M95-SL-04 Supplier readiness/decision-gate
item under MESP-6. The Owner disposition is recorded in Jira comment `10681`
and closure evidence in `10682`; the detailed readiness analysis is in
[`19_Supplier_M95_SL_04_Readiness.md`](19_Supplier_M95_SL_04_Readiness.md).
MESP-104 is also **Done** for the separately authorized Supplier
implementation. PR #39 merged to `main` at
`721adeb27c366d2b8aedde66d006ac6a49956f99` from implementation head
`9bf9afcd8a9ea427ed32b63ad9b655081e9592d3`; Jira activation, validation, and
closure evidence are comments `10685`, `10686`, and `10687`. Supplier is an
external Business Party role with no login, credential, membership,
authentication identity, or consumer session.

MD-OD-001, MD-OD-005, and MD-OD-008 are approved only for the bounded Supplier
slice: Tenant-wide availability inside the owning Tenant with no cross-Tenant
sharing and trusted server-derived authorization; no separate approver for
routine Supplier identity/contact/reference and lifecycle maintenance while
permission, Tenant authorization, optimistic concurrency, audit, and fail-
closed controls remain mandatory; and no Draft with Active-on-authorized-create
plus guarded Deactivate/Reactivate and preserved history. These dispositions do
not resolve the global register or define Business Customer, Procurement,
Finance, Tax, payment/banking, or other downstream policy. MD-OD-007 remains an
external Saudi statutory/legal validation and production gate owned by MESP-49.
The Supplier-only dispositions do not resolve Business Customer, unified Party,
consumer, Procurement, Finance, Tax, payment/banking, or other downstream
policy. MESP-105 subsequently completed the M95-SL-05 Business Customer
readiness and decision gate after the Customer-only disposition in Jira comment
`10691`; MESP-107 is now the separate active implementation item under
MESP-6, with activation evidence in comment `10692`. No Customer source
implementation behavior was added by the readiness/activation handoff.

## Current Business Customer implementation state - 10 August 2026

MESP-105 is **Done** as the M95-SL-05 readiness/decision-gate item under
MESP-6. Hossam's Customer-only Owner disposition is recorded in Jira comment
`10691`. The dedicated readiness record is
[`20_Business_Customer_M95_SL_05_Readiness.md`](20_Business_Customer_M95_SL_05_Readiness.md).
It records the B2B-only external Customer boundary, no User/login/membership/
credential/consumer identity, no unified Party, Tenant isolation, role-local
duplicate handling, and cross-role Supplier match review without rejection.

The approved Customer-only bounds are: MD-OD-001/BC-OD-001 Tenant-wide
Customer identity inside the owning Tenant with no cross-Tenant sharing and
trusted server-derived Tenant/resource authorization; MD-OD-005/BC-OD-005 no
separate approver for routine Customer master-data maintenance while
permission, authorization, concurrency, audit, fail-closed handling, and
integrity remain mandatory; and MD-OD-008/BC-OD-008 no Draft with
Active-on-authorized-create plus guarded Deactivate/Reactivate and preserved
history. Downstream commercial and sensitive policies remain with their owning
domains. MD-OD-007 remains an external Saudi statutory/legal and production
gate under MESP-49; MESP-46, MESP-47, MESP-48, MESP-50 and other downstream
ownership remain unchanged. MESP-106 is the single To Do, non-blocking shared
authorization/duplicate-audit hardening follow-up.

MESP-107 is the separately created and activated single Customer
implementation item under MESP-6, with activation evidence in Jira comment
`10692`. MESP-105 closure evidence is Jira comment `10693`. PR #40 carried the
documentation-only readiness/state handoff and merged to `main` at
`aa778038a509ad24ffabcd5d0fbb1824002451df`. No Customer source behavior was
added by that handoff, and no new ADR is required; ADR-002, ADR-005, ADR-006
and ADR-011 remain authoritative at their existing status.

ADR-002, ADR-005, ADR-006, ADR-011, and MESP-48/MESP-49/MESP-50 remain
authoritative/open at their existing status. The earlier Product hardening
observation remains carried forward; MESP-106 is the single shared follow-up,
so no duplicate hardening issue was opened.

## Historical authoritative execution state at MESP-99 activation - 9 August 2026

MESP-100 is Done with Jira closure evidence 10663; PR #32 merged at
511f6be9f005e54930f993aead9758d7a66b75a8. MESP-99 is In Progress with
activation evidence 10664 and is the single active implementation item. The
five Category/UOM-only bounds are MD-OD-001, MD-OD-005, MD-OD-008, MD-OD-002,
and MD-OD-006. ADR-002 is published and the immutable operation/capability
catalogue is implemented and tested. No Category/UOM persistence or MESP-99
business behavior was added by MESP-100. The older readiness overlay below is
retained as provenance; this section governs current status.

## Historical execution overlay - 9 August 2026 (MESP-100 readiness correction)

MESP-100 is the single active readiness-correction item for M95-SL-02. The
reviewed starting baseline is `c948a4fba8cf1ac9620474b42d56ce95f9effd52`;
MESP-96/M95-SL-01 is Done, MESP-99 remains To Do, and no Category/UOM
persistence exists. This overlay supersedes the older 8 August execution
wording below while preserving it as historical provenance.

The actual production project topology is four projects:
`MiniErp.Api`, `MiniErp.App`, `MiniErp.Contracts`, and the existing
`MiniErp.Infrastructure`. ADR-002 is now published at
`docs/ADR-002_Backend_Project_Structure_and_Module_Enforcement.md` and defines
the direct host composition path `MiniErp.Api -> MiniErp.Infrastructure`, the
provider direction `MiniErp.Infrastructure -> MiniErp.App ->
MiniErp.Contracts`, and the no-cycle/no-cross-module-persistence rules.
ADR-006 remains authoritative for shared SQL Server, Tenant ownership,
module-owned contexts/schemas/migrations, and production/provider gates.

MESP-100 records five Category/UOM-only Owner bounds for MESP-99: MD-OD-001,
MD-OD-005, MD-OD-008, MD-OD-002, and MD-OD-006. They do not resolve or
generalize any later Product, Supplier, Business Customer, Price List, Tax,
Currency, Exchange Rate, tracking, or Product/Item decision.

## Historical execution overlay - 8 August 2026 (preserved)

MESP-31 is **Done** and PR #29 closed MESP-95 at approved head
`c465d660e49a254f2fffbb95e0d07c5fcf17a193`, merged normally at
`93f4e83992ef46f498cfbfacbb513cfc3d8dda7d`. MESP-95 is **Done** with Jira
closure evidence `10654`; ChatGPT passed the final review and M95-R01,
M95-R02, and M95-R03 are closed. MESP-96/M95-SL-01 is now **complete** at its
contract-only, non-persistent boundary; PR #30 merged at actual commit
`87f150d95f583168a86aa56200916343c6404f7f` and Jira closure evidence is
comment `10655`, with the complete state recorded in `.ai/CURRENT_STATE.md`.
M95-SL-02 Category and UOM is the next exact session and is not started.

M95-SL-01 is contract-only and non-persistent. ADR-002 and the actual
repository architecture must be inspected before backend structure changes;
preserve the approved `MiniErp.Api -> MiniErp.App -> MiniErp.Contracts`
direction and do not invent a new production project or topology. The slice
does not create Master Data EF entities/tables, migrations, or `MESP` database
access solely for this work, and does not decide Product/Item, SKU/Barcode,
tracking, business availability, approval catalogue, or Draft/Active behavior.
MD-OD-001 through MD-OD-011 remain unresolved, and MESP-48, MESP-49, and
MESP-50 remain open production/external-validation gates. No Master Data
persistence exists.

| ADR | Title | Status | Required Timing | Related Jira | Detailed Source |
|---|---|---|---|---|---|
| ADR-001 | Modular Monolith, module dependency rules, and source-ownership reconciliation | Approved Baseline | Ownership reconciliation completed in owning BRDs; detailed enforcement before module implementation | MESP-22; MESP-27 to MESP-40 | [Technology Architecture Baseline](01_Technology_Architecture_Baseline.md#21-architecture-decision-records-required) |
| ADR-002 | Backend project structure and module enforcement | **Approved for M95-SL-02 module implementation; production/provider validation gated** | Published before the first data-bearing Master Data module | MESP-100; MESP-99; MESP-48; MESP-50 | [ADR-002](ADR-002_Backend_Project_Structure_and_Module_Enforcement.md) |
| ADR-003 | Shared-database tenant isolation controls | Approved Baseline | Detailed controls before tenant-scoped persistence implementation; validate before production | MESP-29; MESP-38; MESP-50 | [Technology Architecture Baseline](01_Technology_Architecture_Baseline.md#21-architecture-decision-records-required) |
| ADR-004 | Identity cookie, server session, antiforgery, context resolution and authentication-assurance policy | Accepted for Foundation Release 1 implementation | Implemented for the MESP-89 host seam; production providers remain separately gated | MESP-28; MESP-38; MESP-55; MESP-89 | [ADR-004](ADR-004_Identity_Cookie_Server_Session_Antiforgery_Context_Resolution.md) |
| ADR-005 | Policy and resource authorization model | Approved Baseline | Resource and permission details before affected module implementation | MESP-28; MESP-42; MESP-46; MESP-55 | [Technology Architecture Baseline](01_Technology_Architecture_Baseline.md#21-architecture-decision-records-required) |
| ADR-006 | Module schemas, EF Core contexts, migrations, and cross-module transactions | Foundation implementation baseline; production validation gated | Authored before durable persistence seam; SQL Server/provider evidence remains MESP-64 and production migration remains separately reviewed | MESP-61; MESP-64; MESP-48; MESP-50 | [ADR-006](ADR-006_Module_Schemas_EF_Core_Migrations_Transactions.md) |
| ADR-007 | Internal events and transactional outbox/inbox reconciliation | Foundation implementation baseline; production delivery deferred | Authored before durable outbox/inbox contracts; broker/provider and operational retention remain deferred | MESP-61; MESP-64; MESP-48; MESP-50 | [ADR-007](ADR-007_Internal_Events_Transactional_Outbox_Inbox.md) |
| ADR-008 | SQL-backed job execution and worker deployment | Foundation worker seam; deployment topology deferred | Authored before worker contract; SQL backing, capacity and hosting remain MESP-64/production decisions | MESP-61; MESP-64; MESP-48; MESP-50 | [ADR-008](ADR-008_SQL_Background_Workers_and_Ownership.md) |
| ADR-009 | Object storage, access pattern, malware scanning, and retention | Contract baseline; production storage deferred | Authored before private-file adapter; provider, region, scanning, retention, purge and residency remain production gates | MESP-61; MESP-64; MESP-50 | [ADR-009](ADR-009_Private_Object_Storage_Boundary.md) |
| ADR-010 | OpenTelemetry exporter and operational-data retention | Required before production | Instrumentation contract before implementation; exporter, access, and retention before production | MESP-38 | [Technology Architecture Baseline](01_Technology_Architecture_Baseline.md#21-architecture-decision-records-required) |
| ADR-011 | Runtime localization, Arabic search, RTL, and bilingual document generation | Required before module implementation | Before localized search, forms, and business-document implementation | MESP-31; MESP-37 | [Technology Architecture Baseline](01_Technology_Architecture_Baseline.md#21-architecture-decision-records-required) |
| ADR-012 | Production hosting topology, region, availability, RPO, and RTO | Required before production | Before production infrastructure procurement and launch approval | MESP-48; MESP-50 | [Technology Architecture Baseline](01_Technology_Architecture_Baseline.md#21-architecture-decision-records-required) |
| ADR-013 | Secret and encryption-key management | Required before production | Before production credentials, signing keys, or encrypted storage are provisioned | MESP-38; MESP-49; MESP-50 | [Technology Architecture Baseline](01_Technology_Architecture_Baseline.md#21-architecture-decision-records-required) |
| ADR-014 | Data residency, retention, legal hold, export, and purge | Required before production | After qualified privacy/legal validation and before tenant contracts or live data | MESP-50 | [Technology Architecture Baseline](01_Technology_Architecture_Baseline.md#21-architecture-decision-records-required) |
| ADR-015 | Saudi e-invoicing adapter and credential boundary | Required before production | After qualified Saudi VAT/ZATCA validation and before live Saudi invoicing | MESP-49; MESP-37 | [Technology Architecture Baseline](01_Technology_Architecture_Baseline.md#21-architecture-decision-records-required) |
| ADR-016 | SQL Server Row-Level Security adoption or formal deferral | Required before production | Decide after application-layer isolation design and before production security approval | MESP-29; MESP-38; MESP-50 | [Technology Architecture Baseline](01_Technology_Architecture_Baseline.md#21-architecture-decision-records-required) |
| ADR-017 | External partner and API authentication | Deferred until approved integration | Only when an approved Release 1 integration requires external authentication | MESP-39 | [Technology Architecture Baseline](01_Technology_Architecture_Baseline.md#21-architecture-decision-records-required) |
| ADR-018 | Testing environments, SQL Server test containers, and production-like gates | Foundation harness authored and validated; production equivalence deferred | Authored before MESP-64 LocalDB/provider validation; Docker/Testcontainers CI and production-like gates remain separately approved before launch | MESP-64; MESP-38; MESP-48; MESP-50 | [ADR-018](ADR-018_Testing_Environments_SQL_Server_Containers_and_Gates.md) |

ADR-004 is the accepted Foundation Release 1 implementation baseline. ADR-006,
ADR-007, ADR-008, ADR-009 and ADR-018 were authored for the completed MESP-61
and MESP-64 Foundation timing; they establish bounded contracts and local/test
decisions, not production provider approval. ADR-018 evidence is recorded in
`docs/96_Foundation_Release1_Safety_Validation.md`. Its original MESP-64
evidence baseline was the exact 75-assertion catalogue, 11 SQL Server tests and
a 296-test backend regression; the same catalogue and 11 SQL Server tests are
re-run on every later baseline, and the backend regression grew to **493**
tests on `main` after the MESP-92 merge (238 of them the focused
`DurableWork`/ledger/composition/reconciliation regression). The complete sequence and its maturity boundary are
recorded in `docs/97_Foundation_Completion_Review_Checkpoint.md`. ADR-002 and ADR-011
remain required before their owning module work. ADR-016 is an index entry
only — no ADR document exists yet — and remains a production decision for SQL
Server Row-Level Security adoption or formal deferral. The index controls when
each detailed record becomes mandatory and prevents production decisions from
blocking business analysis.

ADR-007 and ADR-008 carry a **Done** MESP-92 correction, merged to `main` at
`322341e70e56270797d5770b4b90342c20b7833e` (PR #22, H92-06/M92-07/L92-02
focused correction, 7 August 2026: `MiniErp.App` no longer grants
`InternalsVisibleTo("MiniErp.Api")` — the mutable effect ledger and its
executor were made `internal` by the prior H92-05/M92-05 correction, but that
friend-assembly grant alone still let the shipping `MiniErp.Api` host reach
them; removing it is the change that actually closed the compiled shipping
boundary. `DurableWorkLocalRuntime`'s public surface remains limited to
`Store`/`Dispatcher`). MESP-93 (hardening the adjacent private-file and
notification seams) is now **Done**: PR #24 merged to `main` at
`005c796629341ab9becfbc6d1abe2ae34b6a7332` after focused ChatGPT security
re-review approval; ADR-009 carries its closed findings. MESP-94 (correcting
Foundation safety-catalogue classifications and validation-evidence
accuracy, including ADR-018's validation-lock scope) is also now **Done**:
PR #26 merged to `main` at `06d837c958c1cb7977dc121e3aaea4e7278944fd` after
a ChatGPT final merge review verdict of APPROVED FOR MERGE. The Foundation
completion checkpoint that followed found no remaining Foundation correction
blocking MESP-31 BRD entry. Hossam recorded the required distinct owner
authorization on 8 August 2026; MESP-31 moved to In Progress and a draft BRD
(`docs/16_Master_Data_and_Product_Catalog_BRD.md`, v0.2 on open PR #28) is
now pending Hossam's business-owner review and is not Approved — see
`.ai/CURRENT_STATE.md`. No ADR
status in this index asserts production maturity: the durable-work store,
dispatcher, worker and effect ledger are local, in-memory, non-crash-durable
test/development seams that are **not composed into the `MiniErp.Api` host**,
and the SQL Server evidence is a disposable-LocalDB probe, not a production
provider selection. The canonical approved PRD is `docs/MESP_PRD_v1.2.docx`
(formerly `MiniERPSaaSPlatform_PRD_v1.2.docx`; contents unchanged).

No new ADR was created by the Opus 5 project-wide checkpoint of 6 August 2026:
that review found no architectural decision that is not already recorded here.

## Current Master Data approval overlay — 8 August 2026

The historical Foundation and MESP-31 status narrative above is preserved.
Hossam approved MESP-31 BRD v0.3 as the Release 1 business baseline in Jira
comment `10649` at reviewed content head
`1e2d055354f0ddde833190948d09fa426707484c`. The approval changes no ADR
decision and silently resolves none of the BRD's MD-OD-001 through MD-OD-011
decisions. ADR-002 and ADR-011 remain required before affected module
implementation; ADR-016 remains a production-timing decision only. PR #28 is
approved for merge but remains open and unmerged until the approval-state
reconciliation is pushed and reverified. MESP-95 exists as To Do and is the
next implementation-readiness item only after MESP-31 is actually merged and
closed. No Master Data source implementation has started.

## Current MESP-95 implementation-readiness overlay — 8 August 2026

The MESP-31 BRD approval is now closed through Jira comment `10650` after PR
#28 merged at actual commit `1dc4d2092d6e9a5bf8f6cfc3347e552a5ddbad1b`.
MESP-95 is the single active implementation-readiness Task and its draft
specification is `docs/17_Master_Data_and_Product_Catalog_Lean_Implementation_Specification.md`.
The review is published as PR #29, open and non-draft, from initial draft head
`dc550e1171e8f9d20cd7fdf5509dfffb7537b3bd`; it remains documentation-only and
must not be merged as source implementation.
The specification evaluates ADR-002, ADR-005, ADR-006, ADR-011, and ADR-016
without creating or updating an ADR: ADR-002 and ADR-011 remain pre-code
dependencies, ADR-016 remains production-timing only, and the existing
Foundation ADR seams remain authoritative. MD-OD-001 through MD-OD-011 remain
open and are classified as slice gates rather than resolved by technical
design. No Master Data source implementation has started; MESP-48, MESP-49,
and MESP-50 remain open gates.

## Superseding current Jira and repository execution state - 8 August 2026

The historical MESP-31/MESP-95 overlay above is retained for provenance. The
live sequence is now MESP-31 **Done**, MESP-95 **Done**, and MESP-96 **In
Progress**. PR #29 merged at `93f4e83992ef46f498cfbfacbb513cfc3d8dda7d` from
approved head `c465d660e49a254f2fffbb95e0d07c5fcf17a193`; MESP-95 closure
evidence is Jira comment `10654`.

MESP-96/M95-SL-01 is contract-only/non-persistent and must preserve all
unresolved MD-OD-001 through MD-OD-011 decisions. It may not create Master
Data persistence, EF entities/tables, migrations, or `MESP` database access
solely for the slice, and may not invent Product/Item, SKU/Barcode, tracking,
business-availability, approval-catalogue, or Draft/Active behavior. ADR-002
and the actual four-project application direction remain mandatory before
backend structure changes. MESP-48, MESP-49, and MESP-50 remain open gates.

## Superseding MESP-96 correction state - 8 August 2026

The historical execution overlays above are retained for provenance. MESP-96
is **Done**. After the original functional PR #30 merge at
`87f150d95f583168a86aa56200916343c6404f7f`, a bounded correction was made in
commit `85d3c48f20a97f8057e5960c305a3bcc0cb8d672` on
`fix/mesp-96-optional-scope-hint`, published as PR #31, and merged at
`4eeefe0d1a9af209cc3e31608812ec35ef283fd9`.

The correction makes empty and same-Tenant tenant-only scope selections
optional hints that preserve trusted server-derived Tenant/scope authority.
Exact matching trusted scope remains allowed; foreign Tenant and
sibling/foreign scope remain denied, and client input cannot broaden or
replace server authority. The original PR #30 review thread was replied to
and resolved. Focused merged-main validation passed 34/34 tests and the
Release build passed with 0 warnings and 0 errors.

No persistence, migration, database, endpoint, Product/Item, SKU/Barcode,
tracking, availability, approval-catalogue, lifecycle, Retail POS, or
Wafra-specific behavior was added. MD-OD-001 through MD-OD-011 remain open and
unresolved; MESP-48, MESP-49, and MESP-50 remain open production/external
gates. M95-SL-02 Category and UOM is the next exact session and has not been
started. Jira correction evidence is comment `10657`; the exact final
repository-state handoff is recorded in `.ai/CURRENT_STATE.md`.
