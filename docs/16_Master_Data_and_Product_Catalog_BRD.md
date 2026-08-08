# Mini ERP SaaS Platform — Master Data and Product Catalog BRD

## 1. Document Control

| Field | Value |
|---|---|
| Document | Master Data and Product Catalog Business Requirements Document |
| Jira | MESP-31 — Produce Master Data and Product Catalog BRD |
| Parent Epic | Presumed `MESP-6 — EPIC 06 - Master Data and Product Catalog`, inferred from the established Epic-numbering pattern (Epic MESP-2 → MESP-27, MESP-3 → MESP-28, MESP-4 → MESP-29, MESP-5 → MESP-30). **Not independently confirmed** in `docs/94_Product_Delivery_Master_Plan.md` or `docs/90_MVP_Founder_Decision_Pack.md`; verify directly against Jira before citing as fact. |
| Version | v0.1 — Draft for Owner Review |
| Status | **Draft pending Hossam's business-owner review.** Not Approved. This document does not authorize implementation. |
| Accountable owner | Hossam, Product Owner and founder approver |
| Prepared by | Claude (Sonnet 5), acting as the delivery agent under Hossam's direction, following the drafting role used for MESP-27/28/29/30 |
| Date | 8 August 2026 |
| Canonical PRD | `docs/MESP_PRD_v1.2.docx`, PRD v1.2 Final Approved Baseline (formerly `MiniERPSaaSPlatform_PRD_v1.2.docx`; contents unchanged) |
| Required glossary | `docs/00_ERP_Business_Glossary.md` |
| Related approved BRDs | `docs/11_SaaS_Platform_Administration_BRD.md`; `docs/12_Identity_and_Access_BRD.md`; `docs/13_Multi_Tenancy_BRD.md`; `docs/14_Organization_and_Company_Structure_BRD.md` |
| Architecture reference | `docs/01_Technology_Architecture_Baseline.md` (constraint reference only; does not dictate business requirements) |
| Delivery reference | `docs/94_Product_Delivery_Master_Plan.md`; `docs/90_MVP_Founder_Decision_Pack.md` |
| Jira state at drafting | MESP-31 In Progress (BRD-entry authorization and future implementation authorization recorded by the Owner on 8 August 2026 — see §41); no downstream implementation Jira item exists or has started |
| Development environment decision (non-business) | The Owner has selected local SQL Server (instance `.`, database `MESP`) as the development environment for the later, separately gated implementation phase. This is an implementation/environment decision, not a business rule, carries no business meaning, and is Out of Scope for this BRD's content. No credential of any kind appears in this document. |
| Classification summary | See §43 Coverage Checklist for the exact rule/scenario/decision counts produced by this draft. |

### Classification legend

| Classification | Meaning |
|---|---|
| **Confirmed** | Directly supported by the approved PRD, the approved glossary, an approved adjacent BRD, or an existing Jira/founder-decision-pack requirement. |
| **Confirmed — Founder-approved Release 1 requirement** | Explicitly approved by Hossam for the Release 1 business baseline (including the scope authorization recorded for this BRD on 8 August 2026) and carried forward without adding implementation behavior. |
| **Open Decision** | A genuine business decision still requiring Hossam's recorded approval. Only the `MD-OD-*` register uses this classification. |
| **Deferred Gate** | Deliberately owned by a later domain BRD (MESP-32 through MESP-40, MESP-46, MESP-49), MESP-48, MESP-50, or a later approval. No value is invented here. |
| **External Validation Required** | Requires confirmation from Saudi legal, tax, or accounting authority beyond this BRD's business-analysis scope. |
| **Out of Scope** | Explicitly excluded from this BRD or owned by another domain in full. |

This is a business-requirements document. It authorizes no API, database, UI, code, automated test, Sprint, or implementation Jira work. See §41 for the exact Owner authorizations this draft relies on and the condition that still gates implementation.

## 2. Executive Summary

**Classification: Confirmed.** Master Data and Product Catalog is the shared, reusable business-fact layer that every other Release 1 domain consumes: Procurement reads Supplier and Product facts to raise a Purchase Order; Inventory reads Product and Unit of Measure facts to move stock; B2B Sales reads Business Customer, Product, and Price List facts to quote and invoice; Finance reads Currency, Exchange Rate, Payment Term, and Tax facts to post a balanced ledger entry. A Product, Supplier, or Tax rate defined once and reused everywhere is what keeps these domains consistent with each other; defining the same fact twice, inconsistently, in two domains is the specific failure this BRD prevents.

**Classification: Confirmed.** Master Data lives inside the approved Tenant boundary (`docs/13_Multi_Tenancy_BRD.md`). Every master record this BRD defines is Tenant-owned business data: it is private to its Tenant by default, is never visible or reusable across a Tenant boundary, and its duplicate checks, search, import, and export never leak another Tenant's values. Nothing in this BRD weakens or reinterprets the Multi-Tenancy or Organization BRDs' approved isolation, scope, or hierarchy rules.

**Classification: Confirmed — Founder-approved Release 1 requirement.** This BRD is reusable, configuration-led product content. It defines no Wafra-specific schema, rule, permission, workflow, report, status, price rule, tax behavior, or UX. Wafra is validation evidence only, exactly as the approved PRD and the Multi-Tenancy/Organization BRDs already require.

**Classification: Confirmed.** A master record that a posted transaction has already referenced is never deleted. It is deactivated. This single rule — restated for every one of the ten domains in this BRD — is what keeps historical Purchase Orders, Sales Orders, invoices, and ledger entries reproducible after the Product, Supplier, Tax rate, or Exchange Rate they used has since changed or been retired.

**Classification: Confirmed.** Several of the ten domains this BRD is required to cover — Supplier, Business Customer, Price List, Tax, Payment Term, Currency, and Exchange Rate — are, in the currently approved glossary, formally *owned* by a later domain module (Procurement, B2B Sales, Finance, or the Saudi Country Pack), not by "Master Data and Catalog." §9 Ownership Boundaries resolves this directly: MESP-31 establishes the shared master-record identity, lifecycle, and cross-module data contract for all ten domains; the named owning module's own BRD (MESP-32 through MESP-40, MESP-46, MESP-49) remains authoritative for that domain's transactional business behavior. Nothing here silently reassigns glossary ownership.

## 3. Business Purpose

**Classification: Confirmed.** MESP-31 exists to define the reusable, Tenant-isolated business meaning of the master data every downstream ERP transaction depends on, before any of Procurement, Inventory, B2B Sales, or Finance is built against an undefined foundation.

| Objective | Required business outcome | Classification | Source |
|---|---|---|---|
| Consistent product identity | A Product is defined once and reused unchanged across Procurement, Inventory, and Sales. | Confirmed | PRD PLT-003; glossary Product |
| Controlled party identity | A Supplier and a Business Customer are identifiable, duplicate-checked, external business parties, never system Users. | Confirmed | PRD PROC-008; glossary Supplier/Business Customer |
| Dependable pricing | A Price List is a reusable, effective-dated, non-ambiguous source of a Product's price for a given Customer/Currency/period. | Confirmed | PRD SAL-001; glossary Price List |
| Governed taxation | Tax rates are effective-dated configuration; they are never hard-coded into transaction logic and never silently rewrite history. | Confirmed | PRD FIN-007, KSA-002 |
| Auditable commercial terms | A Payment Term is reusable configuration whose meaning on an already-posted document survives any later change to the term itself. | Confirmed | PRD FIN-004/FIN-005; glossary Payment Terms |
| Controlled currencies | Release 1 supports multiple currencies; SAR is the Saudi default, not a hard-coded ceiling. | Confirmed | PRD FIN-010; glossary Base/Transaction/Reporting Currency |
| Reproducible exchange rates | An Exchange Rate is effective-dated; a posted transaction keeps the rate it actually used forever. | Confirmed | PRD FIN-003; Founder Decision Pack MESP-54 default |
| Reuse across downstream domains | Every fact this BRD defines is read, not redefined, by Procurement (MESP-32), Inventory (MESP-33), B2B Sales (MESP-35), and Finance (MESP-34). | Confirmed | PRD bounded-context table (Catalog/Parties contexts) |
| Governed gates preserved | MESP-48 and MESP-50 remain explicit production and capacity gates; this BRD invents no volume, retention, or purge value. | Deferred Gate | MESP-48/MESP-50 |

## 4. Scope

| In-scope area | Business requirement | Classification |
|---|---|---|
| Product | Business identity, classification, tax linkage, lifecycle, duplicate control. | Confirmed |
| Product Category | Classification structure, lifecycle, product assignment. | Confirmed |
| Unit of Measure | Base/alternate units, conversions, lifecycle. | Confirmed |
| Supplier | External-party identity, lifecycle, purchasing/AP dependency. | Confirmed |
| Business Customer | B2B counterparty identity, lifecycle, sales/AR dependency. | Confirmed |
| Price List | Reusable effective-dated pricing container, lifecycle. | Confirmed |
| Tax | Effective-dated tax configuration, lifecycle. | Confirmed |
| Payment Term | Reusable due-date/settlement configuration, lifecycle. | Confirmed |
| Currency | Multi-currency master, lifecycle. | Confirmed |
| Exchange Rate | Effective-dated currency-pair conversion, lifecycle. | Confirmed |
| Master-record lifecycle | Deactivate-not-delete, effective dating, reactivation across all ten domains. | Confirmed — Founder-approved Release 1 requirement |
| Tenant isolation | Ownership, duplicate-check, search/export/import isolation for all ten domains. | Confirmed — Founder-approved Release 1 requirement |
| Ownership boundaries | Where MESP-31 ends and MESP-32/33/34/35/37/40/46/49 begin. | Confirmed |
| Audit evidence | Reconstruction of actor, action, before/after value, and reason for sensitive master-data changes. | Confirmed |
| Migration | Business ownership, mapping, duplicate review, reconciliation, and sign-off expectations for master-data migration. | Confirmed |

