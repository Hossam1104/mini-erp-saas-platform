# Identity and Access Management Business Requirements Document

## 1. Document Control

| Field | Value |
|---|---|
| Document title | Identity and Access Management Business Requirements Document |
| Version | v0.1 - Draft for Founder Review |
| Status | Draft |
| Jira | MESP-28 - Produce Identity and Access BRD |
| Parent Epic | MESP-3 - EPIC 03 - Identity and Access Management |
| Accountable owner | Hossam |
| Prepared by | Luna Max, Senior Business Analyst and Product Requirements Lead |
| Date | 2 August 2026 |
| Approval status | Pending Founder Review |
| Source baseline | PRD v1.2 Final Approved Baseline; canonical repository file is MiniERPSaaSPlatform_PRD_v1.2.docx |
| Mandatory vocabulary | docs/00_ERP_Business_Glossary.md |
| Structural reference | docs/11_SaaS_Platform_Administration_BRD.md |
| Architecture reference | docs/01_Technology_Architecture_Baseline.md (constraint reference only) |
| Classification summary | 40 explicit IAM business rules: 30 Confirmed, 10 Proposed; 22 Open Decisions; 4 source conflicts; 32 business acceptance scenarios |
| Change history | v0.1 is the first controlled draft for founder review. No approval is implied. |

This document is a business-requirements baseline candidate. It does not authorize implementation, detailed design, or downstream Jira implementation work.

### Requirement classification legend

- **Confirmed** - explicitly supported by an approved PRD, glossary, approved decision, Jira requirement, or approved MESP-27 boundary. Confirmed rules use **shall**.
- **Proposed** - a business-analysis recommendation that is reasonable but requires founder confirmation. Proposed statements use **proposed**, **may**, or **should**.
- **Open Decision** - the approved sources do not settle the behavior. The question, owner, evidence, options, and gate are recorded in section 27.
- **Out of Scope** - explicitly excluded from MESP-28 or owned by another BRD. It is not a hidden requirement.

The counts above count the stable IAM-BR business-rule register and the stable IAM-OD, IAM-SC, and IAM-AC registers. Process, validation, report, transition, and coverage rows are independently classified in their own tables.

## 2. Executive Summary

Identity and Access exists to ensure that a named User can sign in and act only within the Tenant memberships, Roles, Permissions, and Company / Legal Entity, Branch, and Warehouse scopes that the business has granted. It protects the ERP from accidental disclosure, unauthorized transactions, uncontrolled privilege, conflicting duties, and unaccountable changes.

The business boundary is:

> Platform -> Tenant -> Company / Legal Entity -> Branch -> Warehouse

The Tenant is the subscription and data-isolation boundary. A Company / Legal Entity is a legal and accounting boundary inside a Tenant. A Branch is an operating location, and a Warehouse is a stock-holding location belonging to a Branch. Identity and Access does not redefine these organizational concepts or absorb the detailed Multi-Tenancy or Organization BRDs.

The BRD separates a User identity from a Person or Employee business reference, and separates a Permission from a Tenant-wide Entitlement. Suppliers and Supplier Contacts are external business parties and are not system Users in Release 1. Wafra is Tenant #1 for validation evidence only; no Wafra-specific rule is introduced.

This document remains business-focused. It defines actors, processes, rules, states, evidence, reports, dependencies, migration expectations, and business acceptance scenarios. It deliberately does not choose authentication products, protocols, credential algorithms, session values, interface contracts, storage structures, screens, or automated test implementation.

## 3. Business Purpose and Objectives

### 3.1 Purpose

Identity and Access provides a controlled account and authorization lifecycle for a multi-tenant B2B ERP. It creates an accountable link between a named User and every material action while preserving the separation between Platform operations, Tenant administration, organizational scope, commercial Entitlement, and security Permission.

### 3.2 Objectives

| Objective | Business outcome | Classification | Source |
|---|---|---|---|
| Controlled access | Only an eligible, active User with an applicable Tenant Membership, Permission, scope, document state, and context may perform an action. | Confirmed | PRD principle least privilege; BR-010 |
| Tenant isolation | A User cannot view or affect another Tenant through a changed context, identifier, support request, report, export, or background action. | Confirmed | PLT-001; BR-010; M27-RULE-001/002 |
| Least privilege | Users receive only the Roles, Permissions, and scopes necessary for their accountable work. | Confirmed | PRD principle; ADM-001; M27-REQ-045 |
| Accountability | Material access, authentication, assignment, approval, denial, suspension, recovery, and revocation events identify the actor, time, Tenant, scope, action, outcome, and safe evidence. | Confirmed | PLT-008; BR-011 |
| Separation of duties | A User is prevented from performing conflicting steps or approving a prohibited self-request where the applicable policy says so. | Confirmed | PRD table 7; D-006; MESP-55 |
| User lifecycle control | Invitations, activation, membership, Role assignment, suspension, deactivation, access revocation, and offboarding are explicit and reviewable. | Confirmed | Jira MESP-28; glossary Tenant Membership |
| Privileged-access governance | High-risk and support access is identifiable, purpose-bound, scoped, time-bound where approved, revocable, and fully evidenced. | Confirmed | ADM-001; M27-REQ-045 through M27-REQ-049 |
| Auditability | Tenant users cannot edit or delete access evidence, and material events can be retrieved for review. | Confirmed | PLT-008; BR-011; glossary Audit Event |
| Business continuity | Suspension, recovery, reactivation, and session termination protect access controls while preserving enough evidence to resume work deliberately. | Proposed | M27-REQ-054 through M27-REQ-058; exact continuity policy requires founder review |

## 4. Scope

### 4.1 In scope

1. Authentication business expectations and authentication outcomes.
2. User Account lifecycle, invitation, activation, suspension, deactivation, recovery, and closure questions.
3. Tenant Membership creation, acceptance, modification, suspension, revocation, and offboarding.
4. Predefined and custom Role assignment within an authorized scope.
5. Permission meaning and Permission categories needed by the ERP.
6. Access Scope at Tenant, Company / Legal Entity, Branch, and Warehouse levels.
7. Combined evaluation of membership, Role, Permission, Entitlement, scope, document state, and context.
8. Separation of Duties and prohibition of self-approval where the governing policy applies.
9. Session business expectations, including invalidation after critical access changes.
10. Privileged access and support access where it intersects with Identity and Access.
11. Audit evidence for material access and security events.
12. Identity and access reporting and KPIs.
13. Business dependencies with Tenant lifecycle, Organization, Audit, Files and exports, Notifications, Reporting, and Migration.
14. Migration of existing Users and access assignments, including reconciliation and exception ownership.
15. Business-level Given/When/Then acceptance scenarios.

### 4.2 Boundary statement

Identity and Access owns the User, Tenant Membership, Role, Permission, and access-governance meaning. MESP-29 owns detailed Multi-Tenancy lifecycle and isolation behavior. MESP-30 owns detailed Company / Legal Entity, Branch, and Warehouse organization behavior. Identity and Access consumes their approved business identities and relationships without redefining them.

## 5. Out of Scope

The following are explicitly out of scope for this BRD:

- Technical design, component design, and deployment design.
- API design, interface payloads, endpoint naming, or protocol selection.
- Database design, persistence structures, schemas, keys, migrations, or query design.
- Screen, page, control, navigation, visual, or UI component specifications.
- Source code, framework configuration, credential algorithms, token formats, cookie settings, or identity-provider product selection.
- Automated test implementation, test scripts, performance scripts, or test-case documents. Section 26 contains business acceptance scenarios only.
- Detailed MESP-29 Multi-Tenancy requirements beyond identity boundaries and denial rules referenced here.
- Detailed MESP-30 Organization requirements beyond the approved hierarchy and scope references.
- Procurement, Inventory, Finance, B2B Sales, or Retail POS transactions.
- Production billing, per-Tenant Entitlement overrides, or commercial Plan behavior owned by MESP-27.
- Supplier or Supplier Contact system accounts. Suppliers remain external business parties.
- Wafra-specific roles, workflows, reports, volume limits, or core behavior.
- Identity and Access implementation Stories, Enablers, Tasks, Bugs, or a Sprint.
- Final MESP-28 approval, DDD, FRS, Data Design, TDS, or implementation refinement.

## 6. Source Traceability

| Source | Source requirement or decision | BRD section(s) | Classification | Notes / unresolved gap |
|---|---|---|---|---|
| Jira MESP-28 | Required business purpose, actors, triggers, workflows, exceptions, rules, states, data, validation, permissions, approvals, SoD, impacts, reports, audit, integration, migration, GWT scenarios, open decisions, and owner approval | 2-30 | Confirmed | This BRD is the controlled first draft; approval remains open. |
| Jira MESP-28 | Authentication, tenant membership, Users, Roles, Permissions, Access Scopes, SoD, session handling, privileged access | 4, 7-16, 19, 22 | Confirmed | Detailed values not settled by the Jira task remain open. |
| PRD PLT-003 | Authorized Users can create, review, activate, deactivate, import, export, and search shared master data with validation and duplicate detection | 14, 17, 18, 21, 24 | Confirmed | Applied to access administration; domain master-data behavior remains outside this BRD. |
| PRD PLT-004 | Each business document has a unique human-readable number and immutable internal identity | 9, 23 | Confirmed | Identity and Access controls access to documents; numbering is owned by Organization. |
| PRD PLT-005 | Approval requirements can vary by document type, amount threshold, Branch, and User Role; policy versions remain linked to historical decisions | 15, 16, 19, 20, 22 | Confirmed | Thresholds, levels, delegation, and escalation remain open in MESP-42/MESP-55. |
| PRD BR-010 | Enforce Tenant, Company, Branch, Warehouse, module, Role, document-state, and contextual access | 2, 3, 9, 13, 14, 18, 22, 26 | Confirmed | Exact matrix and precedence require founder/security review. |
| PRD BR-011 | Complete audit evidence for material business, configuration, access, posting, reversal, and support actions | 3, 20-23, 26 | Confirmed | Retention duration is open under MESP-50. |
| PRD section 8 | Authorization combines membership, Role Permissions, business scope, document state, and context; server-side enforcement is mandatory; support modern password, session, MFA capability, expiry, and security events | 3, 9, 13-16, 19, 22 | Confirmed / Open Decision | Business obligations are confirmed; factors, expiry values, and technical controls are not selected here. |
| PRD ADM-001 | Tenant administrators assign predefined or custom Roles within authorized scope; high-risk Permissions are identifiable and auditable | 7, 14-16, 22 | Confirmed | Custom-role boundaries require IAM-OD-013/014. |
| PRD table 7 | Baseline roles and separation concerns for Tenant Administrator, Requester, Buyer, Warehouse Operator, Sales User, Accountant, Finance Approver, and Auditor | 7, 14, 15 | Confirmed | Final role catalogue remains an approval output. |
| PRD table 17 | Privileged access and audit activity are reportable; authorized SaaS/Admin reports reconcile to platform events and support records | 21, 22 | Confirmed | Report definitions are business-level only. |
| PRD table 18 | Roles/access matrix and privileged-access model are required BRD outputs | 7, 14-16, 21, 27, 29 | Confirmed | Approval roles include business owners and security. |
| PRD D-005/D-006/D-007/D-009 | Arabic/English and RTL; versioned approval policy; hierarchy; B2B-only Release 1 | 2, 14-16, 25 | Confirmed | No Retail POS behavior is added. |
| PRD KSA-005/KSA-006/KSA-007 | Bilingual terminology/RTL, privacy baseline, residency/transfer/support-access decisions | 20, 23, 25, 27 | Confirmed / Open Decision | Legal and production values require qualified validation. |
| docs/Decisions.md ADR-003 | Shared-database tenant isolation is an approved architecture baseline; detailed controls before tenant-scoped persistence | 9, 23, 28 | Confirmed boundary | No database design is introduced here. |
| docs/Decisions.md ADR-004 | Identity cookie, antiforgery, session, and MFA policy required before authentication/privileged-session implementation | 9, 19, 27, 30 | Confirmed dependency | Exact policy is a later architecture/production gate. |
| docs/Decisions.md ADR-005 | Policy and resource authorization baseline; detailed permissions before affected implementation | 9, 14-16, 27 | Confirmed dependency | This BRD owns business meaning, not technical enforcement. |
| docs/Decisions.md ADR-011/014 | Localization/RTL and residency, retention, legal hold, export, and purge remain controlled gates | 20, 23, 25, 27 | Confirmed dependency | No retention duration or legal conclusion is invented. |
| docs/Decisions.md MESP-55 | One named approver, controlled administrator reassignment, no self-approval; defer parallel approval and automatic escalation | 15, 16, 27 | Confirmed / Open Decision | Applied as the current approved direction; detailed workflow remains open. |
| docs/Decisions.md MESP-56 | Multiple legal entities in a Tenant without consolidation or intercompany automation | 2, 9, 14, 25 | Confirmed boundary | Organization details belong to MESP-30. |
| docs/Decisions.md MESP-50 | Residency, retention, legal hold, support access, export, and purge require production validation | 20, 22-25, 27 | Open Decision | No duration, region, or legal rule is assumed. |
| Glossary User, Person/Employee | User is a login identity; Employee/Person is a business reference; access is granted to Users | 7-9, 13, 17 | Confirmed | No HR module is implied. |
| Glossary Tenant Membership, Role, Permission, Access Scope | Explicit membership; Role is a reusable Permission bundle; Permission is atomic; scope is a data boundary | 8, 14, 18, 19 | Confirmed / Open Decision | Access Scope granularity is marked Draft for BRD Validation. |
| Glossary Separation of Duties, Approver | SoD prevents conflicting steps; approver is a User with defined authority; workflow limits remain open | 15, 16, 27 | Confirmed / Open Decision | MESP-38 and MESP-55 remain dependencies. |
| Glossary Audit Event, Supplier | Audit evidence is immutable to Tenant Users; Suppliers are not Users | 5, 7, 20, 22 | Confirmed | Supports explicit scope protection. |
| MESP-27 approved BRD | Platform/Tenant administrator boundary; named, case/time/scope support access; session invalidation; suspension/reactivation; offboarding; no support export authority; Wafra neutrality | 7, 9-13, 15, 19-24, 28 | Confirmed | MESP-27 detailed platform lifecycle remains authoritative for its own domain. |
| MESP-27 M27-REQ-045-049 | Named, one-Tenant, case/purpose/time-bound support; no shared superuser; expiry/revocation; emergency access separately governed | 7, 11, 12, 15, 19, 22, 27 | Confirmed / Open Decision | Emergency behavior is not silently enabled. |
| MESP-27 M27-RULE-001/006/012/013/017/018 | Server-established Tenant context; Entitlement/Permission distinction; consistent suspension; reactivation reevaluation; named least-privilege support; no hidden support superuser | 9, 13-16, 19, 22, 26 | Confirmed | Identity-specific interpretation is captured by IAM-BR-005 onward. |
| MESP-27 M27-AC-026-028 | Authorized support, automatic expiry, cross-Tenant support denial | 11, 12, 22, 26 | Confirmed | Used as business acceptance evidence. |
| MESP-27 M27-OQ-005 | When Tenant authorization is mandatory for normal/emergency support | 27, 28, 30 | Open Decision | Owner Hossam plus Security/Privacy. |