## 5. Out of Scope

| Exclusion | Classification | Owner |
|---|---|---|
| API/endpoint contracts, headers, tokens, protocols, request/response payloads | Out of Scope | Later Lean Implementation Specification |
| Database tables, columns, keys, indexes, ORM design, physical schema | Out of Scope | Phase 5 of `docs/94_Product_Delivery_Master_Plan.md` |
| Angular components, screens, layouts, navigation | Out of Scope | Later Lean Implementation Specification |
| Source code, migrations, automated tests, Sprint creation, Pull Requests | Out of Scope | This task is business analysis only; see §41 for the implementation gate |
| Production deployment, infrastructure topology, hosting region | Out of Scope | MESP-50 |
| Local SQL Server development-environment topology (instance, database name) | Out of Scope | Implementation/environment decision only, not a business rule (see §1) |
| Detailed Purchase Order / Purchase Requisition / supplier-confirmation workflow | Out of Scope | MESP-32 Procurement BRD |
| Detailed Sales Order / quotation / credit-check / fulfillment workflow | Out of Scope | MESP-35 B2B Sales BRD |
| Detailed stock ledger, costing, batch/lot/serial, warehouse-transfer behavior | Out of Scope | MESP-33 Inventory BRD |
| Detailed journal posting, AP/AR ledger mechanics, fiscal-period control | Out of Scope | MESP-34 Finance BRD |
| Credit Limit business rule and exposure calculation | Out of Scope | MESP-46 (Finance/B2B Sales) |
| ZATCA/e-invoicing coupling and Saudi statutory tax-treatment detail beyond the Release 1 VAT baseline | Out of Scope | MESP-49 Saudi Country Pack BRD; External Validation Required |
| Physical migration/ETL implementation, scripts, tooling | Out of Scope | Business migration *requirements* are in §38; technical migration implementation is not |
| Wafra-specific product, price, tax, or party behavior | Out of Scope | Wafra is validation-only (PRD BR-003) |
| Retail POS consumer/product/price behavior | Out of Scope | Retail POS is excluded from Release 1 |
| Invented MESP-48 volumes or MESP-50 retention/purge/residency values | Deferred Gate | MESP-48/MESP-50 |

**Business migration requirements vs. technical migration implementation:** this BRD's §38 defines what must be true of a Master Data migration from a business standpoint (ownership, mapping, duplicate handling, reconciliation, sign-off). It defines no ETL script, staging table, or import tool. That remains MESP-40's technical delivery.

## 6. Source Traceability

| Source | Relevant authority used in this BRD | Classification |
|---|---|---|
| Jira MESP-31 | Required scope: Products, Categories, UOM, Suppliers, Business Customers, Price Lists, Taxes, Payment Terms, Currencies, Exchange Rates; deactivate-not-delete instruction. | Confirmed |
| Owner instruction, 8 August 2026 | BRD-entry authorization and future implementation authorization (see §41); explicit ten-domain scope mandate for MESP-31. | Confirmed — Founder-approved Release 1 requirement |
| PRD v1.2 (`docs/MESP_PRD_v1.2.docx`) | **PLT-003** ("Master data. Authorized users can create, review, activate, deactivate, import, export, and search shared business master data with validation and duplicate detection.") is the primary Platform-foundation anchor. **PLT-002** (organization hierarchy, functional currencies). **SAL-001** (customer identity, addresses, tax attributes, contacts, payment terms, price list, credit limit, status). **PROC-002** (supplier quotations: price, currency, tax, delivery terms). **PROC-008** (suppliers are external parties, no platform accounts). **FIN-001** (chart of accounts, currency behavior). **FIN-003** (journal currencies/exchange rates). **FIN-007** (tax calculated from effective-dated rules). **FIN-010** (document/functional/reporting currency, exchange-rate source, rounding differences). **KSA-002** (Saudi VAT seeded at 15% but configurable, never hard-coded). **BR-013** (import opening and master data with preview, validation, duplicate control, rollback, reconciliation). **ADM-003** (import controls: templates, validation previews, row-level errors, duplicate rules). The PRD's own bounded-context table also assigns "Catalog" (Product, category, unit, price list, tax classification) and "Parties" (Supplier, customer, contacts, addresses, terms) as distinct contexts. | Confirmed |
| **Correction to the task brief's cited anchors** | The task brief that requested this BRD names **PLT-011 through PLT-014 and BR-004** as the PRD traceability anchors. Direct extraction of `docs/MESP_PRD_v1.2.docx` text shows these four PLT anchors are Platform Administration requirements (tenant provisioning, subscriptions/entitlements, tenant branding/structure, no-tenant-specific-code) already owned by the approved MESP-27 BRD (`docs/11_SaaS_Platform_Administration_BRD.md` lines 63–66), and BR-004 is "Manage plans, subscriptions, modules, entitlements, quotas, and tenant lifecycle" — also Platform Administration, not master data. This BRD traces instead to the verified anchors listed above (principally PLT-003) rather than repeating an inaccurate citation. See §42 Source Conflicts and Corrections. | Confirmed correction |
| `docs/00_ERP_Business_Glossary.md` | Controlled definitions for Product, Item, SKU, Barcode, Category, Unit of Measure, Base Unit, Purchase/Sales Unit, Supplier, Supplier Contact, Business Customer, Customer Contact, Payment Terms, Credit Limit, Price List, Tax Category, Base/Transaction/Reporting Currency, Exchange Rate family, Audit Event, Retail POS. | Confirmed |
| `docs/90_MVP_Founder_Decision_Pack.md` | MESP-41 (batch/lot/serial/expiry scope is configurable per product/category, disabled by default — jointly owned by MESP-31/MESP-33); MESP-51 (migrate master data plus reconciled opening balances, owned by MESP-40); MESP-54 (exchange rates are manual, effective-dated, Finance-approved by default, automated feeds deferred — owned by MESP-34); domain sequence placing MESP-31 fifth, immediately after MESP-30 and before MESP-32. | Confirmed |
| `docs/13_Multi_Tenancy_BRD.md` | Tenant ownership, private-by-default data, cross-Tenant denial, audit-evidence boundary — all apply unchanged to every master record this BRD defines. | Confirmed |
| `docs/14_Organization_and_Company_Structure_BRD.md` | Approved hierarchy (Platform → Tenant → Company/Legal Entity → Branch → Warehouse); scope never inherits upward; the Organization BRD is silent on which of these levels owns Product/Price List/Tax scope — see MD-OD-001. | Confirmed boundary |
| `docs/12_Identity_and_Access_BRD.md` | Plain-language Permission-category convention (no dotted technical naming); Separation-of-Duties pattern distinguishing SoD from approval workflow. | Confirmed |
| `docs/11_SaaS_Platform_Administration_BRD.md` | Platform-owned vs Tenant-owned record boundary; audit-evidence pattern. | Confirmed boundary |
| `docs/Decisions.md` | ADR-011 (runtime localization/Arabic search/RTL/bilingual documents required before module implementation, explicitly naming MESP-31); ADR-003 (shared-database Tenant isolation); ADR-014/ADR-015 (retention and Saudi e-invoicing gates, not owned here). | Confirmed |
| `docs/94_Product_Delivery_Master_Plan.md` | Sequential BRD delivery discipline; MESP-31 status must remain In Progress until Hossam's review, not silently Done. | Confirmed |

## 7. Actors and Responsibilities

| Actor | Responsibility | Constraint | Classification |
|---|---|---|---|
| Hossam / Product Owner and founder approver | Approves this BRD, resolves the `MD-OD-*` register, and controls delivery sequencing including the implementation gate. | Approval does not itself authorize implementation. | Confirmed |
| Tenant Administrator | Confirms initial master-data setup (Currencies, base Tax configuration, initial Categories) within the Tenant's approved scope. | Cannot cross Tenant boundaries. | Confirmed |
| Master Data Maintainer | Creates, edits, activates, and deactivates Products, Categories, Units of Measure, Suppliers, Business Customers, Price Lists, Taxes, Payment Terms, Currencies, and Exchange Rates within an authorized scope. | Requires the applicable capability (§26); cannot bypass duplicate checks or effective-dating rules. | Confirmed |
| Approver (sensitive master data) | Approves a risk-sensitive change — a tax-rate change, a manually entered exchange rate, or a published price — where §27 requires approval. | Cannot approve their own change where self-approval is prohibited (§28). | Confirmed |
| Procurement / Inventory / B2B Sales / Finance consumer | Reads Master Data facts to perform a downstream business function; never redefines them locally. | Cannot silently fork a Product, Supplier, Tax, or Currency definition inside its own domain. | Confirmed |
| Migration / Onboarding owner | Owns source mapping, duplicate review, reconciliation, and sign-off for a Master Data import. | Ambiguous mappings remain quarantined until accountable approval. | Confirmed |
| Security / Privacy / Audit reviewer | Reviews master-data change evidence and denial events. | Does not gain ordinary maintenance access merely by being a reviewer. | Confirmed |

## 8. Controlled Terminology

This BRD reuses every existing glossary definition unchanged (see §6). It does not redefine Product, Item, SKU, Barcode, Category, Unit of Measure, Base Unit, Supplier, Business Customer, Payment Terms, Price List, or the Currency/Exchange Rate family. The table below is limited to terms this BRD relies on that are either new, cross-cutting, or need an explicit business-meaning statement not already fully settled by the glossary.