## 7. Business Actors and Responsibilities

The actors below are supported by the PRD, glossary, MESP-27, or the MESP-28 Jira Epic. A Company, Branch, or Warehouse-scoped User is an access-scope assignment, not automatically a separate Role or actor.

| Actor | Business responsibility | Permitted scope | Prohibited or constrained actions | Approval responsibility | Audit responsibility | Classification |
|---|---|---|---|---|---|---|
| Hossam / Product Owner and Business Sponsor | Approves the BRD, product decisions, exceptions that require founder authority, and sequencing. | Platform-level governance. | May not approve this BRD silently or treat a draft as an implementation gate. | Founder approval of this document and open decisions. | Ensures approval evidence is recorded. | Confirmed |
| Platform Administrator | Coordinates platform-level tenant and lifecycle administration and hands authorized administration to the Tenant. | Platform metadata and explicitly authorized platform operations; no Tenant business data by default. | Cannot grant self-approval, bypass Entitlements, create tenant-specific behavior, or access Tenant business data without approved support. | Platform lifecycle or support approval where policy assigns it. | Accountable for platform administration evidence. | Confirmed |
| Platform Operations Owner | Owns operational recovery, notifications, jobs, access evidence, and readiness. | Approved platform operations. | Cannot infer wider Tenant or action scope than the initiating record. | Operational exception or recovery approval where assigned. | Ensures failures and retries remain visible. | Confirmed |
| Security / Privacy Owner | Reviews privileged access, support boundaries, privacy, retention, export, and production controls. | Security and privacy evidence, not ordinary Tenant business operation. | Must not be the sole requester and approver of an irreversible action where dual control is required. | Privileged/support/security review where policy assigns it. | Reviews security evidence and exceptions. | Confirmed |
| Tenant Administrator | Manages the Tenant's Users, memberships, Roles, Permissions, organization scope assignments, and tenant-level configuration within governed options. | One authorized Tenant and its Company / Legal Entity, Branch, and Warehouse hierarchy. | Cannot change Platform Plans/Entitlements, edit audit evidence, or cross Tenant boundaries. | Assigns or approves Tenant access where the policy assigns that responsibility. | Reviews Tenant access and confirms support authorization where required. | Confirmed |
| Tenant business User | Performs an approved business function such as requesting, buying, warehouse operation, sales, accounting, finance approval, or audit. | Membership and assigned Role/Permission/Access Scope. | Cannot exceed scope, approve prohibited self-actions, or access another Tenant. | May approve only where an explicit Role/Permission and policy allow it. | Every material action is attributable to the named User. | Confirmed |
| Auditor / read-only User | Reviews authorized reports, configuration, access, evidence, and audit history. | Assigned read-only scope. | No transactional or access mutation. | No approval unless a separate approved Role grants it. | Records review outcome where applicable. | Confirmed |
| Authorized Support User | Investigates a named support case under approved Tenant, purpose, scope, and time. | One Tenant and the exact approved support scope. | No shared credential, hidden superuser, unrestricted impersonation, standing access, or export authority from support alone. | Support access and any separate export authorization as required. | All authentication, records/actions accessed, changes, downloads, expiry, revocation, and closure are evidenced. | Confirmed |
| Named Privileged-Access Approver | Performs the business approval role for high-risk or privileged access when assigned. | The decision scope in the approval request. | Cannot self-approve a prohibited request; exact conflict matrix remains open. | Approves or rejects the named request. | Approval reason, actor, decision, and time are retained. | Confirmed |
| Background Operator | Executes an already authorized business operation or recovery action. | The Tenant and scope recorded by the initiating business action. | Cannot expand Tenant or action scope. | No independent privilege beyond the approved work. | Records outcome and failure/retry evidence. | Confirmed |

No separate Platform Customer, Company Administrator, Branch Administrator, or Warehouse Administrator Role is approved by this BRD. Such Roles may be proposed only through the governed Role catalogue and open decisions, not assumed as new actors.

## 8. Business Terminology

The following definitions use the global glossary. Where a term is marked Draft for BRD Validation or Requires Business Decision in the glossary, this BRD does not silently promote it.

| Term | Business meaning used here | Boundary / status |
|---|---|---|
| User Account | The account state and authentication identity through which a User may sign in. | User is an authenticated identity; final state model is an Open Decision. |
| User | An authenticated identity that can act inside one or more Tenants according to granted Roles and Permissions. | Not an Employee, Supplier, or Supplier Contact. Confirmed glossary term. |
| Person / Employee | A business person reference used for attribution such as requester, buyer, salesperson, or approver. | Not a login and not an access grant. Detailed Employee behavior belongs to MESP-30. |
| Tenant Membership | The explicit link granting a User access to one Tenant with applicable Roles and Access Scope. | Revocable without deleting the User identity. Confirmed glossary term. |
| Role | A named, reusable bundle of Permissions assigned to Users to express a job function. | Not a job title or approval authority by itself. Confirmed glossary term. |
| Permission | An atomic User-level right to perform an action on an object type. | Not a Tenant Entitlement. Confirmed glossary term. |
| Access Scope | The data boundary within which the User's Permissions apply. | Company / Legal Entity, Branch, and Warehouse scope granularity requires MESP-28 confirmation. |
| Company Scope | Access bounded to a Company / Legal Entity inside a Tenant. | Company owns legal/accounting meaning; organization rules are MESP-30. |
| Branch Scope | Access bounded to a Branch inside a Company. | A Branch is not a Warehouse. |
| Warehouse Scope | Access bounded to a Warehouse inside a Branch. | Warehouse is the lowest approved hierarchy level for stock location. |
| Privileged Access | High-risk access that can affect security, access administration, support, configuration, or material evidence. | High-risk identification is confirmed; exact catalogue and factors are open. |
| Support Access | Explicit, named, case-bound, least-privilege, Tenant-bound, time-bounded access by an authorized support person. | No shared account or unrestricted impersonation. Confirmed in MESP-27. |
| Session | A period in which an authenticated User may continue an authorized interaction. | Creation, continued use, logout, and invalidation are business expectations; duration is open. |
| Account Suspension | A temporary restriction preventing some or all access while the identity or membership is retained. | Exact read-only and lockout behavior is open by suspension type. |
| Account Deactivation | A deliberate state in which the User Account or membership no longer permits ordinary operation. | Reactivation conditions are an Open Decision. |
| Access Revocation | Removal or disabling of a Role, Permission, scope, membership, support authorization, or session. | Must be evidenced and take effect according to the approved business state. |
| Separation of Duties | A control preventing one User from performing two conflicting steps of the same business transaction. | Specific conflict pairs and exceptions require MESP-38/MESP-55. |
| Least Privilege | The principle that access is limited to the minimum authorized Tenant, organizational scope, function, and time needed for the work. | Confirmed product principle. |
| Entitlement | A Tenant-wide commercial right derived from Plan and Subscription. | Not a User Permission and not a per-Tenant override. |

### Proposed glossary follow-up

"Access Scope", "Separation of Duties", "Privileged Access", "Session", and the account-state terms should receive a dated glossary update after Hossam approves this BRD. The global glossary is not changed in this task.

## 9. Assumptions, Dependencies, and Boundaries

| Boundary or dependency | MESP-28 treatment | Classification / owner |
|---|---|---|
| SaaS Platform Administration (MESP-27) | Supplies Platform/Tenant administration boundaries, Plan/Subscription/Entitlement distinction, support controls, suspension, reactivation, offboarding, and evidence expectations. | Confirmed dependency; MESP-27 |
| Multi-Tenancy (MESP-29) | Owns Tenant isolation, tenant lifecycle, and detailed tenant context. MESP-28 requires membership and denial but does not define the full tenancy model. | Confirmed boundary; MESP-29 |
| Organization (MESP-30) | Owns Company / Legal Entity, Branch, Warehouse identity and relationships. MESP-28 consumes valid scope references. | Confirmed boundary; MESP-30 |
| Security and Audit (MESP-38) | Owns detailed security evidence, SoD matrix, retention, and data-governance controls. MESP-28 provides access events and business rules. | Confirmed dependency; MESP-38 |
| Files and exports | Access to files/exports must be authorized in the same Tenant and scope. Support access never supplies export authority alone. | Confirmed boundary; MESP-27/MESP-39 |
| Notifications | Invitations, assignments, approvals, failures, suspension, recovery, and material exceptions require visible business evidence; delivery channel/value is open. | Confirmed need / Open Decision IAM-OD-006 |
| Background processes | Background work cannot expand Tenant or scope and must respect suspension/revocation decisions. | Confirmed dependency; MESP-27 |
| Reporting | Reports and KPIs expose only authorized Users and identify freshness/data-as-of information when asynchronous preparation is involved. | Confirmed dependency; MESP-36 |
| Migration | Existing identities and assignments require source ownership, mapping, reconciliation, and exception approval. | Confirmed dependency; MESP-40 |
| Architecture baseline | Approved technology direction is a feasibility constraint only: Modular Monolith, ASP.NET Core Identity direction, secure first-party session direction, and policy authorization. | Confirmed reference; ADR-004/005 |
| Wafra | Wafra supplies validation evidence only. The BRD remains generic for future Tenants. | Confirmed boundary; MESP-24/M27-RULE-003 |
| B2B Release 1 | Identity supports B2B ERP roles and controls. Retail POS actors and workflows remain excluded. | Confirmed boundary; PRD D-009 |

## 10. Identity and Access Business Lifecycle

The lifecycle below separates approved business obligations from state labels that still require founder confirmation. A state label marked Proposed is not a final product state.

### 10.1 User Account lifecycle

| Working stage | Business meaning | Entry / exit expectation | Classification |
|---|---|---|---|
| Invitation issued | An authorized actor has invited a proposed User to a Tenant. | Duplicate and eligibility checks precede usable activation. | Confirmed process / Proposed state label |
| Pending activation | The invited identity has not completed the approved activation path. | Activation evidence is required before ordinary operation. | Proposed; IAM-OD-007 |
| Active | The User may authenticate and act where membership, Permission, scope, Entitlement, document state, and context allow. | Account or membership changes can invalidate sessions. | Confirmed business outcome; final state naming Open |
| Suspended | Access is restricted for a recorded security, administrative, or other approved reason. | Existing evidence is retained; restoration policy is explicit. | Confirmed boundary; exact mode Open |
| Locked | Authentication is prevented after a failed-authentication control, if the approved policy uses this state. | Threshold, duration, and recovery are open. | Open Decision IAM-OD-008 |
| Deactivated | Ordinary operation is no longer permitted for the account or membership. | Identity and evidence are preserved unless another approved lifecycle applies. | Confirmed outcome / final transition Open |
| Closed | A terminal identity state, if required by the approved lifecycle. | No new operation; historical evidence remains reviewable. | Open Decision IAM-OD-019 |

### 10.2 Tenant Membership lifecycle

1. A User may hold an explicit membership in a Tenant only after eligibility and authorization checks.
2. Membership carries the Roles and Access Scope that apply within that Tenant.
3. Membership changes are effective only through an authorized business action and must be evidenced.
4. Suspension, revocation, or offboarding removes the membership's ability to authorize Tenant operations without deleting the global User identity.
5. Whether a User may hold multiple active memberships and how Tenant switching behaves is IAM-OD-001.

Classification: the explicit, revocable membership link is **Confirmed**; exact states and cardinality are **Open Decision**.

### 10.3 Role Assignment lifecycle

| Stage | Business expectation | Classification |
|---|---|---|
| Requested | A User or authorized administrator requests a Role or scope change with a stated business need where required. | Proposed; IAM-OD-015 |
| Reviewed | An authorized reviewer confirms eligibility, scope, conflict, and requested authority. | Confirmed control; approval levels Open |
| Approved | A named approver authorizes the change when the policy requires approval. | Confirmed where policy applies; MESP-55 |
| Active | The Role/Permission assignment may authorize actions in its approved scope. | Confirmed outcome |
| Modified | A change creates new effective evidence without erasing historical decisions. | Confirmed control |
| Suspended / Revoked | The assignment no longer authorizes actions. | Confirmed outcome |
| Periodically reviewed | An access review records retain, change, or revoke outcome if the review policy requires it. | Proposed; IAM-OD-012 |

### 10.4 Privileged Access lifecycle

1. A privileged request identifies the named User, Tenant, requested scope, business purpose, risk, requested actions, start condition, and end condition where applicable.
2. An authorized approver reviews the request and any SoD conflict.
3. Approved access is limited to the approved purpose, Tenant, scope, and time.
4. Activity, denied actions, changes, and closure are evidenced.
5. Access is revoked or expires when the approved condition ends.
6. Emergency access is not enabled by this draft; its separate governance is IAM-OD-011.

Classification: named, least-privilege, purpose-bound evidence is **Confirmed**; exact duration and reauthentication are **Open Decision**.

### 10.5 Session lifecycle

| Business stage | Expectation | Classification |
|---|---|---|
| Created | A successful authentication creates a session for the authenticated User and selected authorized Tenant context. | Confirmed outcome; method Open |
| Continued | The session remains usable only while the User, membership, scope, Permission, and Tenant state remain valid. | Confirmed |
| Context changed | Changing Tenant context requires a valid membership and must not expand access. | Confirmed |
| Logged out | The User can end the current session through the approved logout behavior. | Confirmed expectation |
| Forced termination | A critical account, membership, Permission, scope, Tenant, or security change invalidates affected sessions. | Confirmed; M27-REQ-057 |
| Expired / revoked | The session no longer authorizes actions after the approved policy condition. | Confirmed outcome; duration Open |
| Privileged session | Higher-risk activity is subject to the approved privileged-access policy. | Proposed / IAM-OD-004 |

## 11. Main Business Processes

Each narrative is intentionally business-level. Inputs, actors, decisions, outputs, and evidence are described without prescribing screens, interfaces, persistence, or implementation.

### IAM-PR-001 - Invite a new Tenant User

- **Classification:** Confirmed process; invitation expiry and duplicate policy remain Open Decisions.
- **Trigger:** An authorized Tenant Administrator or other approved inviter identifies a business need for access.
- **Preconditions:** Target Tenant is eligible for access administration; inviter has the required Permission; target identity and requested scope are supplied.
- **Main flow:** Validate duplicate and eligibility conditions; select the requested Tenant Membership, Role, and Access Scope; record the invitation; notify the intended User through an approved channel; retain the invitation evidence.
- **Alternative / exception:** Duplicate or ineligible identity is held for reviewed resolution; an invitation cannot silently create a second membership or grant a wider scope.
- **Output and evidence:** Pending invitation, requested assignment, inviter, reason, time, Tenant, scope, notification outcome, and audit event.

### IAM-PR-002 - Activate a User Account

- **Classification:** Confirmed process; activation factors and invitation expiry are Open Decisions.
- **Trigger:** The intended User accepts a valid invitation or an authorized administrator activates an eligible identity.
- **Preconditions:** Invitation or activation authority is valid; target Tenant and requested assignment remain active.
- **Main flow:** Confirm identity eligibility; complete the approved activation steps; create or confirm the User Account; activate the Tenant Membership and approved assignments; record evidence.
- **Alternative / exception:** Expired, duplicate, withdrawn, or invalid invitation is rejected; an account may not become operational when its Tenant is suspended or its scope is inactive.
- **Output and evidence:** Activation outcome, account/membership state, assigned Roles/scopes, actor, time, and audit event.

### IAM-PR-003 - Authenticate an Active User

- **Classification:** Confirmed process; authentication methods and factors are IAM-OD-003.
- **Trigger:** A User attempts to start an authorized session.
- **Preconditions:** Account and membership are eligible; Tenant is not in a state that prohibits the requested access; required authentication policy is satisfied.
- **Main flow:** Evaluate the approved authentication evidence; establish the User identity and eligible Tenant context; evaluate access before any protected action; record the outcome.
- **Alternative / exception:** Suspended, deactivated, unverified, or unauthorized identities are denied without exposing another Tenant's data.
- **Output and evidence:** Success or denial outcome, User identity, Tenant context, time, reason category, and audit/security evidence.

### IAM-PR-004 - Handle Failed Authentication

- **Classification:** Confirmed process; lockout threshold and response are IAM-OD-008.
- **Trigger:** Authentication evidence is missing, invalid, expired, or otherwise rejected.
- **Preconditions:** An authentication attempt can be associated with an identity or safely recorded as an unknown attempt.
- **Main flow:** Deny the attempt; provide a safe business outcome; record the failed outcome and risk signal; apply the approved protective response when its policy condition is met.
- **Alternative / exception:** Repeated failures may cause suspension or lockout only according to the approved policy; no threshold or duration is assumed here.
- **Output and evidence:** Denial, risk/lockout state if applicable, notification/escalation outcome, and audit/security evidence.

### IAM-PR-005 - Recover Account Access

- **Classification:** Proposed process pending IAM-OD-006.
- **Trigger:** An eligible User reports loss of access or an authorized support/administrator initiates recovery.
- **Preconditions:** The recovery claimant can be evaluated under an approved identity-verification policy; Tenant and membership state are known.
- **Main flow:** Verify the claimant through approved business controls; decide whether access may be restored; invalidate unsafe sessions or credentials where required; record the decision and notify responsible actors.
- **Alternative / exception:** A deactivated, suspended-for-security, ambiguous, or compromised identity is not restored automatically; escalation and evidence are required.
- **Output and evidence:** Recovery request, verification outcome, decision, affected memberships/sessions, actor, reason, and audit evidence.

### IAM-PR-006 - Assign Tenant Membership

- **Classification:** Confirmed process; multi-membership cardinality is IAM-OD-001.
- **Trigger:** A User requires access to an additional or initial Tenant.
- **Preconditions:** Target Tenant exists and is eligible; assigning actor is authorized; User, Role, scope, and business purpose are valid.
- **Main flow:** Confirm that the target Tenant and organizational scopes are related; assign the explicit membership; assign or request Roles/scopes; notify the User and responsible administrator; record evidence.
- **Alternative / exception:** Cross-Tenant assignment, inactive Tenant/scope, duplicate membership, or conflict is rejected or routed to the approved decision owner.
- **Output and evidence:** Membership decision, assignments, actor, effective condition, notification, and audit event.

### IAM-PR-007 - Assign or Change a Role

- **Classification:** Confirmed process; approval level, custom-role boundaries, and multiple-Role behavior remain open.
- **Trigger:** A Role is requested, changed, or revoked for a User.
- **Preconditions:** User has a valid membership; Role is available for the target scope; requested Permissions do not violate known controls.
- **Main flow:** Review requested business function and scope; check high-risk and SoD controls; obtain required approval; activate or change the Role; preserve previous assignment evidence.
- **Alternative / exception:** Unavailable Role, conflicting duties, insufficient authority, or self-approval is rejected or held for a named decision.
- **Output and evidence:** Role assignment outcome, effective scope, decision, approver where required, and audit evidence.

### IAM-PR-008 - Assign Company, Branch, or Warehouse Scope

- **Classification:** Confirmed process; scope precedence and cross-level behavior are IAM-OD-022.
- **Trigger:** A User's operating responsibility changes.
- **Preconditions:** Company, Branch, or Warehouse belongs to the target Tenant and is active for assignment.
- **Main flow:** Validate the hierarchy; assign the minimum scope required for the Role; check conflicts and existing sessions; record the changed scope and effective outcome.
- **Alternative / exception:** Inactive or unrelated scope is rejected; removing a scope while the User is active can require immediate session invalidation.
- **Output and evidence:** Scope assignment/revocation, before/after scope summary, actor, reason, and audit event.

### IAM-PR-009 - Request and Approve Privileged Access

- **Classification:** Confirmed process; privileged factors, duration, and emergency behavior are Open Decisions.
- **Trigger:** A User needs high-risk access beyond ordinary Role assignment.
- **Preconditions:** Business purpose, requested Tenant/scope/actions, risk, and named requester are supplied.
- **Main flow:** Review the business need, Permission, scope, SoD conflicts, and approver eligibility; approve or reject; activate only the approved scope; monitor and close the request.
- **Alternative / exception:** Self-approval, conflicting duty, missing purpose, unsupported Tenant, or expired request is rejected.
- **Output and evidence:** Request, justification, approval/rejection, activation/revocation/expiry, activity summary, and audit events.

### IAM-PR-010 - Review Existing Access

- **Classification:** Proposed process; review frequency and reviewer set are IAM-OD-012.
- **Trigger:** An approved access-review cycle or material risk event occurs.
- **Preconditions:** Current memberships, Roles, Permissions, scopes, privileged assignments, and open exceptions are available to the reviewer.
- **Main flow:** Compare access to current responsibility; retain, modify, suspend, or revoke assignments; resolve exceptions; record the review outcome.
- **Alternative / exception:** Missing owner, ambiguous mapping, or conflict remains open with an accountable owner and due decision.
- **Output and evidence:** Review result, changed assignments, exceptions, reviewer, time, and audit evidence.

### IAM-PR-011 - Suspend a User

- **Classification:** Confirmed process; read-only and lockout behavior vary by approved policy.
- **Trigger:** Security, administrative, Tenant, or other authorized reason requires access restriction.
- **Preconditions:** Authorized actor, reason, affected Tenant/scope, effective time, and restoration condition are identified.
- **Main flow:** Record the suspension; invalidate affected sessions where required; deny prohibited interactive and non-interactive access; notify responsible actors; retain evidence.
- **Alternative / exception:** A suspension may permit a policy-approved read-only mode, but this is never assumed.
- **Output and evidence:** Suspension state, reason, actor, scope, effective time, access mode, session/job outcome, notice, and audit event.

### IAM-PR-012 - Revoke Membership or Access

- **Classification:** Confirmed process; immediate versus scheduled effect is IAM-OD-019/022.
- **Trigger:** A User no longer needs a Role, Permission, scope, membership, or support authorization.
- **Preconditions:** Authorized revoker and affected assignment are identified.
- **Main flow:** Remove or disable the assignment; invalidate affected sessions; prevent future actions; preserve historical evidence and the global User identity where applicable.
- **Alternative / exception:** A request that would remove the last required administrator is held for approved replacement or controlled reassignment.
- **Output and evidence:** Revocation outcome, affected sessions, reason, actor, and immutable audit event.

### IAM-PR-013 - Offboard a User

- **Classification:** Confirmed process; reactivation and final account state are IAM-OD-019.
- **Trigger:** Employment, contract, Tenant membership, or business responsibility ends.
- **Preconditions:** Tenant and identity ownership are confirmed; open privileged/support access and sessions are identified.
- **Main flow:** Revoke memberships, Roles, Permissions, scopes, support authorizations, and active sessions; preserve required evidence; reconcile dependent assignments; notify responsible owners.
- **Alternative / exception:** Ambiguous ownership or migration conflict is held for business-owner approval rather than silently deleting access.
- **Output and evidence:** Offboarding decision, revoked assignments, session outcome, outstanding dependencies, and audit evidence.

### IAM-PR-014 - Terminate Active Sessions after a Critical Access Change

- **Classification:** Confirmed process; "critical" category is to be finalized in IAM-OD-009/010.
- **Trigger:** Account, membership, Role, Permission, scope, Tenant, or security state changes in a way that could invalidate existing authority.
- **Preconditions:** Affected User, Tenant, scope, change reason, and effective time are known.
- **Main flow:** Identify affected sessions; terminate or invalidate them according to the approved policy; require a fresh authorization outcome before further work; record evidence.
- **Alternative / exception:** Unaffected sessions remain governed by their own valid context; no session may retain a revoked authority.
- **Output and evidence:** Session termination outcome, reason, affected scope, actor, and audit event.

### IAM-PR-015 - Support Access involving a Tenant

- **Classification:** Confirmed process; support duration and Tenant-authorization cases are IAM-OD-018.
- **Trigger:** A valid support case needs investigation or controlled assistance.
- **Preconditions:** Named support User, case, business purpose, Tenant, requested scope, start/end condition, approval, and notification requirements are recorded.
- **Main flow:** Authorize the exact support boundary; allow only approved actions; record all access and changes; expire or revoke access; review and close the case.
- **Alternative / exception:** A different Tenant, purpose, scope, export, or extension requires fresh authorization; support authorization alone never grants export authority.
- **Output and evidence:** Case, authorization, access period, activity, downloads, revocation/expiry, outcome, and audit evidence.