| Term | Business meaning used by MESP-31 | Classification |
|---|---|---|
| Master record | Any Product, Category, Unit of Measure, Supplier, Business Customer, Price List, Tax, Payment Term, Currency, or Exchange Rate record defined once and reused across transactions. | Confirmed |
| Business Party | The umbrella business concept covering Supplier and Business Customer: an external counterparty recorded as master data inside a Tenant, never a system User. Supplier and Business Customer keep their distinct approved glossary meanings; "Business Party" only names what they share for duplicate-detection and shared-contact/address purposes. **New glossary entry proposed** (see §42). | Confirmed — Founder-approved Release 1 requirement |
| Currency (generic) | A recognized unit of monetary value maintained as Tenant master data, distinct from the Base/Transaction/Reporting *usage roles* the glossary already defines for a given document or ledger. **New glossary entry proposed** (see §42). | Confirmed |
| Tax (generic) | A configured, effective-dated rate or rule applied to a transaction, distinct from **Tax Category**, which the glossary already defines as the Saudi-Country-Pack-owned classification that determines *which* tax treatment applies. **New glossary entry proposed** (see §42). | Confirmed |
| Effective date | The date from which a rate, price, or configuration value is the one applied to a new transaction; it never changes what an already-posted transaction recorded. | Confirmed |
| Active / Inactive | The two ordinary lifecycle states of a master record. Active records may be selected for a new transaction; Inactive records may not, but remain visible for historical reference. Neither state exists as a standalone glossary entry today — this BRD is the first to define it generically (see §42). | Confirmed |
| Deactivation | The governed act of moving a master record from Active to Inactive. It never deletes the record or any transaction that already referenced it. | Confirmed — Founder-approved Release 1 requirement |
| Tenant ownership (master data) | Every master record this BRD defines belongs to exactly one Tenant, per `docs/13_Multi_Tenancy_BRD.md` MT-BR-002/MT-BR-003. | Confirmed |

## 9. Ownership Boundaries

**Classification: Confirmed.** This is the reconciliation the Executive Summary promised. The approved glossary currently assigns "Owning module" for several of MESP-31's ten mandatory domains to a *later* domain BRD. This BRD does not overrule that ownership; it adds a second, narrower layer beneath it.

| Domain | MESP-31 owns | Downstream owner (per glossary) owns |
|---|---|---|
| Product / Item / SKU / Barcode | Full business identity, classification, tax linkage, and lifecycle — glossary already names MESP-31 as the confirming BRD. | N/A — Master Data and Catalog is already the glossary-assigned owning module. |
| Product Category | Full identity, hierarchy depth (open, §12), and lifecycle. | N/A — same as above. |
| Unit of Measure (general) | Definition, alternate units, conversion identity, and lifecycle. | **Inventory (MESP-33)** owns Base Unit immutability once stock transactions exist and stock-valuation mechanics. |
| Supplier | Business identity, duplicate control, contact structure, and lifecycle as an external party. | **Procurement (MESP-32)** owns supplier-quotation comparison, confirmation workflow, and Purchase Order/AP process detail. |
| Business Customer | Business identity, duplicate control, contact structure, and lifecycle as a B2B counterparty. | **B2B Sales (MESP-35)** owns quotation/sales-order/credit-check process detail; **Finance (MESP-46)** owns Credit Limit mechanics. |
| Price List | Reusable container identity, currency, effective dating, and lifecycle. | **B2B Sales (MESP-35)** owns pricing precedence in a live Sales Order, discount authority, and approval workflow detail. |
| Tax | Reusable rate/rule identity, effective dating, and lifecycle. | **Saudi Country Pack (MESP-49)** owns statutory treatment, e-invoicing coupling, and ZATCA compliance detail. |
| Payment Term | Reusable term identity and lifecycle; assignment to Supplier/Customer. | **Finance (MESP-34)** owns due-date calculation mechanics, AP/AR aging, and collections. |
| Currency | Reusable currency identity and lifecycle. | **Finance (MESP-34)** owns base/functional-currency assignment mechanics, rounding, and GL posting behavior. |
| Exchange Rate | Reusable, effective-dated rate identity and lifecycle. | **Finance (MESP-34)** owns rate application at posting time, rounding-difference posting, and reconciliation. |

**Classification: Confirmed.** Where this table and the glossary's "Owning module" field appear to disagree, this table controls only for the master-record layer described in this BRD; the glossary's named module remains authoritative for the transactional behavior it already owns. §42 records the glossary corrections this reconciliation implies.

## 10. Master Record Lifecycle

**Classification: Confirmed — Founder-approved Release 1 requirement.** Every one of the ten domains in this BRD shares one lifecycle vocabulary. A domain-specific state is not invented unless a genuine business need is identified (none was, for Release 1).

| Status | Business meaning | Entry / exit | Classification |
|---|---|---|---|
| Active | The record may be selected for a new transaction, price, or assignment. | Default state on successful creation after validation and duplicate checks pass. | Confirmed |
| Inactive (Deactivated) | The record may not be selected for a new transaction, but remains visible for historical reference and reporting. | An authorized actor deactivates it; a record already referenced by a draft or posted transaction may still be deactivated — deactivation blocks *new* use, it does not touch existing references. | Confirmed — Founder-approved Release 1 requirement |
| Reactivated | An Inactive record is returned to Active. | An authorized actor reactivates it; for effective-dated domains (Tax, Exchange Rate, Price List) reactivation never rewrites the effective-dated history already recorded — see MD-BR-004. | Confirmed |

**Classification: Confirmed.** No master-data domain in this BRD requires a Draft-before-Active workflow for Release 1; MD-OD-008 records this as confirmable now rather than left open, subject to Hossam's review.

**Reference-preservation matrix**, per task requirement, for what happens when a master record is:

| Situation | Required business outcome | Classification |
|---|---|---|
| Unused (never referenced) | May be edited freely or deleted outright, since no transaction depends on it. | Confirmed |
| Referenced by a draft transaction | May be deactivated; the draft keeps showing the record's current values until the draft is submitted, at which point normal validation re-checks whether the now-Inactive record may still be used to complete the document (Deferred Gate: exact re-validation policy is owned by the consuming domain's BRD, e.g. MESP-32/MESP-35). | Deferred Gate |
| Referenced by a posted transaction | Deactivation is permitted and does not alter the posted transaction's recorded values in any way; deletion is never permitted. | Confirmed — Founder-approved Release 1 requirement |
| Deactivated | Cannot be selected for a new transaction; remains searchable/reportable; existing references are unaffected. | Confirmed |
| Reactivated | Becomes selectable again for new transactions; does not retroactively change anything recorded while it was Inactive. | Confirmed |

## 11. Product Requirements

**Classification: Confirmed.** A Product is Tenant-owned master data with a unique business code, a bilingual (Arabic/English) name where the Tenant's Users require it, an assigned Category, an assigned Base Unit of Measure, a tax classification, and a lifecycle status. Release 1 treats Product and Item as one concept — no separate variant layer — per the glossary's own Release 1 default (see MD-BR-015).

| Field | Requirement class | Notes |
|---|---|---|
| Product code | Required, unique within scope | Duplicate detection is mandatory before create/import; exact coding-rule format is MD-OD-003. |
| Arabic name | Conditionally required | Required wherever the Tenant serves Arabic-speaking Users; see §35. |
| English name | Required | |
| Description | Optional | |
| Category | Required, references an active Category | See §12. |
| Base Unit of Measure | Required, references an active Unit of Measure | Immutable once stock transactions exist (Inventory-owned, MD-BR-019). |
| Tax classification | Required | Configuration-led, never hard-coded (§17). |
| Sellable flag | Required | Business meaning only — whether the Product may appear on a Sales quotation/order; does not define Sales workflow. |
| Purchasable flag | Required | Business meaning only — whether the Product may appear on a Purchase Order; does not define Procurement workflow. |
| Inventory-relevant flag | Required | Whether the Product is stock-tracked; detailed costing/valuation is Inventory-owned (MESP-33). |
| Status | Required | Active / Inactive per §10. |
| Batch/lot/serial/expiry tracking | Conditionally required, per MESP-41 | Configurable per Product or Category, disabled by default, jointly owned by MESP-31 (identity flag) and MESP-33 (enforcement). |

**Business validation:** duplicate code/name detection before create or import; a Product cannot be deleted once referenced by any Purchase Order, Sales Order, Price List line, or stock ledger entry (only deactivation is permitted); deactivating a Product never deactivates or alters historical documents that already referenced it.

## 12. Product Category Requirements

**Classification: Confirmed.** A Category is Tenant-owned master data used to classify Products for grouping, reporting, default rules, and analysis (glossary Category). Hierarchy depth is genuinely undecided (MD-OD-002): the glossary explicitly defers it to this BRD and no other approved source settles it.

| Field | Requirement class |
|---|---|
| Category code/name (Arabic/English) | Required, unique within scope |
| Parent Category | Conditionally required — only if hierarchy is approved (MD-OD-002) |
| Status | Required — Active/Inactive |

Deactivating a Category blocks new Product assignment to it but does not deactivate Products already assigned. A Category referenced by any active Product cannot be deleted, only deactivated.

## 13. Unit of Measure Requirements

**Classification: Confirmed.** A Unit of Measure is Tenant-owned master data identifying the quantity unit a Product is counted, bought, sold, stored, or valued in (glossary Unit of Measure). Every Product has exactly one Base Unit; Base Unit immutability once stock transactions exist is Inventory-owned (MESP-33), not redefined here. Alternate units require a positive, non-zero conversion factor to the Base Unit; a unit without a defined conversion cannot be used for stock movement (glossary).