### IAM-PR-016 - Handle an Attempted Cross-Tenant Access Action

- **Classification:** Confirmed process.
- **Trigger:** A User or support identity attempts to use a Tenant, record, report, file, export, or context outside the authorized membership.
- **Preconditions:** The attempted target and current authorized context can be evaluated safely.
- **Main flow:** Deny the action without disclosing the other Tenant; preserve the current valid context; record a security/audit event; escalate when risk policy requires.
- **Alternative / exception:** Approved platform-level metadata access remains limited to the closed Platform-owned boundary; it does not grant Tenant business-data access.
- **Output and evidence:** Denial, safe reason category, attempted scope, actor, time, and security evidence.

## 12. Alternative Paths and Exception Scenarios

| Scenario | Expected business handling | Classification / owner |
|---|---|---|
| Duplicate invitation | Do not create a second authoritative membership or invitation; show a reviewable duplicate outcome. | Confirmed; PLT-003/M27-REQ-025 |
| Expired invitation | Reject activation and require a new authorized invitation; expiry duration is open. | Proposed; IAM-OD-007 |
| User already belongs to another Tenant | Retain separate membership boundaries; do not infer access to the new Tenant. | Confirmed boundary; cardinality Open |
| User belongs to multiple Tenants | Permit only if the approved membership policy allows it; each context remains isolated. | Confirmed isolation / Open cardinality |
| Role no longer available | Do not activate a stale assignment; route to an approved replacement or exception decision. | Confirmed control; role catalogue Open |
| Scope removed while User is active | Re-evaluate authority and terminate affected sessions when the change is critical. | Confirmed; BR-010/M27-REQ-057 |
| Approver unavailable | Use only an approved reassignment/delegation path; do not infer automatic escalation. | Confirmed boundary / Open workflow; MESP-55 |
| User attempts self-approval | Reject where the policy prohibits it and retain the reason/evidence. | Confirmed where policy applies; MESP-55 |
| Conflicting duties | Hold or reject the assignment/action until the approved SoD matrix or exception process resolves it. | Confirmed control / Open matrix; MESP-38 |
| Suspended User attempts access | Deny the prohibited action and record the attempt; read-only mode is policy-dependent. | Confirmed; M27-REQ-055/056 |
| Deactivated User attempts recovery | Do not restore automatically; require the approved reactivation decision and evidence. | Proposed; IAM-OD-019 |
| Tenant is suspended | Apply Tenant lifecycle restrictions consistently to Users, sessions, jobs, exports, and integrations. | Confirmed boundary; MESP-27 |
| Company, Branch, or Warehouse is inactive | Do not grant new access to the inactive scope; re-evaluate existing assignments. | Confirmed business validation; BR-010 |
| User changes Tenant context | Require a valid membership for the target Tenant; never use the client-selected context to expand authority. | Confirmed; RULE-001 |
| Support User requests Tenant access | Require case, purpose, exact Tenant/scope, authorization, and expiry; no standing access. | Confirmed; M27-REQ-045 |
| Account is suspected to be compromised | Apply the approved security suspension/recovery path, terminate affected sessions, and preserve evidence. | Confirmed boundary / response Open |
| Active session remains after revocation | Treat the session as invalid and record a control exception for investigation; no revoked authority is retained. | Confirmed; M27-REQ-057 |
| Migration creates ambiguous User or Role mapping | Quarantine the mapping, assign an owner, reconcile, and obtain business approval before activation. | Confirmed; PRD migration baseline |

## 13. Business Rules

The following register contains 40 explicit IAM business rules. The first 30 are Confirmed from approved sources. Rules 031-040 are Proposed recommendations pending founder review.

| ID | Rule statement | Classification | Source | Business rationale | Related actors / process | Dependency |
|---|---|---|---|---|---|---|
| IAM-BR-001 | A User shall be a named login identity distinct from a Person/Employee business reference and from a Supplier or Supplier Contact. | Confirmed | Glossary User, Employee, Supplier | Prevents access being inferred from a business party or employee record. | All Users; PR-001/002 | MESP-30 Employee boundary |
| IAM-BR-002 | A Tenant Membership shall be an explicit link between a User and one Tenant, carrying the Roles and Access Scope applicable in that Tenant. | Confirmed | Glossary Tenant Membership | Makes multi-tenant access visible and revocable. | Tenant Administrator; PR-006 | MESP-29 |
| IAM-BR-003 | A User shall not perform Tenant operations without an active, authorized membership for that Tenant. | Confirmed | Glossary; BR-010 | Prevents anonymous or implicit Tenant access. | All Users; PR-003/016 | Membership state decision |
| IAM-BR-004 | The hierarchy shall remain Platform -> Tenant -> Company / Legal Entity -> Branch -> Warehouse. | Confirmed | PRD PLT-002; MESP-56 | Preserves legal, operational, and stock scope meaning. | Tenant/organization administrators; PR-008 | MESP-30 |
| IAM-BR-005 | Tenant context shall be established from trusted authenticated business context; a supplied Tenant identifier shall not expand authority. | Confirmed | PRD RULE-001; M27-RULE-001 | Prevents cross-Tenant disclosure through context substitution. | All Users/support; PR-003/016 | MESP-29/ADR-003 |
| IAM-BR-006 | Access shall be denied by default when membership, Role, Permission, scope, Entitlement, document state, or contextual authorization is missing. | Confirmed | BR-010; PRD section 8 | Makes every required control affirmative rather than implicit. | All Users; PR-003/008/016 | MESP-29/MESP-38 |
| IAM-BR-007 | Least privilege shall limit a User to the minimum authorized Tenant, organization scope, function, and time required for the work. | Confirmed | PRD principle; M27-REQ-045 | Reduces exposure and misuse. | All administrators/support; all PRs | Security review |
| IAM-BR-008 | A Role shall be a named reusable bundle of Permissions and shall not be treated as a job title or automatic approval authority. | Confirmed | Glossary Role | Separates job function from atomic authority. | Tenant Administrator; PR-007 | Role catalogue |
| IAM-BR-009 | A Permission shall represent an atomic right to perform an action on an object type and shall remain distinct from a Role and Entitlement. | Confirmed | Glossary Permission; M27-RULE-006 | Supports precise authorization and audit. | Administrators; PR-007/009 | MESP-38 |
| IAM-BR-010 | Access Scope shall bound where a Permission applies and shall be enforced rather than treated as a report filter. | Confirmed | Glossary Access Scope; BR-010 | Prevents a valid function from becoming unrestricted data access. | Scoped Users; PR-008 | IAM-OD-022 |
| IAM-BR-011 | An action shall require both applicable Tenant Entitlement and User Permission, plus valid lifecycle, scope, document-state, and context controls. | Confirmed | Glossary Entitlement; M27-RULE-006/007 | Prevents commercial and security controls from being confused. | Platform/Tenant administrators; PR-003/016 | MESP-27 |
| IAM-BR-012 | Platform Administrator and Tenant Administrator responsibilities shall remain distinct; one shall not automatically grant the other. | Confirmed | M27-REQ-008; glossary | Prevents platform control from becoming Tenant-data access. | Platform/Tenant administrators; PR-006/015 | MESP-27 |
| IAM-BR-013 | Tenant administration shall be limited to the authorized Tenant and governed organizational hierarchy. | Confirmed | Glossary Tenant Administrator; BR-010 | Preserves customer ownership and isolation. | Tenant Administrator; PR-006/008 | MESP-29/30 |
| IAM-BR-014 | High-risk Permissions shall be identifiable and their assignment and use shall be auditable. | Confirmed | ADM-001; PRD table 17 | Enables privileged-access review. | Security owner/approver; PR-009/010 | MESP-38 |
| IAM-BR-015 | A User shall not approve a prohibited self-request or self-action where the applicable policy requires separation. | Confirmed | PRD table 7; MESP-55 | Reduces fraud and control failure. | Approvers; PR-007/009 | MESP-42/MESP-38 |
| IAM-BR-016 | Buying, receiving, and payment-release responsibilities shall be separable by Permission and policy; a conflicting combination shall be blocked or governed by an approved exception. | Confirmed | PRD table 7; glossary SoD | Protects the purchase-to-pay control chain without defining Procurement behavior. | Buyer/Warehouse/Accountant/Approver; PR-007/009 | MESP-38 |
| IAM-BR-017 | Support Access shall use a named personal identity, one Tenant, one case, one purpose, explicit scope, least privilege, and an approved interval. | Confirmed | M27-REQ-045; M27-RULE-017 | Prevents standing or ambiguous support access. | Support/Tenant administrator; PR-015 | IAM-OD-018 |
| IAM-BR-018 | Shared support credentials, hidden superusers, unrestricted impersonation, and unaudited support access shall not be permitted. | Confirmed | M27-REQ-046; M27-RULE-018 | Preserves accountability and Tenant isolation. | Support/security; PR-015/016 | MESP-38 |
| IAM-BR-019 | Support Access shall expire or be revocable and shall require fresh authorization for extension, another Tenant, another purpose, or another scope. | Confirmed | M27-REQ-047 | Limits privileged exposure. | Support approver; PR-015 | IAM-OD-018 |
| IAM-BR-020 | Support authorization alone shall not grant Tenant export authority; export requires separate Permission, authorization, and explicit Tenant approval for the named artifact or scope. | Confirmed | M27-REQ-095; M27-RULE-021 | Prevents support from becoming an unbounded data-export channel. | Support/export approver; PR-015 | MESP-27/MESP-39 |
| IAM-BR-021 | A suspended User or suspended Tenant shall be denied prohibited interactive and non-interactive actions, including new sessions and affected background work. | Confirmed | M27-REQ-055; M27-RULE-012 | Prevents suspension bypass through another execution path. | Platform/Tenant administrators; PR-011/014 | MESP-27 |
| IAM-BR-022 | A deactivated membership shall not authorize Tenant operations, while the global User identity and required historical evidence remain distinct. | Confirmed | Glossary Tenant Membership; M27 offboarding | Supports revocation without destroying accountability. | Tenant Administrator; PR-012/013 | IAM-OD-019 |
| IAM-BR-023 | A critical account, membership, Role, Permission, scope, Tenant, or security change shall invalidate affected active sessions before the revoked authority is used again. | Confirmed | M27-REQ-057; PRD session security | Closes the gap between access change and existing sessions. | Administrators/security; PR-014 | ADR-004; IAM-OD-009 |
| IAM-BR-024 | Offboarding shall revoke relevant memberships, Roles, Permissions, scopes, support access, and sessions while retaining required evidence. | Confirmed | Jira MESP-28; M27 offboarding | Provides a complete business exit control. | Tenant Administrator/security; PR-013 | MESP-40/MESP-50 |
| IAM-BR-025 | Authentication success and failure outcomes shall be attributable to a User or safely recorded as an unknown attempt. | Confirmed | PRD section 8; BR-011 | Supports investigation and accountability. | All Users/security; PR-003/004 | ADR-004 |
| IAM-BR-026 | Material invitation, activation, authentication, recovery, membership, Role, Permission, scope, privileged, support, suspension, revocation, session, and migration events shall produce business evidence. | Confirmed | PLT-008; BR-011 | Enables control review and dispute resolution. | All administrators/auditors; PR-001-016 | MESP-38/MESP-50 |
| IAM-BR-027 | Tenant Users shall not edit or delete Identity and Access audit evidence. | Confirmed | PLT-008; glossary Audit Event | Protects historical trust. | Tenant Users/auditors; PR-010/015 | MESP-38 |
| IAM-BR-028 | A Company, Branch, or Warehouse that is inactive or unrelated to the target Tenant shall not receive a new access assignment. | Confirmed | BR-010; approved hierarchy | Prevents invalid organizational scope. | Tenant Administrator; PR-008 | MESP-30 |
| IAM-BR-029 | Suppliers and Supplier Contacts shall remain external business parties and shall not receive Release 1 system-user access. | Confirmed | PRD D-008; glossary Supplier | Preserves the approved manual supplier-response model. | Procurement users; PR-001/006 | Procurement BRD |
| IAM-BR-030 | Identity and Access behavior shall remain generic for Wafra and future Tenants and shall support B2B ERP only; Retail POS actors and workflows are excluded. | Confirmed | PRD D-009; MESP-24; M27-RULE-003/030 | Prevents customer-specific and out-of-release scope. | All owners; all PRs | Product change control |
| IAM-BR-031 | A general User invitation should use a verified business channel and should expire or be withdrawable under a founder-approved policy. | Proposed | M27-REQ-029; IAM-OD-007 | Reduces accidental activation and stale invitations. | Tenant Administrator; PR-001/002 | Hossam |
| IAM-BR-032 | Access assignment should support an explicit request-and-approval path when the requested authority is high-risk, cross-scope, or otherwise designated by policy. | Proposed | PRD PLT-005; MESP-55 | Creates a consistent control point without assuming every assignment needs approval. | Requester/approver; PR-007/009 | IAM-OD-016 |
| IAM-BR-033 | Access reviews should occur on a founder-approved cadence and after material role, responsibility, or security events. | Proposed | PRD table 17; MESP-38 dependency | Reduces stale access. | Tenant/security reviewers; PR-010 | IAM-OD-012 |
| IAM-BR-034 | A User should be able to hold more than one Role in one Tenant only when the combined Permissions and SoD evaluation remains unambiguous. | Proposed | Glossary Role; BR-010 | Supports real job combinations while preserving control. | Tenant Administrator; PR-007 | IAM-OD-001/014 |
| IAM-BR-035 | Tenant Administrators should be able to create governed custom Roles from approved Permissions without defining new unrestricted Permission types. | Proposed | ADM-001; Glossary Permission | Balances repeatable job functions with controlled atomic rights. | Tenant Administrator/security; PR-007 | IAM-OD-013/014 |
| IAM-BR-036 | A User should be able to request access for a business purpose, with the request routed to an eligible approver and retained as evidence. | Proposed | Jira required outputs; PRD PLT-005 | Supports accountable access demand without making self-service mandatory. | Business Users/approvers; PR-007/009 | IAM-OD-015/016 |
| IAM-BR-037 | Privileged access should require a recorded business justification and a fresh risk-aware authorization before use. | Proposed | M27-REQ-048/049; ADR-004 | Makes high-risk access deliberate; exact factor and reauthentication rules remain open. | Privileged requester/approver; PR-009 | IAM-OD-004 |
| IAM-BR-038 | Emergency or break-glass access should be available only if Hossam approves a separately governed, time-bounded, post-reviewed policy. | Proposed | M27-REQ-049; M27-OQ-005 | Provides a controlled continuity option without silently weakening isolation. | Security/Platform/Tenant approvers; PR-009/015 | IAM-OD-011 |
| IAM-BR-039 | Account suspension and deactivation should use distinguishable business reasons and restoration paths so that security, administrative, and offboarding outcomes are not conflated. | Proposed | M27-REQ-054; glossary lifecycle | Enables safer recovery and reporting. | Tenant/security administrators; PR-011/013 | IAM-OD-008/019 |
| IAM-BR-040 | Historical access evidence should preserve the decision, effective context, actor, and reason needed to reconstruct past authority, subject to the approved retention and privacy policy. | Proposed | BR-011; MESP-50 | Supports audit without inventing a retention period. | Auditors/security; PR-010/013 | IAM-OD-020; MESP-50 |