| Field | Requirement class |
|---|---|
| UOM code/name (Arabic/English) | Required, unique within scope |
| Base Unit indicator | Required (exactly one Base Unit per Product) |
| Conversion factor to Base Unit | Required for an alternate unit; must be positive and non-zero |
| Status | Required — Active/Inactive |

Precision/rounding-algorithm detail for a conversion is not decided here (MD-OD-006); it is not silently assumed. A Unit of Measure referenced by an active conversion or an active Product cannot be deactivated without an explicit impact review.

## 14. Supplier Requirements

**Classification: Confirmed.** A Supplier is an external business party from whom a Company procures goods or services, recorded as Tenant master data (glossary Supplier). **A Supplier is never a system User; it never authenticates, never signs in, and this BRD models no Supplier access or credential of any kind.**

| Field | Requirement class |
|---|---|
| Legal/trading name (Arabic/English) | Required |
| Supplier code | Required, unique within scope |
| Tax/VAT registration number | Conditionally required — mandatory where Saudi statutory requirements apply; exact statutory field list beyond VAT registration is MD-OD-007 |
| Contact information | Optional |
| Default Payment Term | Optional, references an active Payment Term |
| Default Currency | Optional, references an active Currency |
| Status | Required — Active/Inactive |

Duplicate detection uses legal/trading name and tax registration number where available. Deactivating a Supplier blocks new Purchase Order creation but preserves every historical Purchase Order, receipt, and invoice that already referenced it. Purchasing/AP dependency detail is Deferred to MESP-32/MESP-34.

## 15. Business Customer Requirements

**Classification: Confirmed.** A Business Customer is a B2B counterparty to whom a Company sells goods or services (glossary Business Customer). Release 1 remains B2B-only: **an anonymous retail consumer is never modeled as a Business Customer**; Retail POS is excluded entirely.

| Field | Requirement class |
|---|---|
| Legal/trading name (Arabic/English) | Required |
| Customer code | Required, unique within scope |
| Tax/VAT registration number | Conditionally required — same Saudi statutory dependency as Supplier (MD-OD-007) |
| Contact information | Optional |
| Default Payment Term | Optional, references an active Payment Term |
| Default Price List | Optional, references an active Price List |
| Default Currency | Optional, references an active Currency |
| Credit Limit reference | Out of Scope — Finance/MESP-46 owns the value and mechanics; this BRD records only that a Business Customer *may carry* such a reference |
| Status | Required — Active/Inactive |

Duplicate detection uses legal/trading name and tax registration number. Deactivating a Business Customer blocks new Sales Order creation but preserves every historical document. Sales/AR dependency detail is Deferred to MESP-35/MESP-34.

## 16. Price List Requirements

**Classification: Confirmed.** A Price List is a reusable, named set of Product prices, valid for a defined context and scoped to exactly one Currency (glossary Price List). It is not a discount-approval engine, not a Credit Limit, and not a cost record.

| Field | Requirement class |
|---|---|
| Price List name | Required, unique within scope |
| Currency | Required, references an active Currency |
| Effective start/end date | Required |
| Customer/segment assignment | Optional, where supported |
| Product price lines | Required — at least one Product/price pair |
| Status | Required — Active/Inactive |

**Classification: Open Decision (MD-OD-004).** Precedence when more than one active Price List could apply to the same Product/Customer/Currency combination is not decided by any approved source; this BRD does not invent a precedence formula. What *is* confirmed: two active Price List entries for the same Product, Customer/segment, and Currency must not have overlapping effective-date ranges without an explicit, approved precedence rule (MD-BR-030). A Price List referenced by a draft or posted Sales document is never deleted, only deactivated, so historical pricing evidence survives. Detailed precedence, discount authority, and Sales Order behavior are Deferred to MESP-35.

## 17. Tax Requirements

**Classification: Confirmed.** Tax rates are effective-dated configuration and are never hard-coded into transaction logic — directly confirmed by PRD FIN-007 ("Transactions calculate configured sales and purchase taxes from effective-dated rules") and KSA-002 ("the Saudi country pack seeds the current standard VAT rate of 15% where applicable, but tax rates, exemptions, zero-rating, effective dates, and evidence are configurable and never hard-coded into transaction logic").

| Field | Requirement class |
|---|---|
| Tax name/code | Required, unique within scope |
| Rate | Required |
| Effective start/end date | Required |
| Product/Customer applicability | Optional, where supported |
| Zero-rated / exempt / out-of-scope treatment | Conditionally required — Saudi VAT baseline requires it; detailed statutory treatment is Deferred to MESP-49 |
| Status | Required — Active/Inactive |

**Classification: External Validation Required.** Beyond the KSA-002 15% VAT baseline, statutory Saudi tax treatment, exemption rules, and ZATCA e-invoicing coupling require confirmation from Saudi legal/tax authority and are owned by MESP-49; this BRD invents none of it. A posted transaction retains its applied tax rate and effective date permanently, even if the Tax master record later changes (MD-BR-033); historical reproducibility is Confirmed — Founder-approved Release 1 requirement.

## 18. Payment Terms Requirements

**Classification: Confirmed.** A Payment Term is reusable configuration determining when an invoice becomes due, expressed as a defined interval or schedule from an agreed base date (glossary Payment Terms). It is not a Credit Limit and not a payment method.

| Field | Requirement class |
|---|---|
| Payment Term name/code | Required, unique within scope |
| Due-date structure (days/schedule) | Required — exact structure options are Deferred to MESP-34 |
| Status | Required — Active/Inactive |

A Payment Term is assignable to a Supplier or a Business Customer as a default. Deactivating a Payment Term preserves its exact meaning on every document already posted using it. AP/AR mechanics, collections, and dunning workflow are Deferred to MESP-34.

## 19. Currency Requirements

**Classification: Confirmed.** Release 1 is multi-currency; SAR is the Saudi default, never a hard-coded ceiling (PRD FIN-010; glossary Base/Transaction/Reporting Currency).

| Field | Requirement class |
|---|---|
| Currency code (ISO where applicable) | Required, unique within scope |
| Currency name (Arabic/English) | Required |
| Status | Required — Active/Inactive |

A Currency referenced by any transaction, Price List, or party default cannot be deleted, only deactivated. Base/functional-currency assignment at Company level, rounding, and GL posting mechanics are Deferred to MESP-34; the Organization BRD (PLT-002) already confirms a Tenant may configure one or more functional currencies across its Companies, which this BRD does not reinterpret.

## 20. Exchange Rate Requirements

**Classification: Confirmed.** An Exchange Rate converts an amount from a source currency to a target currency for an effective date (glossary Exchange Rate). It is not a price and not a silently-changeable constant.

| Field | Requirement class |
|---|---|
| Source currency | Required, references an active Currency |
| Target currency | Required, references an active Currency, must differ from source |
| Rate | Required, must be positive |
| Effective date | Required |
| Source/provenance | Conditionally required — manual entry is the Release 1 default (see below) |
| Status | Required — Active/Inactive |

**Classification: Confirmed — Founder-approved Release 1 requirement (default).** Per the Founder Decision Pack's MESP-54 default, Release 1 exchange rates are manually maintained and approved by Finance; automated provider feeds are deferred. Final confirmation of this default remains with MESP-34 (Deferred Gate); this BRD records it as the current Release 1 baseline, not an invention. A posted transaction retains the exact exchange rate it applied, permanently — no retroactive recalculation (MD-BR-039). A missing rate for a required currency pair/effective date blocks posting; it is never silently defaulted (MD-BR-040). Duplicate or overlapping rate entries for the same currency pair and effective date are rejected (MD-BR-041).

## 21. Data Requirements Summary

**Classification: Confirmed.** Every field table in §§11–20 already classifies each field as required, optional, or conditionally required, and states its uniqueness, effective-dating, and lifecycle expectations. No SQL column, data type, or physical schema is specified anywhere in this BRD, consistent with §5 Out of Scope. The cross-cutting expectations that apply to every field across all ten domains are:

| Cross-cutting data expectation | Applies to | Classification |
|---|---|---|
| Tenant-scoped | Every field of every domain in §§11–20 | Confirmed — Founder-approved Release 1 requirement |
| Business-uniqueness enforced at create/import | Product code, Category code, UOM code, Supplier code/tax registration, Customer code/tax registration, Price List name, Tax code, Payment Term code, Currency code, Exchange Rate (currency pair + effective date) | Confirmed |
| Effective-dated | Price List, Tax, Exchange Rate | Confirmed |
| Auditable | Every create, edit, activate, deactivate, and rate/price change across all ten domains | Confirmed |
| Bilingual (Arabic/English) where business-facing | Product, Category, UOM, Supplier, Business Customer, Currency names | Confirmed |

## 22. Business Rules Register

The following register is the MESP-31 business-rule baseline. It contains no API, schema, query, or storage prescription.