## 14. Roles, Permissions, and Access Scopes

### 14.1 Business model

1. A **Role** expresses a job function through a reusable bundle of **Permissions**.
2. A **Permission** is an atomic right to perform an action on an object type, such as read, create, modify, submit, approve, post, reverse, cancel, export, or administer where the owning domain defines that action.
3. An **Access Scope** determines where the Permission applies. Scope is enforced data authority, not a filter or convenience label.
4. A **Tenant Entitlement** determines whether a capability is commercially available to the Tenant. It never grants an individual User Permission.
5. An action is authorized only when the relevant Entitlement, User Permission, membership, scope, lifecycle state, document state, and contextual controls all allow it.
6. High-risk Permissions are identifiable and auditable. Approval authority is not implied by a Role name; it is a Permission and policy outcome.

### 14.2 Scope levels

| Scope level | Business meaning | Example access question | Classification |
|---|---|---|---|
| Platform | Platform-owned metadata and explicitly authorized platform operation. | May this Platform Administrator operate the Tenant catalogue? | Confirmed boundary |
| Tenant | All or selected business areas within one Tenant, subject to lower scopes and Permissions. | May this Tenant Administrator manage membership in Tenant A? | Confirmed |
| Company / Legal Entity | One legal/accounting entity within a Tenant. | May this accountant act for Company A but not Company B? | Confirmed hierarchy; detailed rules MESP-30 |
| Branch | One operational location within a Company. | May this User operate documents for Branch Riyadh? | Confirmed hierarchy; detailed rules Open |
| Warehouse | One stock-holding location within a Branch. | May this Warehouse Operator receive at Warehouse R1? | Confirmed hierarchy; detailed rules Open |

### 14.3 Permission categories

The following categories are a business vocabulary, not a final permission catalogue:

- Read and search authorized records.
- Create or draft a record.
- Modify an unposted or otherwise editable record.
- Submit a record for review.
- Approve or reject within an approved policy.
- Post, reverse, cancel, or otherwise apply a material business action where the owning domain allows it.
- Export or download an authorized result or artifact.
- Administer membership, Role, Permission, or scope.
- Request, approve, use, or close privileged/support access.

### 14.4 Unresolved assignment behavior

- Whether one User may hold multiple Roles is IAM-OD-001/IAM-OD-014.
- Whether a Role may vary by Company, Branch, or Warehouse is required by the PRD direction, but the precedence and inheritance model are IAM-OD-022.
- Whether Tenant Administrators can define custom Roles is supported as a capability by ADM-001; the guardrails and Permission customization boundary are IAM-OD-013/IAM-OD-014.
- How a conflicting Role combination is blocked, escalated, or exception-approved belongs to the future SoD matrix, not an invented default.

## 15. Separation of Duties

### 15.1 Purpose

SoD prevents one User from performing two conflicting steps of the same business transaction. It is distinct from approval workflow: approval identifies who may authorize; SoD identifies who must not authorize because of a conflicting action or responsibility.

### 15.2 Confirmed controls

- Self-approval is prohibited where the governing policy applies.
- Buying, receiving, and payment-release responsibilities are separable by Permission and policy.
- High-risk access and Role combinations are identifiable for review.
- An SoD decision is linked to the User, Tenant, scope, request/action, policy version, decision, reason, and evidence.
- A conflicting assignment cannot be silently treated as safe merely because the User has both Roles.

### 15.3 Open control matrix

The exact conflict pairs, whether a conflict is hard-blocked or approval-routed, exception authority, compensating control, and evidence duration require MESP-38 and Hossam approval. No final matrix is created in this draft.

### 15.4 SoD review responsibilities

| Responsibility | Primary actor | Classification |
|---|---|---|
| Identify high-risk Roles/Permissions | Security/Platform owner with affected business owner | Confirmed |
| Review a requested conflict | Named approver and security/control owner | Confirmed control; approval level Open |
| Reject prohibited self-approval | The authorization decision, with evidence | Confirmed |
| Approve a documented exception | Founder-authorized exception owner after the SoD decision | Proposed; IAM-OD-017 |
| Review active conflicts | Access reviewer | Proposed; IAM-OD-012 |
| Preserve conflict and exception evidence | Security and Audit owner | Confirmed; MESP-38/MESP-50 |

## 16. Approval Controls

Approval is a business authorization, not a technical operation. The following matrix distinguishes approved control intent from open workflow detail.

| Action or decision | Approval expectation | Classification | Source / open decision |
|---|---|---|---|
| Assign ordinary Tenant membership | May be completed by an authorized Tenant Administrator under the approved access policy. | Confirmed boundary | Glossary Tenant Administrator; exact levels IAM-OD-016 |
| Assign a high-risk Role or Permission | Requires a named, eligible approver when the policy designates it high-risk. | Confirmed control | ADM-001; IAM-OD-016 |
| Assign Platform-level Role | Requires platform authority and any additional privileged control. | Proposed pending catalogue | M27 actor boundary; IAM-OD-016 |
| Assign Tenant Administrator | Requires an authorized assignment decision and evidence; exact approver is open. | Confirmed control / Open owner | Glossary; IAM-OD-016 |
| Expand Company, Branch, or Warehouse scope | Requires validation and may require approval when it expands authority or conflicts with SoD. | Confirmed validation / Open level | BR-010; IAM-OD-022 |
| Privileged access request | Requires business purpose, named approver, approved scope, and evidence. | Confirmed | M27-REQ-045/048; IAM-OD-004 |
| Support access | Requires the case, scope, purpose, approver, and Tenant authorization where the policy requires it. | Confirmed | M27-REQ-045; IAM-OD-018 |
| Tenant export requested by support identity | Requires separate export Permission, separate authorization, and explicit Tenant authorization for the named artifact. | Confirmed | M27-REQ-095 |
| SoD exception | Requires a separately governed exception decision and compensating evidence if Hossam approves the process. | Proposed | IAM-OD-017; MESP-38 |
| Reactivation after security suspension | Requires the approved restoration decision, risk clearance, and session/access review. | Confirmed control / details Open | M27-REQ-054-058; IAM-OD-019 |
| Emergency access | Requires a separately approved break-glass policy before it is offered. | Open Decision | IAM-OD-011 |

## 17. Data Requirements

These are business information requirements. They are not a logical or physical data model.

| Business information | Required meaning | Classification | Owner / dependency |
|---|---|---|---|
| User identity | The identity used for authentication and accountability, distinct from Person/Employee. | Confirmed | Identity and Access |
| Account status | Whether the User can enter an approved lifecycle stage. | Confirmed outcome / state Open | Identity and Access; IAM-OD-008/019 |
| Tenant Membership | Tenant link, current status, effective context, Roles, and Access Scope. | Confirmed | Identity and Access / MESP-29 |
| Role assignment | Named Role, User, Tenant, scope, effective decision, approver, reason, and history. | Confirmed | Identity and Access |
| Permission assignment | Atomic action authority, source Role or direct governed assignment, scope, and history. | Confirmed model / customization Open | Identity and Access; IAM-OD-014 |
| Access Scope | Company / Legal Entity, Branch, and Warehouse boundary and relationship to Tenant. | Confirmed hierarchy / granularity Open | MESP-30 |
| Privileged request | Requester, purpose, Tenant, scope, actions, risk, approver, decision, activation, expiry/revocation, and closure. | Confirmed | Security/Audit; MESP-38 |
| Support authorization | Case, named support User, Tenant, scope, purpose, start/end, approvals, notification, activity, and closure. | Confirmed | MESP-27/MESP-38 |
| Authentication outcome | Success/failure category, User or unknown attempt, Tenant context when known, time, and response. | Confirmed | Security/Audit |
| Recovery request | Claimant, verification outcome, decision, affected access, actor, reason, and evidence. | Proposed / policy Open | Identity and Access; IAM-OD-006 |
| Suspension/deactivation reason | Reason category, scope, authority, effective time, access mode, restoration condition, and review. | Confirmed control | MESP-27; IAM-OD-019 |
| Session evidence | Creation, context, logout/termination, reason, and affected authorization. | Confirmed outcome / values Open | ADR-004; IAM-OD-009/010 |
| Access-review outcome | Reviewer, population, retain/change/revoke decision, exceptions, owner, and date. | Proposed | IAM-OD-012 |
| Migration source reference | Source identity/assignment, mapping decision, rejected/ambiguous status, approver, and reconciliation result. | Confirmed | MESP-40 |
| Historical evidence | Enough context to reconstruct prior access decisions, subject to approved retention/privacy. | Proposed | IAM-OD-020; MESP-50 |

## 18. Validation Rules

| ID | Business validation | Classification | Source / dependency |
|---|---|---|---|
| IAM-VR-001 | A User must be eligible for the target Tenant before membership is created. | Confirmed | Glossary Tenant Membership |
| IAM-VR-002 | A Tenant Membership must reference the same Tenant as its Roles and scopes. | Confirmed | BR-010; hierarchy |
| IAM-VR-003 | Company, Branch, and Warehouse scope must belong to the target Tenant and follow the approved hierarchy. | Confirmed | PLT-002; BR-010 |
| IAM-VR-004 | An inactive or unrelated organization scope cannot receive a new assignment. | Confirmed | BR-010 |
| IAM-VR-005 | A Role must be available and appropriate for the requested scope before activation. | Confirmed boundary | ADM-001; exact catalogue Open |
| IAM-VR-006 | A Permission must be evaluated together with Entitlement, membership, scope, state, and context. | Confirmed | M27-RULE-006/007 |
| IAM-VR-007 | A suspended User cannot authenticate or perform a prohibited action. | Confirmed control | M27-REQ-055 |
| IAM-VR-008 | A deactivated membership cannot authorize Tenant operations. | Confirmed | Glossary Tenant Membership |
| IAM-VR-009 | A User cannot approve a prohibited self-request. | Confirmed where policy applies | MESP-55 |
| IAM-VR-010 | A cross-Tenant membership, assignment, or action must be rejected without exposing the other Tenant. | Confirmed | BR-010; M27-AC-028 |
| IAM-VR-011 | A privileged request must include the business justification and approval evidence required by the policy. | Confirmed control | M27-REQ-048; exact fields Open |
| IAM-VR-012 | Support access must identify a case, named User, Tenant, purpose, scope, and approved interval. | Confirmed | M27-REQ-045 |
| IAM-VR-013 | Support authorization cannot satisfy export authorization by itself. | Confirmed | M27-REQ-095 |
| IAM-VR-014 | A critical access change must cause affected sessions to lose the changed authority. | Confirmed | M27-REQ-057 |
| IAM-VR-015 | A migration mapping with ambiguous identity, Role, or scope must be held from activation until owner approval. | Confirmed | PRD migration baseline |
| IAM-VR-016 | A general invitation should be rejected after the founder-approved expiry condition. | Proposed | IAM-OD-007 |
| IAM-VR-017 | A recovery request should be rejected or escalated when the claimant cannot satisfy the approved verification policy. | Proposed | IAM-OD-006 |
| IAM-VR-018 | A Role combination should be rejected or routed to SoD review when the approved conflict matrix identifies a conflict. | Proposed until matrix approved | IAM-OD-017 |