| ID | Domain | Rule | Classification |
|---|---|---|---|
| MD-BR-001 | Cross-cutting | Every master record this BRD defines is Tenant-owned and belongs to exactly one Tenant; it cannot be read or modified across a Tenant boundary. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-002 | Cross-cutting | A master record referenced by a posted transaction is deactivated, never deleted. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-003 | Cross-cutting | Deactivation never alters the recorded values of a document that already referenced the record. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-004 | Cross-cutting | Reactivation of an effective-dated record (Tax, Exchange Rate, Price List) never rewrites already-recorded effective-dated history. | Confirmed |
| MD-BR-005 | Cross-cutting | Duplicate business identity (code, name, tax registration number, currency-pair+effective-date) is checked before create or import. | Confirmed |
| MD-BR-006 | Cross-cutting | Every create, edit, activate, deactivate, and rate/price change on a master record is audit-evidenced with actor, Tenant, before/after value, and timestamp. | Confirmed |
| MD-BR-007 | Cross-cutting | An effective-dated change (Tax, Price List, Exchange Rate) publishes a future-effective value without disturbing already-posted history. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-008 | Cross-cutting | No Wafra-specific master-data schema, rule, permission, workflow, report, status, price rule, or tax behavior exists; every rule is configuration-led and reusable. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-009 | Cross-cutting | Business-facing master-data names (Product, Category, UOM, Supplier, Business Customer, Currency) support Arabic and English where the Tenant requires bilingual usability. | Confirmed |
| MD-BR-010 | Product | A Product has a unique business code within its owning scope. | Confirmed |
| MD-BR-011 | Product | A Product references exactly one active Category and exactly one active Base Unit of Measure. | Confirmed |
| MD-BR-012 | Product | A Product cannot be deleted once referenced by a Purchase Order, Sales Order, Price List line, or stock ledger entry; only deactivation is permitted. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-013 | Product | Deactivating a Product does not deactivate or alter historical documents that already referenced it. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-014 | Product | A Product's tax classification determines its default tax treatment and is configuration, not hard-coded. | Confirmed |
| MD-BR-015 | Product | Release 1 treats Product and Item as one concept; no separate variant layer exists unless separately approved. | Confirmed |
| MD-BR-016 | Category | A Category code/name is unique within scope. | Confirmed |
| MD-BR-017 | Category | Deactivating a Category blocks new Product assignment but does not deactivate Products already assigned. | Confirmed |
| MD-BR-018 | Category | Category hierarchy depth is not assumed; see MD-OD-002. | Open Decision |
| MD-BR-019 | Unit of Measure | Every Product has exactly one Base Unit, assigned at creation; Base Unit immutability once stock transactions exist is Inventory-owned (MESP-33). | Confirmed boundary |
| MD-BR-020 | Unit of Measure | An alternate-unit conversion factor to the Base Unit must be positive and non-zero. | Confirmed |
| MD-BR-021 | Unit of Measure | A Unit of Measure referenced by an active conversion or an active Product cannot be deactivated without an explicit impact review. | Confirmed |
| MD-BR-022 | Supplier | A Supplier is an external business party; it never becomes or requires a system User account. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-023 | Supplier | Supplier duplicate detection uses legal/trading name and tax registration number where available. | Confirmed |
| MD-BR-024 | Supplier | Deactivating a Supplier blocks new Purchase Order creation but preserves historical Purchase Orders and invoices referencing it. | Confirmed |
| MD-BR-025 | Business Customer | A Business Customer is a B2B counterparty; Release 1 never models an anonymous retail consumer as a Business Customer. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-026 | Business Customer | Business Customer duplicate detection uses legal/trading name and tax registration number. | Confirmed |
| MD-BR-027 | Business Customer | Deactivating a Business Customer blocks new Sales Order creation but preserves historical documents referencing it. | Confirmed |
| MD-BR-028 | Business Customer | Credit Limit value and enforcement mechanics are owned by Finance/MESP-46; a Business Customer record may carry a reference only. | Out of Scope (boundary) |
| MD-BR-029 | Price List | A Price List is scoped to exactly one Currency. | Confirmed |
| MD-BR-030 | Price List | Two active Price List entries for the same Product, Customer/segment, and Currency must not have overlapping effective-date ranges without an approved precedence rule. | Confirmed |
| MD-BR-031 | Price List | A Price List referenced by a draft or posted Sales document is never deleted; deactivation preserves historical pricing evidence. | Confirmed |
| MD-BR-032 | Tax | A Tax rate is effective-dated configuration; it is never hard-coded into transaction logic. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-033 | Tax | A posted transaction retains its applied tax rate and effective date even if the Tax master record later changes. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-034 | Tax | Tax deactivation never deletes historical tax calculations. | Confirmed |
| MD-BR-035 | Payment Term | A Payment Term is reusable configuration assignable to a Supplier or a Business Customer. | Confirmed |
| MD-BR-036 | Payment Term | Deactivating a Payment Term preserves its exact meaning on already-posted documents. | Confirmed |
| MD-BR-037 | Currency | Currency support is not limited to SAR; Release 1 provides genuine multi-currency capability. | Confirmed |
| MD-BR-038 | Currency | A Currency referenced by any transaction, Price List, or party default cannot be deleted, only deactivated. | Confirmed |
| MD-BR-039 | Exchange Rate | Exchange rates are effective-dated; a posted transaction retains its applied rate permanently with no retroactive recalculation. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-040 | Exchange Rate | A missing exchange rate for a required currency pair/effective date blocks posting; it is never silently defaulted. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-041 | Exchange Rate | Duplicate or overlapping exchange-rate entries for the same currency pair and effective date are rejected. | Confirmed |
| MD-BR-042 | Exchange Rate | Release 1 default: exchange rates are manually maintained and approved by Finance; automated provider feeds are deferred. | Confirmed (default) |
| MD-BR-043 | Migration | Master-data import/migration follows preview, validation, duplicate control, rollback, and reconciliation. | Confirmed |
| MD-BR-044 | Permissions | Only a Permission-holding, scope-authorized actor may create, edit, activate, or deactivate a master record; the Permission is Tenant-scoped and least-privilege. | Confirmed |

## 23. Main Business Processes

Each process is business-level. No screen, API, schema, or implementation behavior is prescribed.

### 23.1 Create a master record (any of the ten domains)

- **Trigger:** An authorized Master Data Maintainer submits a new record.
- **Preconditions:** Required fields per §§11–20 are present; the actor holds the applicable capability (§26).
- **Main flow:** Validate required fields; run duplicate detection (MD-BR-005); record the create event (MD-BR-006); set status Active.
- **Exception:** A duplicate match holds the record for reviewed resolution rather than silently creating a second identity.
- **Outcome:** One reviewable Active master record with full audit evidence.

### 23.2 Deactivate a master record

- **Trigger:** An authorized actor deactivates a record no longer fit for new use.
- **Preconditions:** The actor holds the applicable capability.
- **Main flow:** Record the deactivation event and reason where required (MD-BR-006); set status Inactive; block the record from new selection; leave every existing reference unaffected (MD-BR-002/003).
- **Exception:** None — deactivation is always permitted regardless of existing references, since it never deletes anything.
- **Outcome:** An Inactive record, unusable for new transactions, with unchanged historical meaning.

### 23.3 Reactivate a master record

- **Trigger:** An authorized actor reactivates an Inactive record.
- **Preconditions:** The actor holds the applicable capability; for effective-dated domains, reactivation is checked against MD-BR-004.
- **Main flow:** Record the reactivation event; set status Active; the record becomes selectable for new transactions again.
- **Exception:** Reactivation is denied if it would rewrite already-recorded effective-dated history.
- **Outcome:** An Active record with preserved historical integrity.

### 23.4 Change an effective-dated value (Tax rate, Price, Exchange Rate)

- **Trigger:** An authorized actor (or Approver, where §27 requires one) publishes a new effective-dated value.
- **Preconditions:** No overlapping effective-dated entry exists for the same scope (MD-BR-030/041); approval is obtained where required.
- **Main flow:** Validate the new value and effective date; record the change event with before/after values; the new value applies only to transactions dated on or after its effective date.
- **Exception:** An overlap or a missing required approval blocks publication.
- **Outcome:** A new effective-dated value with no impact on already-posted transactions.

### 23.5 Import/migrate master data

- **Trigger:** An authorized Migration owner runs a bounded import.
- **Preconditions:** Source mapping, duplicate rules, and rollback strategy are defined (§38).
- **Main flow:** Preview, validate, flag duplicates and rejected rows, quarantine ambiguous mappings, obtain accountable sign-off, commit.
- **Exception:** An ambiguous mapping remains quarantined until its owner resolves it.
- **Outcome:** A reconciled, auditable batch of master records with a documented sign-off.

## 24. Alternative and Exception Paths

| Situation | Required business handling | Classification |
|---|---|---|
| Duplicate code/name/tax-registration detected at create or import | Hold for reviewed resolution; do not silently create a second identity. | Confirmed |
| Deactivation requested for a record referenced by a posted transaction | Permit deactivation; leave the posted transaction's recorded values untouched. | Confirmed — Founder-approved Release 1 requirement |
| Deletion requested for a referenced record | Deny; only deactivation is available once any reference exists. | Confirmed — Founder-approved Release 1 requirement |
| New effective-dated value overlaps an existing one for the same scope | Reject the new entry until the overlap is resolved. | Confirmed |
| Required exchange rate is missing for a transaction's currency pair/date | Block posting; do not silently substitute a different rate or date. | Confirmed — Founder-approved Release 1 requirement |
| Reactivation would rewrite already-recorded effective-dated history | Deny reactivation; require a new effective-dated entry instead. | Confirmed |
| Ambiguous migration mapping | Quarantine until an accountable owner approves the reconciled outcome. | Confirmed |
| Cross-Tenant duplicate-check or search attempt | Deny; never expose another Tenant's master data. | Confirmed — Founder-approved Release 1 requirement |
| Self-approval attempted on a sensitive change | Deny where §27/§28 requires a separate approver. | Confirmed |

## 25. Validation Rules

| ID | Validation condition | Required outcome | Classification |
|---|---|---|---|
| MD-VR-001 | Duplicate code/name/tax-registration at create or import | Hold for reviewed resolution | Confirmed |
| MD-VR-002 | Deletion attempted on a referenced record | Deny; deactivation only | Confirmed — Founder-approved Release 1 requirement |
| MD-VR-003 | Product without an active Category or active Base Unit | Reject creation | Confirmed |
| MD-VR-004 | Alternate UOM conversion factor is zero or negative | Reject | Confirmed |
| MD-VR-005 | Price List entry currency differs from the Price List's own currency | Reject | Confirmed |
| MD-VR-006 | Overlapping effective-dated entries for the same scope (Price, Tax, Exchange Rate) | Reject the new entry | Confirmed |
| MD-VR-007 | Exchange Rate source and target currency are identical | Reject | Confirmed |
| MD-VR-008 | Exchange Rate value is zero or negative | Reject | Confirmed |
| MD-VR-009 | Required exchange rate missing at transaction posting time | Block posting; no silent default | Confirmed — Founder-approved Release 1 requirement |
| MD-VR-010 | Cross-Tenant reference in any master-data field (Category, UOM, Currency, Payment Term, Price List) | Reject | Confirmed — Founder-approved Release 1 requirement |
| MD-VR-011 | Tax-rate, price, or exchange-rate change lacks required approval | Block publication | Confirmed |
| MD-VR-012 | Reactivation would alter already-recorded effective-dated history | Deny | Confirmed |
| MD-VR-013 | Migration mapping is ambiguous or incomplete | Quarantine until accountable approval | Confirmed |

## 26. Permissions

**Classification: Confirmed.** Following the plain-language Permission-category convention already established by `docs/12_Identity_and_Access_BRD.md` §14.3 (no dotted technical syntax), the following business capabilities apply per master-data domain, Tenant-scoped and role-configurable:

| Capability | Applies to | Classification |
|---|---|---|
| View | All ten domains | Confirmed |
| Create | All ten domains | Confirmed |
| Edit | All ten domains | Confirmed |
| Activate | All ten domains | Confirmed |
| Deactivate | All ten domains | Confirmed |
| Approve (sensitive change) | Tax rate, Price List, Exchange Rate | Confirmed |
| Maintain Price Lists | Price List | Confirmed |
| Maintain Taxes | Tax | Confirmed |
| Maintain Exchange Rates | Exchange Rate | Confirmed |
| View audit history | All ten domains | Confirmed |
| Import/migrate | All ten domains | Confirmed |

No Wafra-specific role is created; every capability is reusable across Tenants (MD-BR-008).

## 27. Approval Controls

**Classification: Confirmed.** Blanket approval is not invented for every field change. Risk-sensitive changes require approval; routine identity edits (a Supplier's contact phone number, a Product's description) do not.

| Change | Approval expectation | Classification |
|---|---|---|
| Tax-rate creation or change | Requires one separate Approver distinct from the requester. | Confirmed |
| Manually entered Exchange Rate | Requires Finance approval per the MESP-54 Release 1 default (MD-BR-042). | Confirmed (default) |
| Published commercial Price List change | Requires one separate Approver where the Price List is customer-facing. | Confirmed |
| High-impact reference change (e.g., changing a Product's Base Unit before any stock transaction exists) | Requires review; exact policy owned jointly with MESP-33. | Deferred Gate |
| Routine identity/contact-detail edit | No approval required. | Confirmed |

Where approval policy for a specific field is not settled by an approved source, it is recorded as an Open Decision rather than invented — see MD-OD-005.

## 28. Separation of Duties

**Classification: Confirmed.** Following the SoD-versus-approval distinction already established in `docs/12_Identity_and_Access_BRD.md` §15.1:

| SoD concern | Required separation | Classification |
|---|---|---|
| Tax-rate maintainer vs. transaction poster | The person who changes a Tax rate is not required to also be the poster of transactions using it, but self-approval of one's own tax-rate change is prohibited. | Confirmed |
| Price maintainer vs. sales approver | The person who publishes a Price List entry should not be the sole approver of that same entry where §27 requires separate approval. | Confirmed |
| Supplier maintainer vs. payment execution | Supplier master-data maintenance is separable by Permission from AP payment execution (Finance-owned, MESP-34); this BRD records the separation, not the payment mechanics. | Confirmed boundary |
| Customer maintainer vs. credit authorization | Business Customer master-data maintenance is separable by Permission from credit-limit authorization (Finance-owned, MESP-46). | Confirmed boundary |

This BRD does not over-design a role model; it records the required business separation and leaves technical enforcement to the later Lean Implementation Specification.

## 29. Tenant Isolation

**Classification: Confirmed — Founder-approved Release 1 requirement.** For every one of the ten domains in this BRD: ownership is explicit and belongs to exactly one Tenant; one Tenant cannot read or modify another Tenant's master data; duplicate checks, search, export, and import never leak another Tenant's values; background activity (import jobs, scheduled duplicate scans) remains Tenant-bound; audit evidence retains Tenant context. A Platform Administrator role does not, by itself, grant access to a Tenant's master data — the same MT-BR-009 boundary from `docs/13_Multi_Tenancy_BRD.md` applies unchanged here. Nothing in this BRD invents an exception.

## 30. Company / Legal Entity / Branch Scope

**Classification: Open Decision (MD-OD-001).** The approved Organization BRD (`docs/14_Organization_and_Company_Structure_BRD.md`) confirms the hierarchy Platform → Tenant → Company/Legal Entity → Branch → Warehouse and confirms scope never inherits upward, but it is explicitly silent on which of these levels owns Product, Price List, Tax, Payment Term, Currency, or Exchange Rate master data — the Explore research for this BRD confirmed no existing approved BRD assigns this. This BRD does not assume every record belongs at Tenant level, and it does not invent a shared-across-Tenant master. The recommended default, pending Hossam's decision, is: Product, Category, Unit of Measure, Supplier, Business Customer, Tax, Payment Term, and Currency are Tenant-level (shared across a Tenant's Companies, since the glossary already frames them as "defined once and reused across all transactions"); Price List and Exchange Rate *may* need a Company-level override where a Tenant's Companies operate in different functional currencies (PLT-002 already confirms a Tenant may configure more than one functional currency). This recommendation is not implemented or assumed true anywhere else in this BRD — every domain section above states scope as "within its owning scope" rather than asserting Tenant or Company level.

## 31. Downstream Domain Dependencies

### 31.1 Inventory impact (MESP-33)

**Classification: Confirmed boundary.** Inventory reads Product, Category (for default rules), and Unit of Measure (Base Unit, conversions) to move stock. This BRD defines their identity and active-state validity only; it does not design a stock ledger, costing method, or batch/lot/serial enforcement mechanism.

### 31.2 Purchasing / AP impact (MESP-32, MESP-34)

**Classification: Confirmed boundary.** Procurement reads Supplier, Product, Unit of Measure, Currency, Tax, and Payment Term to raise a Purchase Order; Finance reads the same facts to post an AP invoice. This BRD does not design a Purchase Order, a supplier-confirmation workflow, or an AP posting rule.

### 31.3 B2B Sales / AR impact (MESP-35, MESP-34)

**Classification: Confirmed boundary.** B2B Sales reads Business Customer, Product, Price List, Unit of Measure, Currency, Tax, and Payment Term to quote and invoice; Finance reads the same facts to post an AR invoice. This BRD does not design a Sales Order, an invoice, a receipt, or an AR posting rule.

### 31.4 Accounting impact (MESP-34)

**Classification: Confirmed.** Finance depends on this BRD for stable historical references: a posted journal entry must remain reproducible using the exact Currency, Exchange Rate, Tax rate, and Payment Term it applied, even after any of those master records later changes (MD-BR-003/033/036/039). This BRD defines no journal-entry structure or posting rule.

## 32. Multi-Currency Impact

**Classification: Confirmed.** Every downstream domain that carries a monetary amount depends on this BRD's Currency and Exchange Rate sections. Transaction currency, base/functional currency, and reporting currency are distinct usage roles already defined by the glossary and unaltered here; Price List currency must match the Currency it references (MD-VR-005); Supplier/Business Customer default Currency is optional master data, not a mandate to transact in that currency only. SAR is never hard-coded as the only supported currency (MD-BR-037).

## 33. Saudi Localization Impact