## 19. Status Transitions

The tables below distinguish a business transition guard from an unapproved final state vocabulary. Final state names, numeric thresholds, and durations remain founder decisions where noted.

### 19.1 User Account

| From | Trigger | Authorized actor | To | Required evidence | Classification |
|---|---|---|---|---|---|
| No account | Valid invitation/activation decision | Authorized Tenant Administrator | Invitation issued / Pending activation | Identity, Tenant, inviter, reason, time | Confirmed process / Proposed state |
| Pending activation | Valid activation outcome | Intended User and authorized policy | Active | Activation evidence and assignment | Confirmed outcome |
| Active | Approved security/administrative reason | Authorized administrator/security owner | Suspended | Reason, scope, time, restoration condition | Confirmed control |
| Active | Offboarding or membership end | Authorized Tenant/security owner | Deactivated | Revocation, session outcome, reason | Confirmed outcome |
| Suspended | Approved restoration | Authorized owner | Active or restricted state | Clearance and restoration evidence | Confirmed control / exact state Open |
| Deactivated | Approved reactivation | Authorized owner | Active or reactivation-pending state | Reverification and decision | Proposed; IAM-OD-019 |
| Any | Approved closure | Authorized owner | Closed | Closure reason and retained evidence | Open; IAM-OD-019 |

### 19.2 Tenant Membership

| From | Trigger | Authorized actor | To | Required evidence | Classification |
|---|---|---|---|---|---|
| None | Eligible membership decision | Tenant Administrator / approved actor | Invited or Pending | User, Tenant, Role/scope request | Confirmed process |
| Pending | Accepted and activated | Authorized policy | Active | Acceptance and activation evidence | Confirmed outcome / state name Open |
| Active | Role/scope change | Authorized administrator/approver | Active with revised authority | Before/after assignment, decision, reason | Confirmed |
| Active | Suspension | Authorized administrator/security | Suspended | Reason, scope, access mode, time | Confirmed |
| Active | Revocation/offboarding | Authorized administrator/security | Revoked / Deactivated | Reason, session outcome, evidence | Confirmed outcome / state name Open |
| Suspended | Restoration | Authorized owner | Active or revoked | Restoration decision | Proposed / IAM-OD-019 |

### 19.3 Role Assignment

| From | Trigger | Authorized actor | To | Required evidence | Classification |
|---|---|---|---|---|---|
| Not assigned | Approved request or direct authorized administration | Tenant Administrator/approver | Requested or Approved | Requested Role, scope, reason | Confirmed process / request state Proposed |
| Requested | Review passes | Named eligible approver | Active | Policy version, decision, SoD result | Confirmed control |
| Active | Change request | Authorized administrator/approver | Active revised | Before/after and effective decision | Confirmed |
| Active | Conflict or risk | Security/authorized administrator | Suspended or Revoked | Conflict/reason and decision | Confirmed control |
| Active | Access review | Access reviewer | Retained, Revised, or Revoked | Review outcome and exceptions | Proposed; IAM-OD-012 |

### 19.4 Privileged Access Request

| From | Trigger | Authorized actor | To | Required evidence | Classification |
|---|---|---|---|---|---|
| Not requested | Business need | Named requester | Requested | Purpose, Tenant, scope, actions, risk | Confirmed |
| Requested | Review | Eligible approver/security owner | Approved or Rejected | Decision, reason, SoD result | Confirmed control |
| Approved | Start condition | Authorized support/privileged User | Active | Activation, effective context | Confirmed |
| Active | End, expiry, revocation, or case closure | System policy/authorized owner | Expired or Revoked | Activity, outcome, revocation reason | Confirmed outcome / duration Open |
| Expired/Revoked | Review | Security/auditor | Closed | Closure evidence and exceptions | Confirmed control |

### 19.5 Access Review

| From | Trigger | Authorized actor | To | Required evidence | Classification |
|---|---|---|---|---|---|
| Not due | Approved cadence or risk event | Access owner | Due | Review population and trigger | Proposed; IAM-OD-012 |
| Due | Review begins | Named reviewer | In review | Reviewer, date, population | Proposed |
| In review | Retain/change/revoke decisions | Reviewer/approver | Completed or Exception | Per-assignment outcome and owner | Proposed |
| Exception | Missing owner/conflict/ambiguous mapping | Accountable owner | Resolved or Escalated | Resolution and decision | Confirmed control / workflow Open |

### 19.6 Session business state

| From | Trigger | Authorized actor/policy | To | Required evidence | Classification |
|---|---|---|---|---|---|
| None | Successful authentication | Approved authentication policy | Active | User, Tenant context, time, outcome | Confirmed |
| Active | Logout | User or authorized policy | Ended | End outcome and time | Confirmed expectation |
| Active | Critical access change | Membership/Role/security authority | Invalidated | Change reason, affected context | Confirmed |
| Active | Suspension or Tenant restriction | Authorized lifecycle policy | Invalidated or restricted | State, scope, policy result | Confirmed / read-only behavior Open |
| Active | Session policy condition | Approved policy | Expired | Expiry reason and time | Confirmed outcome / duration Open |

## 20. Document and Evidence Lifecycle

Identity and Access does not own a commercial ERP document. It owns business evidence associated with identity and access decisions.

| Evidence type | Create | Review/use | Close/supersede | Retention position | Classification |
|---|---|---|---|---|---|
| Invitation evidence | When invitation is issued, withdrawn, accepted, or rejected | Tenant Administrator and auditor review | Superseded by activation, expiry, withdrawal, or rejection | Retention duration Open under MESP-50 | Confirmed evidence / duration Open |
| Membership evidence | On create, change, suspend, revoke, or offboarding | Access review and support investigation | Superseded by a later decision; historical link retained | MESP-50 and privacy policy | Confirmed |
| Role/Permission evidence | On request, approval, activation, modification, suspension, or revocation | Access and SoD review | Superseded without erasing history | MESP-50 | Confirmed |
| Scope evidence | On Company/Branch/Warehouse assignment or removal | Organization and access review | Superseded by revised scope | MESP-50 | Confirmed |
| Privileged-access evidence | On request, approval, activation, activity, expiry/revocation, and closure | Security/auditor review | Closed with outcome and exceptions | MESP-50 | Confirmed |
| Support evidence | On case, authorization, access, actions, downloads, expiry/revocation, and closure | Tenant/security/auditor review | Closed after case outcome | MESP-50 | Confirmed |
| Suspension/deactivation evidence | At state change and restoration decision | Security/Tenant review | Superseded by restoration or final offboarding | MESP-50 | Confirmed |
| Access-review evidence | At review start, each outcome, and closure | Control owner/auditor review | Closed or escalated | MESP-50 | Proposed |
| Migration evidence | At mapping, validation, exception, reconciliation, and sign-off | Business owner and migration review | Closed at accepted cutover or retained exception | MESP-50/MESP-40 | Confirmed |

No retention period, purge duration, legal-hold rule, residency, or backup treatment is set by this draft.

## 21. Reports and KPIs

| Report / KPI | Business definition | Classification | Owner / evidence |
|---|---|---|---|
| Active Users by Tenant and scope | Count/list of active Users by Tenant, Company, Branch, and Warehouse scope visible to an authorized reviewer. | Confirmed need | Tenant Administrator; PRD table 17 |
| Suspended and deactivated Users | Users and memberships restricted or ended, with reason category and effective time. | Confirmed need | Tenant/Security owner |
| Privileged Users and assignments | Current high-risk Roles, Permissions, privileged requests, and support authorizations. | Confirmed need | Security/Audit; PRD table 17 |
| Pending access requests | Requests awaiting review, approval, activation, expiry, or closure. | Proposed | Access owner; IAM-OD-016 |
| Access-review completion | Population due, completed, overdue, exception, retained, changed, and revoked. | Proposed | Security/Tenant owner; IAM-OD-012 |
| Authentication outcomes | Success, failure, lockout/suspension outcomes, and trend by authorized Tenant scope. | Confirmed need | Security/Audit; BR-011 |
| Multiple-Tenant memberships | Users with more than one membership, with each Tenant boundary visible only to authorized reviewers. | Proposed / cardinality Open | Identity and Access; IAM-OD-001 |
| SoD conflicts and exceptions | Active conflict, exception, owner, compensating decision, and review status. | Confirmed need / matrix Open | Security/Audit; MESP-38 |
| Support-access activity | Case, Tenant, User, scope, duration, actions, downloads, expiry/revocation, and closure. | Confirmed need | MESP-27/MESP-38 |
| Orphaned access assignments | Roles, Permissions, or scopes without a valid User, membership, or organizational owner. | Proposed | Migration/access review; IAM-OD-012 |
| Migration exceptions | Ambiguous, rejected, unresolved, or manually approved identity/Role/scope mappings. | Confirmed need | MESP-40 |
| Access denial trend | Authorized analysis of denial categories such as cross-Tenant, inactive scope, suspended User, and missing Permission. | Proposed | Security/Audit |

Reports must be Tenant- and scope-authorized, identify data-as-of/freshness when asynchronous preparation is involved, and never expose another Tenant. No dashboard or screen layout is defined.

## 22. Audit Evidence

Material business events requiring evidence include:

| Event | Minimum business evidence | Classification |
|---|---|---|
| Invitation issued, withdrawn, accepted, rejected, or expired | User/identity reference, Tenant, scope, inviter, reason, time, outcome, notification outcome | Confirmed / expiry Open |
| Activation | User, Tenant Membership, assignments, actor, time, activation outcome | Confirmed |
| Authentication success | User, Tenant context, time, outcome category, source context | Confirmed |
| Authentication failure | User or unknown attempt, time, outcome category, protective response | Confirmed |
| Recovery request and decision | Claimant, verification outcome, decision, actor, affected access/session, reason | Proposed / policy Open |
| Membership create/change/suspend/revoke | User, Tenant, before/after assignment, actor, reason, effective time | Confirmed |
| Role or Permission assignment/change | User, Role/Permission, scope, approver, policy version, reason, effective time | Confirmed |
| Scope assignment/removal | Tenant, Company/Branch/Warehouse scope, before/after, actor, reason | Confirmed |
| Privileged-access request/approval/use/closure | Case/purpose, requester, approver, scope, actions, activation, end, activity, outcome | Confirmed |
| Support access | Case, named support User, Tenant, scope, purpose, authorization, actions, downloads, expiry/revocation, closure | Confirmed |
| Suspension/reactivation/deactivation | Reason, authority, state/effective time, session/job outcome, restoration criteria, notice | Confirmed |
| Session invalidation | Affected User/Tenant/scope, critical change, time, outcome | Confirmed |
| Cross-Tenant denial | Actor, attempted Tenant/context, safe denial category, time, escalation outcome | Confirmed |
| Access review | Reviewer, population, decisions, exceptions, owner, completion time | Proposed |
| Migration mapping | Source reference, mapping, validation, exception, reconciliation, approver | Confirmed |

Audit evidence shall be immutable to Tenant Users, correlated to the business action where applicable, and safe: secrets and unnecessarily sensitive personal data are not recorded. Retention, legal hold, residency, and purge remain MESP-50 decisions.

## 23. Integration Requirements

These are business dependencies and ownership expectations, not interface specifications.

| Dependency | Business interaction | Required outcome | Classification |
|---|---|---|---|
| Tenant lifecycle | Identity access follows Tenant provisioning, activation, suspension, reactivation, termination, and retention states. | No unauthorized User/session operation in an ineligible Tenant. | Confirmed; MESP-27/MESP-29 |
| Organization lifecycle | Membership Roles/scopes reference valid Company, Branch, and Warehouse identities. | Scope cannot cross Tenant or hierarchy boundaries. | Confirmed; MESP-30 |
| Security and Audit | Identity events produce immutable, retrievable evidence. | Access decisions and denials can be reconstructed. | Confirmed; MESP-38 |
| Notifications | Invitations, approvals, failures, suspension, recovery, and material exceptions have visible outcome evidence. | A delivery failure does not silently change the authorization decision. | Confirmed need / channel Open |
| Files and exports | Access to attachments and exported artifacts is evaluated under User/Tenant/scope authority. | Support access cannot substitute for export authorization. | Confirmed; MESP-27/MESP-39 |
| Reporting | Authorized access reports expose current state and freshness. | Reports do not bypass authorization. | Confirmed; MESP-36 |
| Background processes | Jobs act only for the recorded Tenant and approved scope and stop when access/lifecycle policy prohibits them. | No asynchronous access bypass. | Confirmed; MESP-27 |
| Migration and onboarding | Existing identities and assignments are mapped, reconciled, and approved before activation. | Ambiguous mappings remain blocked and visible. | Confirmed; MESP-40 |

## 24. Migration Requirements

Migration is a business onboarding and reconciliation concern. This BRD does not define migration scripts or storage structures.