| Item | Classification |
|---|---|
| VAT baseline seeded at 15%, configurable, never hard-coded (KSA-002) | Confirmed |
| VAT/CR statutory registration-number requirement on Supplier/Business Customer | Confirmed (business fact — required where Saudi statutory rules apply) |
| Exact statutory field list beyond VAT registration (e.g., Commercial Registration number format) | Open Decision (MD-OD-007) |
| ZATCA e-invoicing coupling, tax-invoice/simplified-invoice/credit-note/debit-note generation detail | Deferred Gate — MESP-49 |
| Legal completeness of Saudi tax exemption/zero-rating rules beyond the Release 1 VAT baseline | External Validation Required |
| Future country-pack extensibility (a second country's tax/currency/party rules) | Confirmed — this BRD's configuration-led design does not block it |

## 34. Bilingual and RTL Requirements

**Classification: Confirmed.** Every business-facing master-data name (Product, Category, Unit of Measure, Supplier, Business Customer, Currency) supports Arabic and English capture, consistent with ADR-011's requirement that runtime localization, Arabic search, RTL, and bilingual document generation be resolved before module implementation and its explicit naming of MESP-31. Arabic-name mandatory-vs-optional distinction, where not already settled (e.g., internal-only codes), remains as stated per field in §§11–20; this BRD does not specify Angular layout, text direction rendering, or search-indexing mechanics — those are Lean Implementation Specification concerns.

## 35. Reports and KPIs

| Report | Business justification | Classification |
|---|---|---|
| Active/Inactive master-data listing, per domain | Operational maintenance visibility. | Confirmed |
| Incomplete master-data records (missing required field) | Data-quality control before use in a transaction. | Confirmed |
| Duplicate-candidate report | Supports MD-BR-005 duplicate control. | Confirmed |
| Upcoming effective-dated changes (Tax, Price List, Exchange Rate) | Lets Finance/Sales/Tax owners review before a change takes effect. | Confirmed |
| Exchange-rate history | Supports MD-BR-039 historical reproducibility review. | Confirmed |
| Master-data audit-change report | Supports §36 Audit Evidence. | Confirmed |

No dashboard implementation or visualization design is specified.

## 36. Audit Evidence

**Classification: Confirmed.** For every sensitive master-data change — create, edit, activate, deactivate, and any effective-dated rate/price change — the following must be reconstructable: Tenant, actor, action, affected master record, previous business value where required, new business value, effective date where applicable, timestamp, approver identity where §27 required one, and reason where required. No physical audit table or storage mechanism is specified; this is the same evidence pattern already approved in `docs/13_Multi_Tenancy_BRD.md` §20 and the PRD's PLT-008.

## 37. Integration Requirements

| Boundary | Classification |
|---|---|
| Future Product/Supplier/Customer import from an external catalog or accounting system | Deferred — future integration |
| Tax-reference feed from a government/ZATCA source | Deferred — MESP-49 |
| Exchange-rate provider feed | Deferred — MESP-34, superseding the manual MESP-54 default only when separately approved |
| Accounting-system dependency for Currency/Payment Term/Tax at posting time | Release 1 required — internal, MESP-34 |

No vendor is selected; no API contract is specified.

## 38. Migration Requirements

**Classification: Confirmed.** Per the Founder Decision Pack's MESP-51 default, master data (all ten domains in this BRD) migrates together with reconciled opening balances, owned technically by MESP-40. This BRD's business expectations, distinct from technical ETL implementation (§5):

| Requirement | Classification |
|---|---|
| Named source ownership per domain | Confirmed |
| Field-level mapping from source to this BRD's data requirements (§§11–20) | Confirmed |
| Duplicate detection consistent with MD-BR-005 | Confirmed |
| Arabic/English data normalization | Confirmed |
| Currency, Tax, Price List, and UOM-conversion migration validated before opening-balance load (PRD §19.1 ordering: configuration and master data before open documents and opening balances) | Confirmed |
| Preview and validation reports before commit; immutable batch identifier; row-level result | Confirmed (BR-013, ADM-003) |
| Rejected-row handling and quarantine for ambiguous mappings | Confirmed |
| Reconciliation and accountable sign-off before go-live | Confirmed |
| Audit evidence of the migration batch itself | Confirmed |

No migration script, staging schema, or tool is specified.

## 39. Business Acceptance Scenarios

These are business acceptance scenarios, not automated tests.

**Product**

- MD-AC-001 — Given an authorized Master Data Maintainer, when they create a Product with a unique code, an active Category, and an active Base Unit, then the Product is created Active with full audit evidence.
- MD-AC-002 — Given a Product code that already exists in the Tenant, when a User attempts to create a duplicate, then the system holds it for reviewed resolution instead of creating a second identity.
- MD-AC-003 — Given a Product already referenced by a posted Sales Order, when an authorized actor deactivates it, then the Product becomes unselectable for new transactions and the posted Sales Order is unchanged.
- MD-AC-004 — Given a Product referenced by a posted transaction, when any User attempts to delete it, then the deletion is denied and only deactivation is offered.

**Product Category**

- MD-AC-005 — Given an active Category with Products assigned, when an authorized actor deactivates the Category, then new Product assignment is blocked but existing assignments are unaffected.

**Unit of Measure**

- MD-AC-006 — Given a Product with an assigned Base Unit and no stock transactions yet, when an authorized actor changes the Base Unit, then the change is permitted subject to §27 review.
- MD-AC-007 — Given an alternate Unit of Measure conversion factor of zero, when a User attempts to save it, then the system rejects the value.

**Supplier**

- MD-AC-008 — Given a new Supplier record, when it is created, then no system User account, login, or credential is created for it.
- MD-AC-009 — Given an existing Supplier with the same tax registration number, when a User attempts to create a new Supplier with that number, then the system holds it for reviewed duplicate resolution.
- MD-AC-010 — Given a Supplier referenced by historical Purchase Orders, when an authorized actor deactivates it, then new Purchase Order creation against it is blocked and historical Purchase Orders remain unchanged.

**Business Customer**

- MD-AC-011 — Given a request to record a retail walk-in consumer, when a User attempts to create it as a Business Customer, then the system rejects it as outside Release 1's B2B-only scope.
- MD-AC-012 — Given a Business Customer referenced by historical Sales Orders, when an authorized actor deactivates it, then new Sales Order creation against it is blocked and historical Sales Orders remain unchanged.

**Price List**

- MD-AC-013 — Given an active Price List in SAR, when a User attempts to add a price line in USD, then the system rejects the mismatch.
- MD-AC-014 — Given two Price List entries for the same Product, Customer segment, and Currency with overlapping effective dates, when a User attempts to publish the second, then the system rejects it pending an approved precedence rule.
- MD-AC-015 — Given a Price List referenced by a posted Sales invoice, when an authorized actor deactivates it, then the posted invoice's recorded price is unchanged.

**Tax**

- MD-AC-016 — Given a Tenant with the Saudi VAT baseline seeded at 15%, when an authorized Approver publishes a new effective-dated exemption rule, then it applies only to transactions dated on or after its effective date.
- MD-AC-017 — Given a posted invoice that applied a 15% VAT rate, when the Tax master record is later changed to a different rate, then the posted invoice continues to show 15% unchanged.
- MD-AC-018 — Given a Tax-rate change request from a Maintainer who is also the sole Approver, when they attempt to self-approve, then the system denies the approval.

**Payment Terms**

- MD-AC-019 — Given a Payment Term assigned to a Supplier, when an authorized actor deactivates the term, then documents already posted using it keep their original due-date meaning.

**Currency**

- MD-AC-020 — Given a Tenant operating only in SAR today, when an authorized actor adds a second active Currency, then the Tenant can immediately reference it on new Price Lists, Suppliers, and Business Customers.
- MD-AC-021 — Given a Currency referenced by an active Price List, when a User attempts to delete it, then the deletion is denied and only deactivation is offered.

**Exchange Rate**

- MD-AC-022 — Given no exchange rate exists for USD-to-SAR on a transaction's date, when a User attempts to post a USD transaction, then posting is blocked rather than defaulting to an assumed rate.
- MD-AC-023 — Given a posted transaction that applied an exchange rate of 3.75, when a new effective-dated rate of 3.80 is later published, then the posted transaction continues to reflect 3.75 unchanged.
- MD-AC-024 — Given a manually entered exchange rate awaiting Finance approval, when the entering User attempts to also approve it, then the system denies self-approval.
- MD-AC-025 — Given a duplicate exchange-rate entry for the same currency pair and effective date, when a User attempts to save it, then the system rejects it.

**Lifecycle / cross-cutting**

- MD-AC-026 — Given any of the ten master-data domains, when an authorized actor deactivates a record with zero existing references, then the record becomes Active-unselectable with no other side effects.
- MD-AC-027 — Given a deactivated Tax rate that would need reactivation to alter already-recorded effective-dated history, when an actor attempts reactivation, then the system denies it and requires a new effective-dated entry instead.

**Tenant isolation**

- MD-AC-028 — Given two Tenants each with a Product coded "SKU-100", when a User in Tenant A searches for duplicates, then Tenant B's identical code is never shown or matched against.
- MD-AC-029 — Given a Platform Administrator with no Tenant-business-data grant, when they attempt to view a Tenant's Supplier list, then access is denied by default.

**Bilingual / multi-currency / audit**

- MD-AC-030 — Given a Product created with only an English name where Arabic is required for the Tenant, when the User attempts to save it, then the system requires the Arabic name before completion.
- MD-AC-031 — Given a Business Customer with a default Currency of EUR trading with a Tenant whose base currency is SAR, when a Sales invoice is issued in EUR, then both the EUR transaction amount and its SAR-converted equivalent are retained.
- MD-AC-032 — Given any create/deactivate/rate-change event across the ten domains, when an auditor requests reconstruction, then actor, Tenant, before/after value, and timestamp are all retrievable.

**Migration**

- MD-AC-033 — Given a migration batch with an ambiguous Supplier-to-tax-registration mapping, when the batch runs, then the ambiguous rows are quarantined rather than committed.
- MD-AC-034 — Given a completed master-data migration dry run, when the accountable owner reviews the reconciliation report, then sign-off is required before the batch is committed to production data.

## 40. Open Decision Register

| ID | Question | Why it matters | Impacted domains | Recommended option | Owner | Blocking? | Target decision point |
|---|---|---|---|---|---|---|---|
| MD-OD-001 | Does master data live at Tenant level or Company/Legal Entity level? | Determines whether a Tenant with multiple Companies shares one Product/Price List/Tax catalog or maintains separate catalogs per Company. | All ten domains | Tenant-level by default, with a Company-level override considered for Price List and Exchange Rate where functional currencies differ. | Hossam | Yes — blocks Lean Implementation Specification data-scope decisions | Before MESP-31 approval or immediately after, before any Lean Implementation Specification work |
| MD-OD-002 | Is Product Category a flat list or a multi-level hierarchy? | Determines Category data structure and reporting rollups. | Product Category | Not recommended without further evidence; defer to Hossam. | Hossam | No — Release 1 can launch with a flat structure and add hierarchy later without data loss | Before MESP-32/33/35 Lean Implementation Specifications that rely on Category defaults |
| MD-OD-003 | What is the Product SKU/Barcode coding-rule structure — auto-generated, manual, or hybrid; what format? | Affects duplicate detection and migration mapping. | Product | Not recommended without further evidence; defer to Hossam. | Hossam | No | Before MESP-31 implementation phase begins |
| MD-OD-004 | What precedence rule resolves two applicable Price Lists for the same Product/Customer/Currency? | Directly affects which price a Sales Order uses. | Price List, B2B Sales (MESP-35) | Not recommended without further evidence; defer to Hossam and MESP-35. | Hossam / MESP-35 owner | Yes — blocks MESP-35 pricing logic | Before MESP-35 BRD drafting |
| MD-OD-005 | Which specific master-data field changes require a separate Approver beyond Tax/Exchange Rate/Price List already confirmed in §27? | Determines the full approval-control catalogue. | All ten domains | Not recommended without further evidence; defer to Hossam. | Hossam | No | Before the Lean Implementation Specification's authorization design |
| MD-OD-006 | What rounding/precision rule applies to a Unit of Measure conversion? | Affects quantity accuracy across Purchasing, Inventory, and Sales. | Unit of Measure, Inventory (MESP-33) | Not recommended without further evidence; defer to Hossam and MESP-33. | Hossam / MESP-33 owner | No | Before MESP-33 BRD drafting |
| MD-OD-007 | Beyond VAT registration number, which Saudi statutory fields (e.g., Commercial Registration number) are mandatory on Supplier/Business Customer for Release 1? | Affects required-field validation and migration mapping. | Supplier, Business Customer | Not recommended without further evidence; External Validation Required from Saudi legal/tax authority. | Hossam, with external validator | No — can launch with VAT registration only and add fields later | Before MESP-49 Saudi Country Pack BRD drafting |
| MD-OD-008 | Does any master-data domain require a Draft-before-Active state for Release 1? | Determines whether §10's two-state lifecycle is complete. | All ten domains | No — confirmed as not required for Release 1 (see §10); listed here only so Hossam can explicitly override if evidence changes. | Hossam | No | At BRD approval |
| MD-OD-009 | Can a deactivated effective-dated record (Tax, Exchange Rate, Price List) ever be reactivated, or must a new effective-dated entry always be created instead? | Affects the exact reactivation guard in MD-BR-004/MD-VR-012. | Tax, Exchange Rate, Price List | Recommend: reactivation is allowed only when it introduces no new effective-dated value into already-posted history; otherwise require a new entry. | Hossam | No | Before the Lean Implementation Specification's lifecycle design |

## 41. Owner Authorizations Recorded in This BRD

**Classification: Confirmed — Founder-approved Release 1 requirement.** Two separate Owner decisions are recorded as of 8 August 2026:

1. **BRD-entry authorization.** Hossam approved beginning MESP-31 — Master Data and Product Catalog BRD drafting, including the explicit ten-domain scope mandate (Products, Categories, Units of Measure, Suppliers, Business Customers, Price Lists, Taxes, Payment Terms, Currencies, Exchange Rates) that this BRD covers. This satisfies the distinct-authorization precedent already established for MESP-31 BRD entry (the MESP-29 precedent recorded in `.ai/CURRENT_STATE.md`'s "MESP-31 BRD entry eligibility" section, requiring an explicit owner authorization statement beyond Foundation completion alone).
2. **Future implementation authorization.** Hossam has also explicitly pre-authorized the later Master Data implementation phase. This authorization exists now, but **it is not yet executable**: implementation cannot start until (a) this BRD is completed, reviewed, and explicitly approved by Hossam as a business baseline, and (b) a dedicated implementation Jira item is identified and activated, separate from MESP-31 and separate from any other implementation item.

**No implementation was performed in the drafting of this BRD.** No Master Data source code, EF Core entity, migration, SQL table, repository, application service, API endpoint, controller, DTO, Angular screen, or database was created. The `MESP` local SQL Server database was not created.

**Note on Jira access:** this repository's tooling in this session does not include a live Jira integration. The Owner-authorization facts above are recorded here and should be mirrored into the actual Jira MESP-31 ticket by Hossam directly (or by a session with Jira access) rather than assumed to already exist there; §44 states this limitation explicitly again in the review package.

## 42. Source Conflicts and Corrections

| Conflict | Resolution | Classification |
|---|---|---|
| The task brief that requested this BRD cited PRD anchors PLT-011 through PLT-014 and BR-004 as MESP-31's traceability. Direct extraction of `docs/MESP_PRD_v1.2.docx` shows these are Platform Administration anchors (tenant provisioning, subscriptions/entitlements, tenant branding, no-tenant-specific-code) already owned by the approved MESP-27 BRD. | This BRD traces instead to the verified anchors in §6 (principally PLT-003, plus SAL-001, PROC-002, PROC-008, FIN-001/003/007/010, KSA-002, BR-013, ADM-003). | Confirmed correction |
| The glossary assigns "Owning module" for Supplier, Business Customer, Price List, Tax Category, Payment Terms, and the Currency/Exchange Rate family to Procurement, B2B Sales, Finance, or the Saudi Country Pack — not to "Master Data and Catalog" — yet MESP-31's confirmed scope (§4, per Owner instruction) requires this BRD to define all of them. | §9 Ownership Boundaries resolves this as a two-layer split: MESP-31 owns the master-record identity/lifecycle layer; the named glossary module remains authoritative for transactional behavior. No glossary "Owning module" field is silently overwritten by this BRD. | Confirmed resolution |
| The glossary has no standalone "Business Party," "Currency," or "Tax" entry, and no generic "Active/Inactive" entry, even though this BRD needs all four as controlled cross-cutting terms. | New glossary entries are proposed for these four terms (see §8); each is additive and does not change any existing entry's approved definition, owning module, or approval status. | Confirmed — glossary addition recommended |
| The glossary's Item, SKU, Barcode, and Category entries are marked "Draft for BRD Validation," explicitly deferring confirmation to this BRD (MESP-31). | This BRD confirms Release 1 treats Product and Item as one concept (MD-BR-015) and leaves SKU coding rules and Category hierarchy depth as Open Decisions (MD-OD-002/003) rather than guessing; their glossary "Approval status" should move to "Approved Product Baseline" only after Hossam resolves the remaining Open Decisions. | Confirmed — partial resolution, remainder Open Decision |

## 43. Coverage Checklist

| Domain | Coverage | Classification |
|---|---|---|
| Products | Complete | Confirmed |
| Product Categories | Complete (hierarchy depth Open Decision) | Confirmed |
| Units of Measure | Complete (rounding precision Open Decision) | Confirmed |
| Suppliers | Complete | Confirmed |
| Business Customers | Complete (Credit Limit explicitly Out of Scope, owned by MESP-46) | Confirmed |
| Price Lists | Complete (precedence rule Open Decision) | Confirmed |
| Taxes | Complete (Saudi statutory detail beyond VAT baseline is External Validation Required / Deferred to MESP-49) | Confirmed |
| Payment Terms | Complete (due-date structure detail Deferred to MESP-34) | Confirmed |
| Currencies | Complete | Confirmed |
| Exchange Rates | Complete (automated-feed decision Deferred to MESP-34) | Confirmed |
| Multi-Tenant isolation | Verified consistent with `docs/13_Multi_Tenancy_BRD.md` throughout | Confirmed |
| No Wafra hard-coding | Verified — MD-BR-008 and no domain section names Wafra | Confirmed |
| Suppliers are external parties, never Users | Verified — MD-BR-022, MD-AC-008 | Confirmed |
| B2B Business Customer boundary | Verified — MD-BR-025, MD-AC-011 | Confirmed |
| Deactivate-not-delete rule | Verified across all ten domains — MD-BR-002/003/012/013/024/027/031/034/038 | Confirmed |
| Effective-dated tax rule | Verified — MD-BR-032/033 | Confirmed |
| Multi-currency preserved | Verified — MD-BR-037, §32 | Confirmed |
| Saudi localization boundaries | Verified — §33 | Confirmed |
| Bilingual/RTL requirements | Verified — §34, ADR-011 | Confirmed |

**Register totals:** 44 business rules (MD-BR-001–044); 34 acceptance scenarios (MD-AC-001–034); 13 validation rules (MD-VR-001–013); 9 Open Decisions (MD-OD-001–009); 1 Deferred-Gate-heavy domain (Tax/Saudi statutory detail, External Validation Required); 0 rules or scenarios silently invented without a cited source.

## 44. Review and Approval Status

**Classification: Confirmed.** This document is a **Draft pending Hossam's business-owner review**. It is not Approved. It does not itself move MESP-31 to Done, and it does not authorize any implementation work. The dedicated future implementation Jira item referenced in §41 does not exist yet and must not be created or started before Hossam approves this BRD's content as the Release 1 Master Data and Product Catalog business baseline.

**No Jira write access exists in this session.** No live Jira integration tool was available to transition MESP-31's status or post an evidence comment directly. The evidence that would normally be posted to Jira is instead recorded in full in this BRD (§41 Owner Authorizations) and in `.ai/CURRENT_STATE.md`; Hossam or a session with Jira access should mirror it into the actual MESP-31 ticket.