| Migration area | Business requirement | Classification | Owner / gate |
|---|---|---|---|
| Existing Users | Identify source owner, identity purpose, active/inactive state, Tenant relationship, and accountable business owner. | Confirmed | MESP-40 |
| Duplicate identities | Detect likely duplicates before activation and assign a reviewed merge/retain/reject decision. | Confirmed | MESP-40; IAM-OD-002 |
| Existing Roles | Map source Roles to approved Release 1 Roles and record unmapped/retired assignments. | Confirmed | MESP-40; IAM-OD-013 |
| Permission assignments | Reconcile atomic rights to approved Permissions without silently granting broader authority. | Confirmed | MESP-40; IAM-OD-014 |
| Organizational access | Map Company, Branch, and Warehouse scope within the approved Tenant hierarchy. | Confirmed | MESP-30/MESP-40 |
| Disabled/inactive Users | Keep disabled source identities from becoming active without an approved reactivation decision. | Confirmed | IAM-OD-019 |
| Missing ownership | Hold a User or assignment without a clear business owner for founder/business-owner decision. | Confirmed | MESP-40 |
| Ambiguous scope | Quarantine an assignment that could map to more than one Tenant or organizational scope. | Confirmed | IAM-OD-022 |
| Privileged Users | Identify high-risk and support assignments separately and obtain approval before activation. | Confirmed | MESP-38 |
| Historical evidence | Record what source evidence exists and what cannot be migrated; do not claim unavailable history. | Proposed | IAM-OD-020/MESP-50 |
| Reconciliation | Reconcile source counts, active assignments, Roles, scopes, exceptions, and owner decisions before cutover. | Confirmed | MESP-40 |
| Founder/business-owner sign-off | Hossam and named business owners approve unresolved mappings before access is enabled. | Confirmed | PRD migration baseline |

## 25. Business Impact Assessment

### 25.1 Inventory impact

Identity and Access does not directly create, increase, decrease, value, or own Inventory quantities. It indirectly controls which authorized Users may create, review, approve, post, reverse, count, transfer, or adjust inventory records within a valid Company, Branch, or Warehouse scope. Inventory movement meaning remains owned by MESP-33 and the approved glossary.

Classification: **Confirmed boundary**. No Inventory transaction requirement is introduced.

### 25.2 Accounting impact

Identity and Access does not directly post journals, payables, receivables, cash, tax, or general-ledger entries. It indirectly controls which authorized Users may prepare, approve, post, reverse, reconcile, or release accounting actions and supports SoD evidence around buying, receiving, and payment responsibilities. Finance meaning remains owned by MESP-34.

Classification: **Confirmed boundary**. No Finance transaction requirement is introduced.

### 25.3 Multi-currency impact

Identity and Access does not calculate currencies, select exchange rates, or alter transaction/base/reporting currency facts. It only controls access to Users and Roles that may perform approved currency-related business actions in the owning Finance context.

Classification: **Confirmed boundary**. Exchange-rate behavior remains MESP-54/MESP-34.

### 25.4 Saudi localization impact

- English and Arabic terminology and RTL are foundational Release 1 product capabilities and must cover Identity and Access business messages and evidence labels.
- Saudi launch context is generic for all eligible Saudi Tenants; Wafra does not create a special identity rule.
- Residency, cross-border support, privacy, retention, and support-access obligations require qualified review under KSA-006/KSA-007 and MESP-50.
- No Saudi legal, tax, or regulatory conclusion is invented by this BRD.

Classification: **Confirmed product direction / Open production decisions**.

## 26. Given/When/Then Acceptance Scenarios

These are business acceptance scenarios, not automated test instructions or a separate test-case document.

1. **IAM-AC-001 - Valid invitation:** Classification: Confirmed. Given an eligible User, Tenant, authorized inviter, and valid requested scope, when the invitation is issued, then one reviewable invitation and membership request are recorded without granting unrelated Tenant access.
2. **IAM-AC-002 - Duplicate invitation:** Classification: Confirmed. Given a matching pending or active membership, when another invitation is requested, then duplicate creation is prevented and the reviewed duplicate outcome is evidenced.
3. **IAM-AC-003 - Invitation expiry:** Classification: Proposed. Given an invitation past the founder-approved validity condition, when activation is attempted, then activation is rejected and a new authorized invitation is required.
4. **IAM-AC-004 - Activation:** Classification: Confirmed. Given a valid invitation and eligible Tenant, when the User completes the approved activation path, then the account and membership become eligible only for the recorded Roles and scopes.
5. **IAM-AC-005 - Suspended Tenant activation denied:** Classification: Confirmed. Given a suspended Tenant, when a new User attempts activation or sign-in, then prohibited access is denied and the outcome is evidenced.
6. **IAM-AC-006 - Authentication success:** Classification: Confirmed. Given an active User with a valid membership and required authentication evidence, when authentication succeeds, then the User can act only within the authorized Tenant context.
7. **IAM-AC-007 - Authentication failure:** Classification: Confirmed. Given invalid authentication evidence, when the User attempts sign-in, then access is denied, a safe outcome is provided, and the failed event is recorded.
8. **IAM-AC-008 - Lockout policy:** Classification: Proposed. Given the approved failed-authentication policy threshold is reached, when another attempt occurs, then the approved protective state and evidence apply without exposing another Tenant.
9. **IAM-AC-009 - Recovery verification:** Classification: Proposed. Given an eligible recovery request, when the claimant satisfies the approved verification policy, then the approved recovery outcome is recorded and unsafe sessions/access are addressed.
10. **IAM-AC-010 - Recovery of deactivated identity:** Classification: Proposed. Given a deactivated User or membership, when recovery is requested, then ordinary recovery does not silently reactivate access and an authorized decision is required.
11. **IAM-AC-011 - Membership scope:** Classification: Confirmed. Given a User has membership in Tenant A but not Tenant B, when the User targets Tenant B, then access is denied without revealing Tenant B data.
12. **IAM-AC-012 - Multiple Tenant membership isolation:** Classification: Confirmed. Given a User has approved memberships in two Tenants, when the User changes context, then each context exposes only its own authorized Roles, scopes, and data.
13. **IAM-AC-013 - Role assignment:** Classification: Confirmed. Given a valid membership and an available Role, when an authorized assignment is approved, then the User gains only the Role's approved Permissions and scope.
14. **IAM-AC-014 - Role unavailable:** Classification: Confirmed. Given a requested Role has been retired or is unavailable, when assignment is attempted, then the User does not receive stale authority and the exception is visible.
15. **IAM-AC-015 - Permission without Entitlement:** Classification: Confirmed. Given a User has a Permission but the Tenant lacks the applicable Entitlement, when the action is attempted, then access is denied and no commercial capability is granted.
16. **IAM-AC-016 - Entitlement without Permission:** Classification: Confirmed. Given the Tenant has an Entitlement but the User lacks the Permission, when the action is attempted, then access is denied.
17. **IAM-AC-017 - Company scope:** Classification: Confirmed. Given a User is scoped to Company A only, when the User acts on Company B, then the action is denied and the valid Company A authority is unchanged.
18. **IAM-AC-018 - Branch/Warehouse scope:** Classification: Confirmed. Given a User is scoped to one Branch or Warehouse, when the User targets an unrelated or inactive scope, then the action is denied and the attempt is evidenced.
19. **IAM-AC-019 - Self-approval:** Classification: Confirmed. Given a policy prohibits self-approval, when a User attempts to approve their own prohibited request, then the approval is rejected and the reason is recorded.
20. **IAM-AC-020 - SoD conflict:** Classification: Confirmed. Given the approved SoD matrix identifies a conflict, when a User requests or performs the conflicting assignment/action, then it is blocked or routed to the approved exception path.
21. **IAM-AC-021 - Privileged request:** Classification: Confirmed. Given a named requester, business purpose, Tenant, scope, requested actions, and eligible approver, when privileged access is approved, then only the requested boundary becomes active and the approval evidence is retained.
22. **IAM-AC-022 - Privileged request rejection:** Classification: Confirmed. Given missing purpose, ineligible approver, self-approval, conflict, or unsupported scope, when privileged access is requested, then it is rejected or held and no privilege is activated.
23. **IAM-AC-023 - Support access:** Classification: Confirmed. Given a valid case, named support User, Tenant authorization where required, approved scope, purpose, and interval, when support access begins, then only that boundary is available and activity is evidenced.
24. **IAM-AC-024 - Support expiry:** Classification: Confirmed. Given the support interval has ended, when further support access is attempted, then it is denied and closure evidence remains.
25. **IAM-AC-025 - Support cross-Tenant attempt:** Classification: Confirmed. Given support is authorized for Tenant A, when the same identity targets Tenant B, then access is denied without revealing Tenant B and a security event is recorded.
26. **IAM-AC-026 - Support export separation:** Classification: Confirmed. Given a support User requests an export, when support authorization alone is evaluated, then export is denied until separate Permission, export authorization, and explicit Tenant authorization are present.
27. **IAM-AC-027 - Suspension:** Classification: Confirmed. Given an authorized suspension with reason and scope, when the suspension takes effect, then prohibited sessions and actions are denied and the suspension evidence is visible.
28. **IAM-AC-028 - Session invalidation:** Classification: Confirmed. Given a critical membership, Role, Permission, scope, or security change, when the change becomes effective, then affected sessions cannot use the revoked authority.
29. **IAM-AC-029 - Revocation:** Classification: Confirmed. Given a Role, Permission, scope, or membership is revoked, when the User attempts the formerly authorized action, then it is denied and the original decision remains auditable.
30. **IAM-AC-030 - Offboarding:** Classification: Confirmed. Given a User is offboarded, when the process completes, then applicable memberships, Roles, scopes, support access, and sessions no longer authorize operations while required evidence remains.
31. **IAM-AC-031 - Migration exception:** Classification: Confirmed. Given an ambiguous identity, Role, or scope mapping, when migration validation runs, then activation is held, an owner is assigned, and the decision is reconciled before access is enabled.
32. **IAM-AC-032 - Audit retrieval:** Classification: Confirmed. Given an auditor or authorized reviewer requests material access evidence, when the evidence is retrieved, then actor, Tenant, scope, action, time, outcome, and safe decision context are available without allowing Tenant-user editing.

## 27. Open Decisions

Each decision remains open until Hossam records the decision and the affected source/glossary/traceability records are updated. No recommended option below is an approved requirement.

| ID | Question | Why it matters | Source evidence | Options / trade-offs | Recommended option when supportable | Owner | Implementation gate | Status |
|---|---|---|---|---|---|---|---|---|
| IAM-OD-001 | May one User hold multiple active Tenant Memberships, and how is switching between them governed? | Determines external accountant and multi-Tenant operating model, review, and isolation expectations. | Glossary permits an example with two memberships; BR-010 requires isolation. | A: permit multiple explicit memberships; B: one active Tenant at a time; C: prohibit multiple memberships. | Retain explicit memberships and decide cardinality through a controlled policy; this preserves the approved membership meaning. | Hossam | Before IAM DDD/FRS and affected implementation | Open |
| IAM-OD-002 | What is the identity-uniqueness boundary: globally unique, Tenant-unique, or both with a controlled linking rule? | Determines duplicate detection, invitations, migration, and privacy exposure. | PLT-003 duplicate detection; M27-REQ-025; glossary User. | Global uniqueness improves deduplication; Tenant uniqueness supports separation; linking creates governance cost. | No option is sufficiently supported; decide with privacy and migration evidence. | Hossam + Security/Privacy | Before migration and identity persistence design | Open |
| IAM-OD-003 | Which authentication factors are required for ordinary Users, and under which risk or Tenant conditions? | Defines who may authenticate and the account-recovery burden. | PRD section 8 requires modern password/session management and MFA support; ADR-004. | Password only; optional MFA; mandatory MFA by risk/Tenant; external identity. | Preserve MFA capability and decide required enforcement by risk; no factor is silently selected. | Hossam + Security | Before authentication TDS/implementation | Open |
| IAM-OD-004 | What additional authentication or reauthentication is required for privileged Users and high-risk actions? | Limits impact of credential compromise and supports privileged evidence. | M27-REQ-048/049; ADR-004; BR-011. | Same factor; stronger factor; fresh reauthentication; separate privileged identity. | Fresh risk-aware confirmation is a sensible control, but exact factor and trigger require Security/Hossam approval. | Hossam + Security | Before privileged-access implementation | Open |
| IAM-OD-005 | Who owns the password/credential policy and which business outcomes must it cover? | Affects account creation, lockout, recovery, support, and audit. | PRD section 8; Architecture security baseline. | Platform policy; Tenant-governed options; mixed minimum plus governed additions. | Platform-controlled minimum with governed Tenant options is a candidate, not an approval. | Hossam + Security | Before authentication and onboarding | Open |
| IAM-OD-006 | Who owns account recovery and what verification, notification, and escalation outcomes are required? | Recovery is a high-risk path that can bypass ordinary authentication. | Jira MESP-28 scope; PRD security; M27 support controls. | User self-service; Tenant Administrator; Platform support; controlled combination. | No recommendation until identity uniqueness and factor policy are decided. | Hossam + Security/Privacy | Before recovery implementation | Open |
| IAM-OD-007 | How long is an invitation valid, and can it be withdrawn, reissued, or transferred? | Prevents stale invitations and ambiguous ownership. | M27-REQ-029; PLT-007; no duration in sources. | Fixed period; event-based expiry; administrator withdrawal; combination. | Require expiry and withdrawal capability; exact period and transfer rule remain open. | Hossam | Before invitation implementation | Open |
| IAM-OD-008 | What is the business behavior after failed authentication: lockout, suspension, progressive response, notification, and recovery? | Balances attack resistance with continuity and support workload. | PRD section 8; no threshold or duration approved. | Lockout; temporary suspension; progressive controls; manual review. | No threshold or duration invented; choose with Security evidence. | Hossam + Security | Before authentication implementation | Open |
| IAM-OD-009 | Which session-duration categories apply to ordinary, sensitive, and privileged activity? | Determines when continued use stops being valid. | PRD section 8 says configurable session expiry; ADR-004. | One duration; category-based; activity-based; risk-based. | Category-based review is a candidate, but values and triggers remain open. | Hossam + Security | Before session TDS/implementation | Open |
| IAM-OD-010 | Are concurrent sessions allowed, limited, or terminated after a new sign-in or risk event? | Affects shared-device risk, continuity, and incident response. | Architecture baseline mentions sessions but gives no business value. | Unlimited; capped; newest-only; risk-based. | No recommendation without pilot evidence and security review. | Hossam + Security | Before session implementation | Open |
| IAM-OD-011 | Is emergency or break-glass access required, and what approvals, interval, post-review, and evidence apply? | Provides continuity while avoiding an unbounded bypass. | M27-REQ-049 and M27-OQ-005 explicitly leave it open. | No break-glass; dual approval; single emergency authority with post-review; controlled support path. | If required, use a separate time-bounded and post-reviewed policy; do not enable by default. | Hossam + Security/Privacy | Before privileged/support implementation | Open |
| IAM-OD-012 | How often must access be reviewed, who reviews it, and which events trigger an out-of-cycle review? | Reduces stale access and defines KPI completion. | PRD table 17; MESP-38 dependency; no cadence approved. | Monthly; quarterly; event-driven; risk-based. | Event-driven review after critical changes is prudent; cadence requires approval. | Hossam + Security/Business owners | Before access-review implementation | Open |
| IAM-OD-013 | What custom Role behavior is allowed, who can create it, and how is it versioned? | ADM-001 names custom Roles but does not settle guardrails. | PRD ADM-001; glossary Role. | Predefined only; composed custom Roles; Tenant custom Roles from approved Permissions; Platform-only catalogue. | Governed composition from approved Permissions is a candidate; owner and scope need approval. | Hossam + Security | Before Role administration implementation | Open |
| IAM-OD-014 | May a business owner create new Permission definitions, or only assign approved atomic Permissions? | Prevents custom Permission expansion from bypassing security review. | Glossary Permission; ADM-001 custom Roles; no Permission-authoring rule. | Approved catalogue only; Platform-owned new Permissions; Tenant-created Permissions. | Keep atomic Permission definitions Platform/governance controlled; confirm no Tenant-created Permission types. | Hossam + Security | Before Permission model implementation | Open |
| IAM-OD-015 | Are self-service access requests required, optional, or unavailable? | Affects workflow, support burden, and evidence. | Jira required output; no source mandates self-service. | Administrator-only; User request plus approval; manager request; no request feature. | No recommendation; decide from operating model and Wafra evidence without making behavior specific to Wafra. | Hossam | Before access-request workflow | Open |
| IAM-OD-016 | Which access changes require one named approver, dual approval, or no approval? | Makes PLT-005 and MESP-55 actionable without inventing thresholds. | PLT-005; MESP-55 one named approver and no self-approval; MESP-42. | One named approver; dual control for high risk; policy by Role/scope; administrator-only. | One named approver with dual control only where risk requires is consistent with MESP-55, pending approval. | Hossam + Security/Business owners | Before approval and Role implementation | Open |
| IAM-OD-017 | What is the SoD conflict matrix and what is the exception/compensating-control process? | Prevents contradictory assignments and unreviewed exceptions. | Glossary SoD; PRD table 7; MESP-38 dependency. | Hard block; approval exception; compensating review; domain-specific matrix. | Preserve self-approval prohibition and define a reviewed matrix before implementation; no pairs invented. | Hossam + Security/Finance/Domain owners | Before authorization and domain implementation | Open |
| IAM-OD-018 | What is the maximum support-access duration, and when is Tenant authorization mandatory? | Controls cross-Tenant/privacy risk and support continuity. | M27-REQ-045/047; M27-OQ-005; MESP-50. | Fixed maximum; case-specific; risk-based; emergency exception. | Named, case, Tenant, scope, and time-bound access is confirmed; numeric maximum and authorization cases remain open. | Hossam + Security/Privacy | Before support-access implementation and production | Open |
| IAM-OD-019 | Can a deactivated User or membership be reactivated, by whom, and after which checks? | Avoids unsafe restoration and preserves offboarding meaning. | M27-REQ-057/058; no final User state model. | Never; owner approval; reverification; new invitation; case-specific. | Require a fresh authorized decision and evidence; exact route is open. | Hossam + Security/Tenant owners | Before lifecycle/recovery implementation | Open |
| IAM-OD-020 | What historical identity/access evidence must be retained, and how do privacy, legal hold, export, and purge apply? | Determines audit reconstruction and offboarding obligations. | BR-011; glossary Audit Event; MESP-50; KSA-006/007. | Retain all material history; policy-classified retention; legal-hold override; minimize history. | Retain enough evidence to reconstruct material decisions while awaiting qualified privacy/legal review. | Hossam + Privacy/Legal/Security | Before production and retention design | Open |
| IAM-OD-021 | How are migration conflicts in User, Role, Permission, scope, and privileged assignments resolved? | Prevents silent over-granting or ambiguous activation during onboarding. | PRD migration baseline; MESP-40. | Quarantine and owner decision; source-system precedence; manual merge; reject. | Quarantine unresolved mappings and require owner approval before activation. | Hossam + Migration/Business owners | Before migration rehearsal | Open |
| IAM-OD-022 | What is the precedence and inheritance rule among Tenant, Company, Branch, and Warehouse scopes, including scope removal while sessions remain active? | Determines whether a lower scope narrows, overrides, or inherits a higher scope. | BR-010; glossary Access Scope marked Draft; MESP-30 dependency. | Explicit assignment only; hierarchical inheritance; most-specific-wins; deny-overrides. | Explicitly record effective scope and deny expansion; choose inheritance/precedence with Organization and Security owners. | Hossam + MESP-30/Security | Before scope authorization implementation | Open |

## 28. Source Conflict Register

| ID | Conflict or ambiguity | Affected sections | Classification | Proposed resolution path |
|---|---|---|---|---|
| IAM-SC-001 | The Jira prompt and MESP-28 description name MiniERPSaaSPlatform_PRD_v1.2_Final_Approved_Baseline.docx, while the repository and Founder Decision Pack identify MiniERPSaaSPlatform_PRD_v1.2.docx as the canonical approved v1.2 baseline. | 1, 6, 30 | Source conflict | Treat the repository file as canonical for this draft; confirm the naming alias in the Product Decision Register or Jira evidence without renaming the source in this task. |
| IAM-SC-002 | An earlier MESP-28 Jira comment says not to move the task In Progress until the BRD entry criteria are approved, while the current explicit founder authorization instructs that MESP-28 may start now. | 1, 6, 30 | Governance conflict | Preserve the historical comment; treat the later explicit founder authorization as the current execution decision; keep the BRD Draft and MESP-28 In Progress. |
| IAM-SC-003 | The approved MESP-27 review package says not to start MESP-28 under the earlier sequence, while the current founder instruction authorizes MESP-28 after MESP-27 and MESP-57 completion. | 6, 9, 30 | Governance timing conflict | Record the new authorization as the superseding execution event; do not change MESP-27 behavior or approval content. |
| IAM-SC-004 | The glossary marks Access Scope and Separation of Duties as Draft for BRD Validation, while the PRD/Jira task require them in the MESP-28 output. | 8, 14, 15, 27 | Terminology/ownership gap | Keep glossary definitions and boundaries, classify detailed granularity/matrix as Open Decisions, and update the glossary only after founder approval. |

No unresolved source conflict is resolved by assumption. These four conflicts are included in the Jira source-conflict count.

## 29. BRD Coverage Checklist

| Jira MESP-28 required output | Covered section(s) | Coverage status | Deferred owner / decision |
|---|---|---|---|
| Business purpose | 2-3 | Covered | None |
| Actors and responsibilities | 7 | Covered | Final actor/Role catalogue: Hossam, MESP-38 |
| Trigger and preconditions | 11 | Covered | Values tied to open decisions where noted |
| Main process | 11 | Covered | BPMN, if required later, is separate work |
| Alternative paths | 12 | Covered | Open behavior is classified |
| Exception scenarios | 12 | Covered | Open behavior is classified |
| Business rules | 13 | Covered | 40 stable IAM-BR rules |
| Document lifecycle | 20 | Covered | Identity/access evidence lifecycle; no commercial document |
| Status transitions | 19 | Covered | Final state names/durations require Hossam |
| Data requirements | 17 | Covered | Business information only; no structures |
| Validation rules | 18 | Covered | 18 stable business validations |
| Permissions | 14 | Covered | Atomic Permission and Role/scope distinction |
| Approval controls | 16 | Covered | MESP-55 and IAM-OD-016 remain gates |
| Separation of duties | 15 | Covered | Matrix and exception process open in MESP-38/IAM-OD-017 |
| Inventory impact | 25.1 | Covered | Indirect authorization only; MESP-33 owns transactions |
| Accounting impact | 25.2 | Covered | Indirect authorization only; MESP-34 owns transactions |
| Multi-currency impact | 25.3 | Covered | No currency calculation; MESP-34/MESP-54 own it |
| Saudi localization impact | 25.4 | Covered | Bilingual/RTL and Saudi production gates |
| Reports and KPIs | 21 | Covered | Report catalogue remains business-level |
| Audit evidence | 22 | Covered | Material event catalogue |
| Integration requirements | 23 | Covered | Business dependencies, not interface design |
| Migration requirements | 24 | Covered | Mapping, reconciliation, exceptions, approval |
| Given/When/Then scenarios | 26 | Covered | 32 business acceptance scenarios |
| Open decisions | 27 | Covered | 22 stable IAM-OD decisions |
| Business-owner approval | 30 | Covered pending | Hossam must approve before the BRD is baseline |

### Coverage result

All Jira MESP-28 required outputs have a dedicated section and an owner or explicit decision gate. No coverage gap remains in this draft. A covered item is not an approval: rows marked Open or Proposed remain outside the approved baseline until resolved.

## 30. Founder Review and Approval

### 30.1 Items ready for confirmation

Hossam can confirm the following source-supported boundaries:

- Identity and Access is business-only and remains separate from implementation design.
- The approved hierarchy remains Platform -> Tenant -> Company / Legal Entity -> Branch -> Warehouse.
- User, Tenant Membership, Role, Permission, Access Scope, Entitlement, and Audit Event remain distinct concepts.
- Tenant isolation, least privilege, deny-by-default, support boundaries, session invalidation after critical changes, auditability, and offboarding evidence are required controls.
- Suppliers are external business parties and are not system Users.
- Wafra is validation-only and creates no reusable Tenant-specific behavior.
- Release 1 is B2B ERP only; Retail POS is excluded.
- The four source conflicts and 22 IAM-OD decisions are visible rather than silently resolved.

### 30.2 Decisions requiring Hossam

1. IAM-OD-001 and IAM-OD-002 - multi-Tenant membership and identity uniqueness.
2. IAM-OD-003 through IAM-OD-010 - authentication, factors, recovery ownership, invitations, lockout, and session policy.
3. IAM-OD-011 through IAM-OD-018 - emergency access, reviews, Role/Permission customization, requests, approvals, SoD, and support duration.
4. IAM-OD-019 through IAM-OD-022 - reactivation, historical evidence, migration conflicts, and scope precedence.
5. IAM-SC-001 through IAM-SC-004 - source naming, governance timing, and glossary ownership conflicts.

Security/Privacy, Finance/control, Organization, Migration, and other qualified owners must provide the specialist validation named in the applicable decision before production or affected implementation.

### 30.3 Deferred topics

The following remain deferred until the relevant BRD, ADR, or production gate: final authentication factors and credential policy; exact session values; detailed SoD matrix; exact support maximum and emergency access; MESP-50 retention/residency/legal hold/purge; MESP-40 migration mapping; MESP-29 isolation lifecycle; MESP-30 organization behavior; and all DDD, FRS, Data Design, TDS, implementation backlog, and code.

### 30.4 Approval checklist

| Approval item | Founder response |
|---|---|
| Approve Identity and Access BRD v0.1 business scope and boundaries | Pending |
| Confirm the 40-rule classification register (30 Confirmed, 10 Proposed) | Pending |
| Resolve IAM-OD-001 through IAM-OD-022 | Pending |
| Accept or revise IAM-SC-001 through IAM-SC-004 | Pending |
| Confirm Suppliers are external business parties, not Users | Pending |
| Confirm Wafra validation-only treatment | Pending |
| Confirm Release 1 B2B-only and Retail POS exclusion | Pending |
| Confirm no DDD, FRS, Data Design, TDS, implementation backlog, Sprint, or code starts from this draft | Pending |
| Approved by / date | Pending |
| Requested changes | Pending |

**This document remains Draft and MESP-28 remains In Progress until Hossam provides founder approval.**
