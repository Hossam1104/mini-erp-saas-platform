# Foundation Release 1 Lean Implementation Specification

**Version:** v0.2 — Draft for Final Architecture and Security Review
**Status:** Draft — Not Approved for Implementation
**Jira:** MESP-86 - Produce Foundation Release 1 Lean Implementation Specification  
**Scope:** Identity and Access, Multi-Tenancy and Tenant Lifecycle, Organization and Company Structure  
**Branch:** `docs/foundation-release1-lean-spec`  
**Owner:** Hossam / Product Owner  
**Date:** 2 August 2026

This is a design and implementation-preparation document. It does not authorize
application code, database migrations, API or UI implementation, automated-test
implementation, a Sprint, or a production release.

## 1. Document Control

| Field | Value |
|---|---|
| Document | Foundation Release 1 Lean Implementation Specification |
| Version/status | v0.2 / Draft for Final Architecture and Security Review |
| Governing Jira Task | MESP-86 (In Progress; outside all Sprints) |
| Parent Epic | MESP-1 - Product Governance and BRD Management (In Progress) |
| Approved BRD baselines | MESP-28, MESP-29, MESP-30, each v0.2 Approved Release 1 Baseline |
| Technical baseline | `docs/01_Technology_Architecture_Baseline.md` |
| Planned review | Critical architecture, security, and tenant-isolation review |
| Top-level sections | 55 (sections 1-55) |
| Relevant context/module boundaries | 8 |
| Aggregate roots | 14 |
| Domain entities | 22 |
| Value objects | 12 |
| Numbered invariants | 45 |
| Commands / queries | 60 / 31 |
| API operations | 90 across 6 groups (catalogue only; no endpoints implemented) |
| UI journeys/pages | 18 (route and state inventory only) |
| Mandatory safety tests | 48 (strategy only; no tests created) |
| Open technical decisions | 8 |

## 2. Executive Summary

Release 1 foundation is a single modular-monolith design for a B2B ERP. A
globally identified User authenticates once, then operates through an explicitly
selected Tenant context. Tenant business data is isolated by server-derived
context, explicit ownership, relational constraints, deny-by-default policies,
and negative tests. Organization provides the approved Company, Branch,
Warehouse, and Department hierarchy. Identity and access controls determine
whether a user may act; tenant and organization scope determine where the act
may apply.

The design intentionally keeps the three domains separate. It uses small
aggregate boundaries and explicit contracts rather than one graph-shaped
aggregate. Cross-module writes are coordinated by application services and
transactional integration records; implementation must not introduce
microservices, event sourcing, or a second database per Tenant.

Business values approved in MESP-28/29/30 are normative here. Technical session
mechanisms, cookie configuration, session storage, framework settings, and
implementation details remain downstream design concerns and must not change
the approved values. The document is therefore a blueprint for review, not an
implementation authorization.

## 3. Authority and Source Priority

When sources disagree, the following order applies and the discrepancy is
recorded and escalated rather than silently reconciled:

1. Explicit founder-approved decisions and approved change-control records.
2. Approved PRD v1.2.
3. The approved BRD that owns the relevant business concept: MESP-27, MESP-28,
   MESP-29, or MESP-30.
4. The approved architecture baseline and approved ADRs for technical
   feasibility and implementation constraints.
5. Jira MESP-86 for the scope and delivery control of this specification.
6. The approved glossary and decision registers.
7. The Wave 1 backlog and Product Delivery Master Plan.
8. Existing code only as evidence of the current implementation seam.

Jira scope controls this task but cannot override an approved business rule.
Architecture may identify infeasibility or require an explicit ADR, but cannot
silently change approved business behavior. Any conflict is recorded for the
Product Owner and appropriate specialist review.

The glossary supplies terms, not new behavior. Wafra is used only to validate
generic behavior. Retail POS and later domain BRDs are not sources for this
document.

## 4. Scope

This specification prepares the first Release 1 foundation slice:

- global identity, credentials, sessions, MFA, invitation and recovery behavior;
- Tenant membership, context selection, switching, suspension and termination;
- Roles, Platform-approved Permissions, scoped assignments, support access and
  audit evidence;
- Company / Legal Entity, Branch, Warehouse, and Department ownership and
  lifecycle;
- logical persistence, API contracts, UI route/state inventory, security
  controls, observability, and a targeted test strategy for those capabilities;
- implementation slicing guidance for the existing MESP-58 through MESP-64
  Enablers, without changing those Jira issues.

## 5. Out of Scope

The following remain outside this document and outside Release 1 foundation
implementation:

- Procurement, Inventory, Finance, B2B Sales, Production, and Retail POS
  transactions;
- MESP-31 and all downstream BRDs;
- implementation of MESP-58, MESP-59, or any other Enabler;
- physical SQL migrations, controllers, Angular components, tests, or a Sprint;
- microservices, Kubernetes, event sourcing, message brokers, CQRS/mediator
  frameworks, database-per-Tenant, or a separate identity server;
- tenant-specific core code for Wafra;
- production hosting, supported-volume limits, retention, legal hold, purge,
  backup, restoration, residency, or RPO/RTO values governed by MESP-48 and
  MESP-50;
- consolidation, intercompany processing, transfer pricing, or a consolidation
  currency.

## 6. Approved Technology Baseline

| Concern | Approved baseline | Why it fits this slice |
|---|---|---|
| Web client | Angular 22 and TypeScript | First-party enterprise shell, typed contracts, Arabic/RTL support |
| API | ASP.NET Core Web API on .NET 10 LTS | Supported long-lived host with policy middleware and OpenAPI |
| Data access | Entity Framework Core 10 | One operational context initially; module-owned mappings and transactions |
| Database | Microsoft SQL Server 2025 | Shared operational database with schema separation and relational integrity |
| Architecture | Modular Monolith | One deployable boundary for a one-developer team; explicit seams without distributed operations |
| Contract | REST and OpenAPI | Stable, reviewable application boundary; no endpoint implementation in this draft |
| Identity | ASP.NET Core Identity | Password, lockout, MFA capability, and user lifecycle primitives |
| Browser authentication | Secure HTTP-only first-party cookie | Avoids browser token storage; technical options remain downstream design |
| Authorization | Server-side policy and resource authorization | Composes membership, permission, organization scope, lifecycle and support checks |
| Work | SQL-backed durable work plus transactional outbox/inbox | Reliable tenant-aware background processing without a broker |
| Files | Private object storage behind an interface | Tenant-scoped files and future provider choice without coupling the domain |
| Local development | Docker Compose | Repeatable SQL Server, object-storage emulator and telemetry dependencies |
| Observability | OpenTelemetry-compatible logs, metrics and traces | Correlation across API, jobs and security/audit evidence |
| Testing tools | xUnit; Playwright TypeScript | Backend isolation and API validation plus critical browser journeys |

The baseline is a design constraint, not permission to add infrastructure. The
existing three production projects remain `MiniErp.Api`, `MiniErp.App`, and
`MiniErp.Contracts`; module internals remain internal to `MiniErp.App` and
public composition contracts remain explicit.

## 7. Context Map

```mermaid
flowchart LR
  Browser["Angular first-party browser"] --> Api["MiniErp.Api"]
  Api --> App["MiniErp.App public application contracts"]
  App --> Contracts["MiniErp.Contracts"]
  App --> Platform["Platform Governance"]
  App --> Identity["Identity and Access"]
  App --> Tenant["Tenant Lifecycle"]
  App --> Org["Organization"]
  App --> Support["Support Access"]
  Identity --> Audit["Security and Audit"]
  Tenant --> Audit
  Org --> Audit
  App --> Work["SQL durable work / outbox"]
  App --> Files["Private object storage adapter"]
```

The eight relevant boundaries are Platform Governance, Identity and Access,
Tenant Lifecycle, Organization, Support Access, Security/Audit, Files, and
Durable Work/Integration. They share contracts and transaction infrastructure,
not mutable domain state. `TenantContext` is an immutable technical value
assembled by the application/security pipeline; it is not a jointly owned
persisted business aggregate.

## 8. Module and Ownership Boundaries

| Boundary | Owns | May consume | Must not own |
|---|---|---|---|
| Platform Governance | Platform roles, approved permission catalogue, governance records | Tenant lifecycle status for governance | Tenant business data |
| Identity and Access | User, Credential/ASP.NET Core Identity User, User Session, MFA Enrollment, Tenant Membership, Invitation, Password Recovery Request, Role, Role Permission, Role Assignment, Access Scope grant | Tenant lifecycle eligibility and valid organization facts through contracts | Tenant lifecycle or organization tables |
| Tenant Lifecycle | Tenant identity, lifecycle and status, and lifecycle eligibility facts | Identity references and organization summaries through contracts | Credentials, memberships, roles, or organization tables |
| Organization | Company/Legal Entity, Branch, Warehouse, Department, hierarchy and organization lifecycle | Tenant identity and authenticated actor through contracts | Users, permissions, memberships, or Tenant tables |
| Support Access | Cases, named grants, approvals, expiry and evidence | Identity, Tenant, audit contracts | General Tenant authority |
| Security/Audit | Immutable security/audit evidence and correlation | Events from all boundaries | Passwords, raw recovery secrets, business payloads |
| Files | Tenant-scoped object references and private storage adapter | Tenant context, audit contracts | Public blobs or cross-tenant search |
| Durable Work/Integration | Outbox, inbox and background work records | Initiating Tenant and scope context | Unscoped work or hidden business writes |

Each boundary exposes intent-level contracts. Direct access to another module's
tables is prohibited; application orchestration validates the receiving
module's invariants before committing a cross-module operation. Identity owns
the Access Scope grant, while Organization supplies hierarchy facts used to
validate it. No module directly mutates another module's tables.

## 9. Ubiquitous Language

| Term | Meaning in this specification |
|---|---|
| Platform | The SaaS control plane above all Tenants. |
| Tenant | Subscription and data-isolation boundary. |
| Company / Legal Entity | Tenant-owned legal/accounting organization. |
| Branch | Operational subdivision of a Company; not a legal entity or ledger. |
| Warehouse | Stock/location identity below a Branch; transactions are out of scope here. |
| Department | Tenant Company-owned organizational grouping, reusable across Company branches. |
| User | Global login identity; not a Person, Employee, Supplier, or Customer. |
| Membership | Explicit relationship granting a User eligibility in one Tenant. |
| Tenant context | Server-derived, one-Tenant request/workspace/session context. |
| Role | Tenant assignment of Platform-approved Permissions. |
| Access Scope | Downward path from Tenant to Company, Branch, or Warehouse. |
| Support grant | Named, case-bound, Tenant-approved, time-bounded support authority. |
| Active | Lifecycle state that permits the operation in question. |
| Historical reference | Authorized read of a previously used inactive/closed record without rewriting it. |

## 10. Foundation Domain Model

The following model is the minimum cross-domain vocabulary. `Platform` means the
record is global and purpose-bound; `Tenant` means exactly one Tenant key is
required. Lifecycle and invariant references are expanded in sections 13-15.

| Entity | Owner | Ownership class (Platform or Tenant) | Lifecycle | Transaction boundary | Cross-module contract |
|---|---|---|---|---|---|
| User | Identity | Platform/global | Active, suspended, offboarded | User security change | User identity reference |
| Credential / Identity User | Identity | Platform/global | Enabled, locked | Credential or lockout change | Authentication result |
| User Session | Identity | Platform record linked to User and Tenant context | Active, expired, revoked | Session issue/revoke | Revocation evidence |
| MFA Method / Enrollment | Identity | Platform/global per User | Pending, enabled, revoked | Enrollment/change | MFA assurance claim |
| Tenant | Tenant Lifecycle | Platform-owned identity; Tenant boundary | Draft, Provisioning, Configuration Required, Ready for Activation, Active, Grace Period, Suspended, Reactivated, Export Requested, Termination Pending, Terminated, Retained | Tenant lifecycle change | Tenant context eligibility |
| Tenant Membership | Identity | Exactly one Tenant | Invited, active, suspended, revoked | Membership change | Permission evaluation input |
| Invitation | Identity | One target Tenant | Issued, accepted, withdrawn, expired | Issue/accept/withdraw | User and membership creation |
| Password-Recovery Request | Identity | Platform record linked to User | Issued, consumed, expired, revoked | Recovery request | Session-revocation event |
| Role | Identity | Platform definition or one Tenant custom role | Draft, active, retired | Role definition change | Permission-set reference |
| Permission | Platform Governance | Platform-owned catalogue | Active, retired | Catalogue governance | Policy requirement |
| Role Permission | Identity | Follows Role ownership | Active, removed | Role edit | Effective permission set |
| Role Assignment | Identity | One Tenant and optional scope | Pending, active, revoked, expired | Assignment/approval change | Authorization grant |
| Access Scope | Identity | One Tenant; optional Company/Branch/Warehouse references validated by Organization | Active, revoked | Scope change | Resource authorization path |
| Support Case | Support Access | One Tenant and named requester | Open, approved, closed | Case change | Support approval context |
| Support Access Grant | Support Access | One Tenant, one case, named actor | Requested, active, expired, revoked | Grant lifecycle | Bounded support policy |
| Company / Legal Entity | Organization | Exactly one Tenant | Draft, active, inactive, closed | Company lifecycle/configuration | Parent organization reference |
| Branch | Organization | Exactly one Company/Tenant | Draft, active, inactive, closed | Branch lifecycle | Company scope reference |
| Warehouse | Organization | Exactly one Branch/Company/Tenant | Draft, active, inactive, closed | Warehouse lifecycle | Branch scope reference |
| Department | Organization | Exactly one Company/Tenant | Draft, active, inactive, closed | Department lifecycle | Company scope reference |
| Audit Event | Security/Audit | Platform record with optional Tenant key | Immutable | Append only | Evidence reference |
| Outbox Message | Durable Work | Initiating Tenant when applicable | Pending, dispatched, failed, dead-lettered | Same transaction as source change | Integration delivery |
| Background Work Record | Durable Work | Initiating Tenant and scope | Queued, running, succeeded, failed, cancelled | Work state change | Tenant-aware worker contract |

## 11. Aggregate Roots and Consistency Boundaries

There is no giant aggregate. Fourteen roots own only the state that must change
atomically; read models and audit/outbox records are separate persistence
records. Cross-root operations use application orchestration and explicit
contracts.

| Root | Owning boundary | Atomic decisions | Cross-root reference |
|---|---|---|---|
| User | Identity | Identity status and global normalized email | User ID |
| Tenant | Tenant Lifecycle | Tenant lifecycle status | Tenant ID |
| Tenant Membership | Identity | One User's membership in one Tenant | User ID, Tenant ID |
| Invitation | Identity | Invitation issue/withdraw/accept | Target User/Tenant IDs |
| Password-Recovery Request | Identity | Single-use recovery lifecycle | User ID |
| Role | Identity | Role metadata and permission set | Permission IDs |
| Role Assignment | Identity | Assignment, approver and revocation | User, Tenant, Role, Scope IDs |
| Access Scope | Identity | Scope grant and references validated against Organization hierarchy | Company/Branch/Warehouse IDs |
| Support Case | Support Access | Case purpose, tenant and requester | Tenant/User IDs |
| Support Access Grant | Support Access | Approval, exact scope, expiry | Case, Tenant, User IDs |
| Company | Organization | Company identity and approved configuration | Tenant ID |
| Branch | Organization | Branch identity and lifecycle | Company ID |
| Warehouse | Organization | Warehouse identity and lifecycle | Branch ID |
| Department | Organization | Department identity and lifecycle | Company ID |

Sessions, MFA enrollments, audit events, outbox messages, and background work
records have independent persistence lifecycles and are not hidden inside a
domain root. A root never loads an unbounded Tenant graph.

## 12. Entities and Value Objects

The 22 entities are the rows in section 10. The twelve value objects are
immutable, validated at the boundary, and carry no persistence identity:

| Value object | Validation purpose |
|---|---|
| NormalizedEmail | Canonical, globally unique login key |
| TenantContext | Immutable non-persisted request/workspace/session value assembled from User/User Session, active Membership, Tenant lifecycle, Organization hierarchy, and support grant |
| SessionWindow | Maximum and inactivity timestamps |
| MfaAssurance | Required level and fresh-auth timestamp |
| MembershipStatus | Explicit active eligibility |
| LifecycleState | Draft/active/inactive/closed or security state |
| ScopePath | Tenant -> Company -> Branch -> Warehouse downward path |
| CompanyConfiguration | Calendar, time zone, functional currency references |
| FiscalCalendarRef | Company calendar selection without physical policy values |
| TimeZoneId | Company time-zone identifier |
| CurrencyCode | Company functional currency code |
| CorrelationId | Request/job/audit trace key |

Credentials, raw MFA secrets, recovery tokens, and cookie values are sensitive
values handled by approved framework/adapters and are never represented as
auditable domain value objects.

## 13. Domain Invariants

The following 45 invariants are the non-negotiable Release 1 safety baseline.

1. **I-01:** Normalized email is globally unique.
2. **I-02:** User is a global identity and is not duplicated per Tenant.
3. **I-03:** Business access requires an active, explicit Membership.
4. **I-04:** Every protected request, workspace, and session has exactly one Tenant context.
5. **I-05:** Separate authorized contexts may coexist without sharing mutable state.
6. **I-06:** A client-supplied Tenant identifier never expands authority.
7. **I-07:** Tenant A state is never displayed, submitted, searched, or reused in Tenant B.
8. **I-08:** Every Tenant-owned record has exactly one Tenant owner.
9. **I-09:** Platform records are purpose-bound and cannot expose business data.
10. **I-10:** Cross-Tenant access is deny-by-default on reads, writes, search, reports, exports, files, jobs, notifications, integrations, audit, and support.
11. **I-11:** Platform Administrator alone does not grant Tenant business-data access.
12. **I-12:** Scope flows only downward Tenant -> Company -> Branch -> Warehouse.
13. **I-13:** Branch/Warehouse scope cannot be used upward to Company/Tenant-wide data.
14. **I-14:** Release 1 has no explicit-deny overlay; absence of a grant denies.
15. **I-15:** Roles contain only Platform-approved Permissions.
16. **I-16:** Privileged assignments have a separate named approver; self-approval is prohibited.
17. **I-17:** MFA is mandatory for approved privileged actors and operations.
18. **I-18:** Five failed attempts cause a 15-minute lockout.
19. **I-19:** Ordinary sessions have an 8-hour maximum lifetime.
20. **I-20:** Ordinary sessions expire after 30 minutes of inactivity.
21. **I-21:** Concurrent sessions are allowed, subject to independent revocation.
22. **I-22:** Suspension, offboarding, password reset, and critical access changes revoke affected sessions.
23. **I-23:** Administrators cannot view or set a user's password.
24. **I-24:** Support access is named, case-bound, Tenant-approved, exact-scope, and time-bounded.
25. **I-25:** Support access is at most 8 hours per grant and does not alone grant export authority.
26. **I-26:** Company belongs to one Tenant; Branch to one Company; Warehouse to one Branch; Department to one Company.
27. **I-27:** Inactive or closed units reject new users, documents, jobs, integrations, and transactions.
28. **I-28:** Parent inactivity blocks descendants; parent reactivation does not auto-restore descendants.
29. **I-29:** Authorized historical references remain readable and preserve original ownership.
30. **I-30:** Used parent ownership cannot be silently rewritten.
31. **I-31:** Wafra is validation-only and cannot create Tenant-specific core behavior.
32. **I-32:** Retail POS is excluded from Release 1 foundation scope.
33. **I-33:** Files, exports, reports, search indexes, notifications, and audit evidence carry Tenant scope where applicable.
34. **I-34:** Background work preserves initiating Tenant and organization scope.
35. **I-35:** MESP-50 controls retention, legal hold, purge, residency, backup, and restoration; no physical purge is designed here.
36. **I-36:** MESP-48 controls supported volume and performance evidence; this document invents no limits.
37. **I-37:** Tenant lifecycle transitions are limited to the approved Draft, Provisioning, Configuration Required, Ready for Activation, Active, Grace Period, Suspended, Reactivated, Export Requested, Termination Pending, Terminated, and Retained states.
38. **I-38:** Tenant suspension blocks ordinary interactive and asynchronous business operations; required Platform safety and governance operations may continue.
39. **I-39:** Tenant reactivation re-evaluates Users, Memberships, sessions, integrations, jobs, descendants, drafts, and pending work and never automatically restores them.
40. **I-40:** Tenant termination revokes ordinary access while preserving evidence; Retained remains subject to MESP-50 and Purged is not a Release 1 state.
41. **I-41:** Valid Tenant A working state remains explicitly owned by Tenant A when switching away, is never automatically deleted, and can return only after current authorization re-evaluation; invalid state is never restored.
42. **I-42:** A persistence write is rejected when TenantId is missing, inconsistent with trusted context, or changed after a used record is established.
43. **I-43:** Same-Tenant composite relationships are enforced for hierarchy, membership, assignments, scopes, support records, files, exports, reports, and work metadata.
44. **I-44:** IgnoreQueryFilters, raw SQL, bulk, Platform maintenance, and migration paths are unavailable to ordinary Tenant application paths and require a named privileged, purpose-bound, authorized, reviewed, audited contract.
45. **I-45:** Session renewal never extends the original absolute maximum; fresh authentication is bound to the specific protected operation or challenge completion unless a separate security decision approves a reusable window.

## 14. Lifecycle and State Models

| Object | States | Allowed transitions and guard |
|---|---|---|
| User | Active, Suspended, Offboarded | Security/owner action; suspension/offboarding revokes affected sessions; reactivation requires review |
| Membership | Invited, Active, Suspended, Revoked | Active only after explicit assignment; revoked membership cannot select context |
| Tenant | Draft, Provisioning, Configuration Required, Ready for Activation, Active, Grace Period, Suspended, Reactivated, Export Requested, Termination Pending, Terminated, Retained | Only approved guards may advance state; suspension blocks ordinary work; termination revokes ordinary access and preserves evidence |
| Invitation | Issued, Accepted, Withdrawn, Expired | Seven-day business value; single target and non-transferable |
| Recovery request | Issued, Consumed, Expired, Revoked | One-use verified-email path; success revokes affected sessions |
| Role/assignment | Draft, Active, Retired / Pending, Active, Revoked, Expired | Permission catalogue and approval policies apply before activation |
| Support case/grant | Open, Approved, Closed / Requested, Active, Expired, Revoked | Exact Tenant/scope, named approver, maximum eight-hour grant |
| Company/Branch/Warehouse/Department | Draft, Active, Inactive, Closed | Parent and historical-reference rules in section 32 |

The Tenant lifecycle deliberately has no Purged Release 1 state. Purged may only
be a future MESP-50-gated terminal outcome. No production purge, retention,
grace, cooling-off, backup, restoration, or purge duration is invented here.
Reactivation re-evaluates Users, Memberships, sessions, integrations, jobs,
descendants, drafts, and pending work; it does not automatically restore any of
them. Required Platform safety and governance operations may continue while a
Tenant is Suspended. No state transition silently restores old privileges. A
state change emits a domain event and an audit record after the transaction is
durable.

## 15. Domain Events

Events are internal integration facts, not a mandate for event sourcing. Every
event includes a correlation ID, actor ID, initiating Tenant when applicable,
scope snapshot, occurred-at time, schema version, and sensitivity classification.

| Event | Producer | Consumers |
|---|---|---|
| UserActivated / UserSuspended / UserOffboarded | Identity | Session revocation, audit |
| CredentialChanged / PasswordResetCompleted | Identity | Session revocation, audit |
| MfaEnrolled / MfaRevoked | Identity | Assurance policy, audit |
| MembershipActivated / MembershipSuspended / MembershipRevoked | Identity | Tenant Lifecycle context eligibility; session revocation |
| InvitationIssued / Accepted / Withdrawn / Expired | Identity | Membership workflow, audit |
| RoleChanged / AssignmentApproved / AssignmentRevoked | Identity | Policy cache invalidation, audit |
| SupportCaseApproved / SupportGrantActivated / Expired / Revoked | Support | Support policy, audit |
| TenantActivated / Suspended / Reactivated / Terminated | Tenant Lifecycle | Context, work and session guards |
| TenantDraftCreated / ProvisioningStarted / ConfigurationRequired / ReadyForActivation | Tenant Lifecycle | Lifecycle workspace and activation guards |
| TenantGracePeriodEntered / ExportRequested / TerminationPending / RetainedRecorded | Tenant Lifecycle | Export, termination and evidence workflows |
| CompanyChanged / BranchChanged / WarehouseChanged / DepartmentChanged | Organization | Scope validation, audit |
| OutboxDispatched / WorkCompleted / WorkFailed | Durable Work | Observability and retry policy |

## 16. Commands and Queries

Commands are intent-level application operations; queries are read operations.
The catalogue contains 60 commands and 31 queries. They do not imply
controllers, database migrations, or implementation work. Every command is
mapped to a journey and to an API/application entry point in sections 17 and 37.

### Commands (60)

**User, authentication, and recovery:**

1. `RegisterUser` (internal step of invitation acceptance or recovery; not a
   public operation); 2. `ConfirmEmail` (internal verification step; not a
   public operation); 3. `SignIn`; 4. `SignOut`; 5. `RevokeSession`;
6. `BeginMfaChallenge`; 7. `VerifyMfa`; 8. `EnrollMfa`; 9. `RevokeMfa`;
10. `SuspendUser`; 11. `ReactivateUser`; 12. `OffboardUser`;
13. `RevokeAffectedUserSessions`; 14. `RequestPasswordRecovery`;
15. `CompletePasswordRecovery`.

**Tenant lifecycle:**

16. `CreateTenantDraft`; 17. `StartTenantProvisioning`;
18. `MarkTenantConfigurationRequired`; 19. `MarkTenantReadyForActivation`;
20. `ActivateTenant`; 21. `EnterTenantGracePeriod`; 22. `SuspendTenant`;
23. `ReactivateTenant`; 24. `RequestTenantExport`;
25. `BeginTenantTermination`; 26. `TerminateTenant`;
27. `RecordTenantRetainedState`.

**Invitation lifecycle:**

28. `IssueInvitation`; 29. `AcceptInvitation`; 30. `WithdrawInvitation`;
31. `ReissueInvitation` (creates a new invitation; never transfers the old one).

**Membership, Roles, scope, and review:**

32. `ActivateMembership`; 33. `SuspendMembership`; 34. `RevokeMembership`;
35. `CreateRole`; 36. `UpdateRole`; 37. `AssignRole`;
38. `ApprovePrivilegedAssignment`; 39. `RevokeAssignment`;
40. `RevokeAccessScope`; 41. `StartAccessReview`;
42. `RecordAccessReviewDecision`.

**Support access:**

43. `OpenSupportCase`; 44. `ApproveSupportGrant`;
45. `ActivateSupportGrant`; 46. `RevokeSupportGrant`.

**Organization management:**

47. `CreateCompany`; 48. `UpdateCompanyConfiguration`;
49. `ConfirmFiscalCalendar`; 50. `ConfirmOperatingTimeZone`;
51. `ConfirmFunctionalCurrency`; 52. `ChangeCompanyLifecycle`;
53. `CreateBranch`; 54. `UpdateDraftBranchParent`;
55. `ChangeBranchLifecycle`; 56. `CreateWarehouse`;
57. `UpdateDraftWarehouseParent`; 58. `ChangeWarehouseLifecycle`;
59. `CreateDepartment`; 60. `ChangeDepartmentLifecycle`.

### Queries (31)

1. `GetSessionStatus`; 2. `ListSessions`; 3. `GetCurrentTenantContext`;
4. `ListEligibleMemberships`; 5. `GetTenantLifecycle`;
6. `ListTenantLifecycleHistory`; 7. `GetTenantExportStatus`;
8. `ListUsers`; 9. `GetUser`; 10. `ListMemberships`; 11. `ListRoles`;
12. `GetRole`; 13. `ListPermissions`; 14. `ListAssignments`;
15. `ListAccessReviewEvidence`; 16. `GetAccessReview`; 17. `ListCompanies`;
18. `GetCompany`; 19. `GetCompanyConfiguration`; 20. `ListBranches`;
21. `GetBranch`; 22. `ListWarehouses`; 23. `GetWarehouse`;
24. `ListDepartments`; 25. `GetDepartment`; 26. `GetCompanyHierarchy`;
27. `GetHistoricalOrganizationReference`; 28. `ListSupportCases`;
29. `GetSupportCase`; 30. `ListSupportEvidence`; 31. `ListAuditEvidence`.

`RegisterUser` and `ConfirmEmail` are application-internal steps, not public
API operations; they are exercised by the invitation and recovery journeys.
Every other command and query has a named operation in section 37. Every query
derives Tenant context on the server, applies authorization and lifecycle
guards, and returns only records safe for that context.

## 17. User Journeys

| Journey | Primary actor | Outcome | Blocking dependency |
|---|---|---|---|
| Sign in and establish context | User | Authenticated session with one eligible Tenant context | Identity and membership |
| MFA challenge and fresh auth | Privileged user | Required assurance for protected operation | MFA policy and session evidence |
| User suspension, reactivation and offboarding | Platform administrator or authorized Tenant administrator | User lifecycle change with affected-session revocation and explicit re-evaluation | Identity policy and approval |
| Recover password | User | Verified recovery and affected-session revocation | Email delivery adapter decision |
| Accept invitation | Invitee | User and explicit Membership established | Invitation validity |
| Tenant lifecycle workspace | Platform administrator or authorized Tenant administrator | Draft through Retained state with guarded transitions | Tenant Lifecycle policy and MESP-50 gate |
| Select/switch Tenant | Multi-Tenant user | Safe context replacement with no state leakage | Context resolver |
| Return to a Tenant | Multi-Tenant user | Previously valid Tenant-owned state is available only after current re-evaluation | Membership, lifecycle, scope, Permission, session and support grant |
| Manage users and memberships | Tenant Admin | Explicit member lifecycle | Tenant and organization scope |
| Manage Role and Permissions | Tenant Admin / Platform approver | Approved grant without self-approval | Permission catalogue |
| Review/revoke sessions | User/Admin | Independent session control | Revocation evidence |
| Manage Company hierarchy | Tenant Admin | Company, Branch, Warehouse, Department lifecycle | Parent active and same Tenant |
| Confirm Company configuration | Tenant Admin / Finance boundary | Fiscal calendar, operating time zone and functional currency are explicitly confirmed | Organization ownership and approved configuration rules |
| Request and approve support | Support actor / Tenant approver | Named exact-scope time-bounded grant | Case and MFA policy |
| Review audit/access evidence | Authorized reviewer | Evidence without secrets or leakage | Audit boundary |

## 18. Screen and Route Inventory

The 18 pages/journeys are route-level design references only:

1. `/login` Login; 2. `/mfa` MFA challenge; 3. `/password-recovery` password recovery;
4. `/invitation/accept` invitation acceptance; 5. `/tenant/select` Tenant selection;
6. `/tenant/lifecycle` Tenant lifecycle workspace; 7. context indicator/switcher;
8. user list/details; 9. membership management;
10. Role catalogue/editor; 11. Permission catalogue; 12. Role/scope assignment;
13. session management; 14. Company list/details; 15. Branch list/details;
16. Warehouse list/details; 17. Department list/details; 18. support approval,
   monitoring, access-review and audit evidence.

## 19. UI States and Validation

Each route must specify loading, empty, success, validation failure, restricted,
expired, suspended, no-access, and unexpected-error states. Context-dependent
pages show the current Tenant and relevant organization path; they never trust a
hidden input as authority. A valid Tenant A draft, filter, prepared result or
working state is explicitly owned by Tenant A and is not automatically deleted
when switching away. It must not be displayed, submitted, reused, interpreted,
searched, cached, exported, or executed in Tenant B. Returning to Tenant A makes
it available only after current Membership, Tenant lifecycle, organization
scope, Permission, session, and support-grant re-evaluation; invalid state is
never restored merely because the User returns. Mutating controls require
server confirmation and refresh after a lifecycle or permission change.

Arabic and English labels, RTL layout, keyboard navigation, focus order,
screen-reader names, visible validation summaries, and non-color-only status
indicators are required. Expired sessions and revoked support grants return to a
safe re-authentication or no-access state without revealing protected data.

## 20. Authentication Architecture

ASP.NET Core Identity owns the global User and credential lifecycle. The API
creates a secure HTTP-only first-party cookie after successful credential and,
when required, MFA verification. The server resolves the User from the cookie,
then evaluates active Membership and Tenant context. No password, raw token, or
cookie value is exposed to the Angular application.

Authentication is distinct from authorization. A valid global User without an
active Membership cannot access Tenant business data. Identity events are
correlated with audit evidence without storing raw secrets.

## 21. Session Architecture

Business values are fixed at eight-hour maximum ordinary lifetime, thirty-minute
inactivity timeout, and concurrent sessions allowed. The safe Release 1
technical default is a server-side `UserSession` record with an opaque session
identifier represented in the protected cookie ticket. Every protected request
validates server-side session status and revocation. Renewal may refresh
inactivity but never extends the original absolute maximum.

Each session has revocation state, issued/last-seen/absolute-expiry evidence,
User ID, and one selected Tenant context at a time. Concurrent sessions are
independent. Password reset, User suspension, offboarding, Membership
revocation, critical Role/Permission/scope changes, and required Tenant
lifecycle changes revoke affected sessions. An expired or revoked session fails
safely without disclosing whether a protected resource exists. Raw cookie
values never enter audit evidence. Any storage, key-management, or renewal
detail beyond this default remains an ADR-004 implementation decision.

## 22. MFA and Fresh-Authentication Behavior

MFA capability is required. MFA is mandatory for Platform Administrators,
Support Users, Tenant Administrators, privileged assignments, and approved
high-risk operations. A fresh-auth claim is required when a policy marks an
operation high risk or when support approval is extended.

The concrete factor, enrollment UX, recovery-code handling, and technical
assurance storage remain downstream design decisions. Failed challenges do not
leak factor details. MFA enrollment/revocation and failed challenge evidence are
audited without secrets. Fresh authentication is distinct from ordinary session
validity: it is bound to the specific protected operation or challenge
completion. No reusable fresh-auth duration is invented; any reusable assurance
window requires an explicit technical security decision before MESP-59 becomes
Ready.

## 23. Password, Recovery, Invitation, and Lockout Behavior

- Passwords are handled only by ASP.NET Core Identity; administrators cannot set
  or view them.
- Five failed attempts cause the approved fifteen-minute lockout behavior.
- Recovery starts from a verified email path, uses a single-use opaque request,
  and revokes affected sessions after completion.
- Invitations are single-target, non-transferable, withdrawable, and valid for
  seven days; acceptance creates or activates the explicit Membership only after
  all Tenant and User guards pass.
- Safe errors do not reveal whether an email, account, Tenant, or invitation
  exists.

## 24. Tenant Context Resolution

The trusted server pipeline resolves context in this order:

1. Authenticate the global User and session.
2. Load the requested or persisted context only from server-side membership and
   lifecycle facts.
3. Verify exactly one Tenant, then optional Company/Branch/Warehouse scope from
   Organization-owned hierarchy facts.
4. Verify support-case/grant constraints when the actor is support.
5. Assemble an immutable, non-persisted `TenantContext` value from the
   authenticated User and User Session, active Identity Membership, eligible
   Tenant lifecycle, valid Organization hierarchy, and applicable support grant.
6. Attach that context to application services, queries, audit, file access,
   and background work creation.

Missing, stale, suspended, terminated, or ambiguous context is denied. A client
Tenant ID is a selector hint, never an authorization input.

## 25. Tenant Context Switching

Switching is an explicit command that re-evaluates Membership, Tenant status,
organization scope, current Permission, session assurance, and support grant.
Tenant A state is never displayed, submitted, reused, interpreted, searched,
cached, exported, or executed inside Tenant B. Switching away does not
automatically delete valid Tenant A drafts, filters, prepared results, or working
state; each remains explicitly owned by Tenant A. The old context is cleared
from the active request/workspace before the new one is attached.

Returning to Tenant A may expose preserved state only after successful current
Membership, lifecycle, organization-scope, Permission, session, and support
grant re-evaluation. Revoked, expired, suspended, terminated, or otherwise
invalid state is never restored merely because the User returns. Separate
authorized browser sessions/workspaces may operate in different Tenants, but
their state, caches, and authority remain isolated. Audit records capture the
decision and correlation ID without leaking protected state.

## 26. Tenant-Isolation Enforcement Model

Defense in depth is mandatory:

1. Trusted server-derived Tenant context.
2. Explicit Tenant ownership on every Tenant-owned record.
3. Deny-by-default policy and resource guards.
4. Tenant-aware unique constraints.
5. Tenant-aware relationships/FKs where appropriate.
6. No unscoped repository/query path for Tenant data.
7. Background work keeps initiating Tenant and organization scope.
8. Tenant-specific files, exports, reports, search, cache and audit evidence.
9. Safe context switching with active-context clearing while preserving valid
   Tenant-owned state.
10. Cross-Tenant negative tests for every protected surface.
11. Logging of denied cross-Tenant attempts without leaking the target data.

`TenantContext` is assembled by the application/security pipeline and is not a
shared persisted aggregate. Identity supplies the authenticated User, User
Session, and active Membership; Tenant Lifecycle supplies eligibility and
status; Organization supplies valid hierarchy facts; Support Access supplies a
case-bound grant where applicable. Each module owns its own tables and exposes
contracts rather than direct table access.

Every Tenant-owned record has a non-null `TenantId`. Tenant-aware alternate or
unique keys support same-Tenant relationships, and logical composite
relationships enforce same-Tenant ownership for Branch -> Company,
Warehouse -> Branch, Department -> Company, Membership -> Tenant, Role
Assignment -> Tenant, Access Scope -> Tenant and its hierarchy, Support
Case/Grant -> Tenant, and Tenant-owned file/export/report/work metadata. A
global query filter may be a convenience and defense-in-depth measure but is
never the only control.

The application write pipeline or `SaveChanges` guard rejects missing Tenant
ownership, ownership inconsistent with trusted context, cross-Tenant relationship
changes, and attempts to change immutable Tenant ownership after use. Normal
Tenant repositories and query handlers require trusted `TenantContext`.
`IgnoreQueryFilters`, raw SQL, bulk operations, Platform maintenance, and
migration paths are unavailable to ordinary Tenant application paths; each
requires a named privileged contract, purpose, explicit authorization, review,
audit evidence, and negative tests. Outbox, background work, files, exports,
search metadata, and audit evidence retain initiating Tenant and scope.

Optional SQL Server row-level security is a defense-in-depth decision, not a
prerequisite for this design. ADR-016 must be approved before production if RLS
is adopted; application and relational ownership controls remain required.

## 27. Role, Permission, and Assignment Model

Platform Governance owns the Permission catalogue. A Role references only
active Platform-approved Permissions. Tenant administrators may manage ordinary
Tenant Roles within their authority; privileged Role or scope assignments need a
separate named approver and never permit self-approval. Assignment records carry
User, Tenant, Role, scope, approver, effective state, and revocation evidence.

Role changes, scope removal, Membership suspension, and other critical access
changes invalidate affected sessions. UI visibility is advisory; every command
and protected query repeats server-side policy and resource checks.

## 28. Organization Access-Scope Enforcement

An Access Scope is a Tenant-owned downward path: Tenant-wide, Company, Branch,
or Warehouse. A grant at a parent can authorize descendants; a child grant
cannot authorize a parent or sibling. Release 1 has no explicit deny overlay.
Organization lifecycle is evaluated after scope: inactive or closed parents and
descendants block new work even if a Role exists. Department is Company-owned
and is not an authorization level unless a later approved requirement adds one.

## 29. Platform Administrator Boundary

Platform Administrators may manage Platform governance records, approved
Permissions, Tenant lifecycle governance, and evidence required for operations.
The role alone does not grant Tenant business-data access. A separate explicit
Tenant Membership and applicable Permission/scope are required. Platform
records that reference a Tenant are purpose-bound and must not become a covert
business-data query path.

## 30. Support-Access Boundary

Support access starts with a named Support Case, the target Tenant, a purpose,
requested exact scope, named support actor, Tenant approval, and MFA. A grant is
time-bounded to at most eight hours, can be revoked or expire, and must be
re-approved for extension. Support access does not alone grant export authority
and cannot cross into another Tenant. All requests, approvals, activations,
denials, expiries, and revocations are immutable evidence without raw secrets or
business payloads.

## 31. Company, Branch, Warehouse, and Department Design

- Company / Legal Entity belongs to exactly one Tenant and owns the active
  fiscal-calendar, time-zone, and functional-currency selections defined by the
  approved Organization BRD. Finance owns later accounting behavior.
- Branch belongs to one Company; it is not a legal entity or independent ledger.
- Warehouse belongs to one Branch. Stock effects belong to Inventory, while this
  slice owns identity and hierarchy.
- Department belongs to one Company and may be reused across that Company's
  branches; it is not a scope level by itself.
- Saudi Gregorian January-December, Asia/Riyadh, and SAR are approved BRD
  recommendations/prefills where applicable, not a new country-specific code
  path in this specification.

## 32. Parent Lifecycle and Historical-Reference Behavior

An inactive or closed Company, Branch, Warehouse, or Department rejects new
users, documents, jobs, integrations, and transactions. Parent inactivity blocks
descendants. Reactivating a parent does not auto-restore descendants; each
descendant is re-evaluated independently.

Historical references remain readable to an authorized actor and retain original
ownership. A used parent cannot be silently reassigned. A Draft parent may only
change its valid same-Tenant parent before it has been Active and before any
transactions, stock, documents, files, reports, numbering, integrations, jobs,
users, or historical references; otherwise a controlled migration or closure is
required. The physical migration is outside this draft.

## 33. Logical Data Model

The logical model uses stable identifiers, explicit ownership, status, audit
metadata, and optimistic concurrency markers without specifying SQL lengths or
physical migrations.

| Record family | Required keys and relationships | Integrity and lifecycle rule |
|---|---|---|
| Identity | User ID; NormalizedEmail unique; sessions/MFA/recovery reference User | Global identity; secrets are framework-managed |
| Membership | Membership ID, User ID, Tenant ID, status | Same-Tenant relationship; unique active relationship per User/Tenant; explicit state required |
| Tenant | Tenant ID, lifecycle status | Platform-governed boundary; complete lifecycle; termination preserves evidence |
| Roles | Role ID, Permission IDs, Tenant owner where custom | Permission catalogue FK; privileged assignment approval evidence |
| Assignment | Assignment ID, User ID, Role ID, Tenant ID, approver | Same-Tenant User/Role/Tenant relationship; no self-approval |
| Scope | Scope ID, Tenant ID, optional Company/Branch/Warehouse IDs | Same-Tenant hierarchy and downward-path validation |
| Support | Case ID and Grant ID with Tenant/User/scope/approver | Same-Tenant exact scope, expiry, revocation and no-export-alone rule |
| Organization | Company ID/Tenant ID; Branch/Company ID; Warehouse/Branch ID; Department/Company ID | Same-Tenant FKs, parent lifecycle and historical references |
| Audit | Event ID, correlation, actor, purpose, optional Tenant | Append-only; no raw auth secrets or sensitive payloads |
| Durable work | Message/work ID, initiating Tenant/scope, status | Idempotency key and retry state; no unscoped work |

All Tenant-owned records carry one non-null Tenant owner in the logical model,
including file references, exports, reports, search documents, notifications,
and audit/work records where applicable. Composite uniqueness is Tenant-aware.
Deletion is not a default lifecycle operation; deactivation, closure, revocation,
and retention gates preserve historical evidence.

## 34. Logical ERD

```mermaid
erDiagram
  USER ||--o{ TENANT_MEMBERSHIP : has
  USER ||--o{ USER_SESSION : opens
  USER ||--o{ MFA_ENROLLMENT : owns
  TENANT ||--o{ TENANT_MEMBERSHIP : admits
  TENANT ||--o{ COMPANY : contains
  COMPANY ||--o{ BRANCH : contains
  BRANCH ||--o{ WAREHOUSE : contains
  COMPANY ||--o{ DEPARTMENT : owns
  ROLE ||--o{ ROLE_PERMISSION : includes
  PERMISSION ||--o{ ROLE_PERMISSION : approved
  USER ||--o{ ROLE_ASSIGNMENT : receives
  TENANT ||--o{ ROLE_ASSIGNMENT : scopes
  ROLE ||--o{ ROLE_ASSIGNMENT : assigned
  ROLE_ASSIGNMENT ||--o{ ACCESS_SCOPE : constrains
  TENANT ||--o{ ACCESS_SCOPE : owns
  TENANT ||--o{ SUPPORT_CASE : subject
  TENANT ||--o{ SUPPORT_ACCESS_GRANT : bounds
  SUPPORT_CASE ||--o{ SUPPORT_ACCESS_GRANT : governs
  TENANT ||--o{ AUDIT_EVENT : scopes
  TENANT ||--o{ OUTBOX_MESSAGE : initiates
  TENANT ||--o{ BACKGROUND_WORK_RECORD : initiates
```

The ERD is logical only. It does not prescribe table names, column lengths,
physical indexes, migrations, or an RLS implementation.

## 35. Persistence Ownership and Constraints

The shared SQL Server database uses module-owned schemas and one operational EF
Core context initially. Identity, Tenant Lifecycle, Organization, Audit, Files,
and Integration mappings are owned by their boundaries; a module cannot update
another module's records directly. `TenantContext` is a non-persisted value
assembled by the application/security pipeline, not a jointly owned table.

The logical persistence acceptance criteria are:

1. Every Tenant-owned record has a non-null `TenantId`.
2. Tenant-aware alternate or unique keys support same-Tenant relationships.
3. Logical composite keys/foreign keys, or an equivalent reviewed relational
   constraint, enforce same-Tenant ownership for Branch -> Company,
   Warehouse -> Branch, Department -> Company, Membership -> Tenant, Role
   Assignment -> Tenant, Access Scope -> Tenant and referenced hierarchy,
   Support Case/Grant -> Tenant, and Tenant-owned file/export/report/work
   metadata.
4. A global query filter is convenience and defense in depth only; it is never
   the sole isolation control.
5. The application write pipeline or `SaveChanges` guard rejects missing
   Tenant ownership, ownership inconsistent with trusted context,
   cross-Tenant relationship changes, and ownership changes after use.
6. Normal Tenant repositories and query handlers require trusted
   `TenantContext`.
7. IgnoreQueryFilters, raw SQL, bulk, Platform maintenance, and migration
   operations are unavailable to ordinary Tenant paths and require named,
   purpose-bound, explicitly authorized, reviewed, audited contracts with
   negative tests.
8. Outbox, background work, files, exports, search metadata, and audit evidence
   retain initiating Tenant and scope.

No physical migration, table length, or RLS deployment is specified. SQL Server
RLS remains optional defense in depth under ADR-016 and is not silently approved.

## 36. Concurrency and Idempotency

Mutable roots use optimistic concurrency and return a safe conflict when a
version is stale. Commands that may be retried carry an idempotency key scoped to
the authenticated actor, operation, and Tenant context. Invitation acceptance,
recovery completion, membership transitions, approvals, support grant changes,
and organization lifecycle commands are single-effect operations.

Outbox messages and background work use durable status, deduplication, retry and
dead-letter evidence while retaining initiating Tenant and scope. A worker
revalidates Tenant, Membership, lifecycle, support grant, organization scope,
and record ownership before executing. Missing or mismatched TenantId, a
cross-Tenant relationship, or an ownership rewrite after use is rejected.
No new rate, volume, retention, or concurrency limits are invented;
MESP-48/MESP-50 own those decisions.

## 37. API Catalogue

The catalogue contains 90 operation intents in six bounded groups. Names are
illustrative contract identifiers, not implemented routes. Every comma-separated
operation in a row inherits the complete metadata in that row; no operation is
unprofiled.

| Group (count) and operations | Owning boundary; actor and authentication/MFA | Tenant context, Permission/scope and lifecycle guards | Concurrency and idempotency | Audit, safe errors and response semantics |
|---|---|---|---|---|
| **Authentication, user and session (15):** `login`, `logout`, `session-status`, `list-sessions`, `revoke-session`, `begin-mfa`, `verify-mfa`, `enroll-mfa`, `revoke-mfa`, `suspend-user`, `reactivate-user`, `offboard-user`, `revoke-affected-user-sessions`, `request-recovery`, `complete-recovery` | Identity; anonymous login/recovery where permitted, authenticated User or authorized administrator otherwise; MFA and operation-bound fresh authentication for privileged actions | No business Tenant is trusted before context resolution; protected calls use server-side session and active Membership; User lifecycle and session state guard every action | Recovery, revocation and lifecycle commands are idempotent; session version/conflict is returned safely | Authentication, MFA, lockout, lifecycle and revocation evidence without secrets; safe existence errors; resource/status response with correlation ID |
| **Invitation (4):** `issue-invitation`, `withdraw-invitation`, `accept-invitation`, `reissue-invitation` | Identity; authorized Tenant administrator for issue/withdraw/reissue; invitee for acceptance; MFA/fresh auth where policy requires | Target Tenant is derived from authorized actor or opaque invitation; active Tenant and invitation lifecycle required; no invitation can cross Tenant | Accept/withdraw/reissue are single-effect; reissue creates a new invitation and never transfers the old one | Invitation and Membership evidence without token; safe invalid/expired response; created/accepted/revoked resource state |
| **Tenant lifecycle and context (19):** `create-tenant-draft`, `start-tenant-provisioning`, `mark-tenant-configuration-required`, `mark-tenant-ready-for-activation`, `activate-tenant`, `enter-tenant-grace-period`, `suspend-tenant`, `reactivate-tenant`, `request-tenant-export`, `begin-tenant-termination`, `terminate-tenant`, `record-tenant-retained-state`, `eligible-memberships`, `select-context`, `switch-context`, `current-context`, `tenant-lifecycle-status`, `tenant-lifecycle-history`, `tenant-export-status` | Tenant Lifecycle; Platform or authorized Tenant administrator as approved by policy; MFA/fresh auth for lifecycle, export and termination decisions | Server-derived Tenant only; transitions enforce the complete lifecycle and Platform safety exception; context calls re-evaluate Membership, status, Organization hierarchy, Permission, session and support grant | Lifecycle transitions and context selection are idempotent; export/termination use idempotency keys and optimistic concurrency | Lifecycle, context, export and denial evidence; no cross-Tenant leakage; response returns guarded state and next allowed action, never a hidden business payload |
| **IAM administration and review (20):** `list-users`, `get-user`, `list-memberships`, `activate-membership`, `suspend-membership`, `revoke-membership`, `list-roles`, `get-role`, `create-role`, `update-role`, `list-permissions`, `list-assignments`, `list-access-review-evidence`, `get-access-review`, `assign-role`, `approve-assignment`, `revoke-assignment`, `revoke-access-scope`, `start-access-review`, `record-access-review` | Identity, with Platform Governance for Permission catalogue reads; Tenant administrator or named approver; MFA/fresh auth for privileged assignment/review | Active Membership, approved Permission and downward Access Scope required; no self-approval; inactive Tenant/organization blocks new authority | Assignment/review and membership changes use idempotency and optimistic concurrency; revocation is repeat-safe | Access and review evidence with approver identity; denied/no-access/conflict errors are safe; response includes effective lifecycle and version |
| **Organization (24):** `list-companies`, `get-company`, `create-company`, `update-company-configuration`, `confirm-fiscal-calendar`, `confirm-operating-time-zone`, `confirm-functional-currency`, `change-company-lifecycle`, `list-branches`, `get-branch`, `create-branch`, `update-draft-branch-parent`, `change-branch-lifecycle`, `list-warehouses`, `get-warehouse`, `create-warehouse`, `update-draft-warehouse-parent`, `change-warehouse-lifecycle`, `list-departments`, `get-department`, `create-department`, `change-department-lifecycle`, `company-hierarchy`, `historical-organization-reference` | Organization; authorized Tenant administrator with downward scope; MFA/fresh auth for lifecycle or configuration changes when policy requires | Trusted TenantContext plus Company/Branch/Warehouse scope; same-Tenant parent, lifecycle, unused-Draft and historical-reference guards apply | Create/update/lifecycle operations use idempotency and optimistic concurrency; parent changes are single-effect | Hierarchy/configuration/lifecycle evidence; safe not-found/no-access/invalid-parent errors; response includes parent, status, configuration confirmation and concurrency version |
| **Support and evidence (8):** `open-support-case`, `approve-support-grant`, `activate-support-grant`, `revoke-support-grant`, `list-support-cases`, `get-support-case`, `list-support-evidence`, `list-audit-evidence` | Support Access and Security/Audit; named support actor, Tenant approver or authorized reviewer; MFA and operation-bound fresh auth | Exact Tenant, case, named actor, exact scope, Tenant approval and expiry; no export authority alone and no other Tenant | Case/grant commands use idempotency and optimistic concurrency; expiry/revocation repeat-safe | Immutable case/grant/audit evidence without secrets or payload leakage; safe denied/expired response; evidence query returns authorized records only |

The operation profiles cover purpose, owning boundary, actor, authentication and
MFA, Tenant-context behavior, Permission/scope, lifecycle, concurrency,
idempotency, audit evidence, safe errors, and response semantics. No endpoint is
implemented by this specification.

## 38. Request, Response, Validation, and Error Contracts

Every request carries correlation and idempotency metadata where applicable;
the server supplies User, Tenant, scope, and authorization facts. Responses use
stable resource identifiers, lifecycle state, version/concurrency metadata, and
safe links to follow-up evidence. Errors use a consistent problem shape with a
correlation ID, machine-readable code, field errors when safe, and no existence
or cross-Tenant leakage.

Validation is layered: transport shape, authentication/session, Tenant context,
permission and resource scope, lifecycle, domain invariant, concurrency, then
durability. A failed layer does not invoke later business effects. Unauthorized,
expired, suspended, revoked, conflict, validation, and dependency failures are
distinguishable to the client only to the extent that doing so is safe.

## 39. Cookie, Antiforgery, and Browser-Security Behavior

The first-party cookie is secure, HTTP-only, appropriately SameSite, scoped to
the application, and never read by Angular code. Unsafe state-changing requests
require an antiforgery mechanism compatible with the cookie model. CORS is
restricted to approved first-party origins. Security headers, clickjacking
protection, content-type enforcement, and safe redirect handling are required.

Technical cookie names, renewal details, key storage, and antiforgery token
transport are downstream ADR-004 decisions. Logout, revocation and expiry clear
usable browser state without returning protected payloads.

## 40. Background Jobs and Asynchronous Tenant Context

Durable SQL-backed work and transactional outbox/inbox records are created in
the same transaction as the initiating change. Each record carries initiating
Tenant, optional Company/Branch/Warehouse scope, actor/purpose, correlation,
idempotency, and lifecycle status. Workers run with no browser authority and
revalidate Tenant lifecycle, Membership/permission policy where relevant,
support expiry, and organization state before touching Tenant data.

Retries are bounded by approved operational policy; dead-letter records remain
auditable. A job cannot fall back to a global query or execute against a different
Tenant because a client payload contains another identifier.

## 41. Audit Evidence

Audit is immutable, append-only evidence. It records actor, action, purpose,
decision, target type/ID, Tenant and scope where applicable, correlation,
timestamp, result, and policy version. It covers authentication, MFA, recovery,
membership, Role/Permission/scope changes, approvals, support access, Tenant and
organization lifecycle, denied cross-Tenant attempts, jobs, exports and files.

Raw passwords, cookies, MFA secrets, recovery tokens, private payloads, and
unnecessary target data are never recorded. Retention, legal hold, purge,
residency, and evidence export policy remain MESP-50 gates.

## 42. Logging, Metrics, and Tracing

OpenTelemetry-compatible telemetry uses correlation IDs across API requests,
application services, SQL calls, outbox dispatch, workers, file adapters, and
security/audit decisions. Logs are structured and redact credentials, tokens and
Tenant data. Metrics cover authentication outcomes, lockouts, context denials,
authorization denials, lifecycle transitions, outbox/work status, and latency;
they do not disclose business payloads or invent MESP-48 limits. Traces preserve
Tenant-safe attributes only and apply access controls to diagnostic views.

## 43. Threat Model

| Threat | Consequence | Primary mitigations |
|---|---|---|
| Forged Tenant ID | Cross-Tenant disclosure/change | Server context, membership, ownership and negative tests |
| Stale browser state after switch | Data submitted in wrong Tenant | Tenant-bound state ownership, no display/reuse/search/cache/export/execute across contexts, safe revalidation |
| Destructive context switch | Valid Tenant A drafts or working state lost | State is not automatically deleted; it remains Tenant A-owned and returns only after current authorization re-evaluation |
| Invalid state restoration | Revoked or terminated state becomes usable after return | Membership, lifecycle, scope, Permission, session and support-grant checks before restore |
| Privilege escalation by role/scope | Unauthorized business action | Platform permissions, downward scope, approval, fresh auth |
| Session theft or stale session | Continued access after change | HTTP-only cookie, server-side UserSession validation on every protected request, affected-session revocation, absolute expiry and inactivity |
| Support grant overreach | Unbounded operator access | Named case, exact scope, Tenant approval, expiry, no export alone |
| Parent lifecycle bypass | New work under inactive unit | Descendant checks and historical-reference rules |
| Async Tenant confusion | Background cross-Tenant write | Context-carried durable work, ownership checks and lifecycle revalidation |
| Sensitive telemetry/audit | Secret or data leakage | Redaction, purpose-bound evidence, controlled access |
| Duplicate/replayed command | Double invitation/approval/change | Idempotency and optimistic concurrency |
| Misconfigured infrastructure | Loss or exposure | MESP-48/MESP-50 gates, deployment review and private storage |

## 44. Security Controls

Required controls include least privilege, default deny, secure cookie and
antiforgery protections, MFA and fresh authentication, lockout, session
revocation, safe errors, input/output validation, tenant-aware relational
integrity, private object storage, secret management outside source, audit
redaction, dependency scanning, security headers, controlled support access,
separation of approval duties, and security review of every critical path.

Production-specific hosting, key management, scanning, retention, restoration,
residency, purge, and supported-volume controls cannot be closed here; see
sections 52-53.

## 45. Localization, Arabic, RTL, and Accessibility

Angular routes and messages use localization resources for Arabic and English.
RTL is a first-class layout mode with logical CSS direction, mirrored navigation
where appropriate, Arabic-friendly form ordering, and no hard-coded LTR-only
assumptions. Dates, times, numbers, currency and calendars follow the Company
configuration and approved Saudi defaults where applicable.

All foundation pages require keyboard access, focus management, semantic labels,
screen-reader announcements for validation and state changes, sufficient
contrast, non-color-only status, and accessible error recovery. Localized safe
errors must preserve security semantics without revealing protected existence.

## 46. Migration and Seed Strategy

No migration is created by this document. Before implementation, the team must
define an idempotent seed for Platform-approved Permissions and the minimum
Platform governance records, with founder approval for any initial data. Tenant,
Company, Branch, Warehouse, Department, User and Membership data require an
explicit migration/cutover plan, ownership mapping, duplicate handling,
validation report, rollback/recovery plan, and audit evidence.

Seed data must not contain Wafra-specific branching in core behavior. Production
retention, purge, legal hold, backup, restoration and residency remain MESP-50
decisions.

## 47. Automated-Test Strategy

The eventual implementation should use focused layers, not a large speculative
suite:

- domain tests for invariants and lifecycle transitions;
- application tests for commands, policy composition, idempotency and safe errors;
- authentication/authorization tests for session, MFA, scope and support rules;
- persistence/integrity tests for Tenant ownership, same-Tenant composite
  relationships, SaveChanges guards and concurrency;
- API contract tests for error/response behavior and correlation;
- architecture tests for `Api -> App -> Contracts` and internal module seams;
- Playwright TypeScript for critical login, MFA, invite, switch, lifecycle and
  denied-access journeys.

Tests must create independent Tenant fixtures, assert negative cross-Tenant
paths, exercise the complete Tenant lifecycle, server-side session revocation,
absolute expiry and renewal, valid-but-preserved working state, restricted
unscoped paths, background revalidation, User suspension/offboarding/reactivation,
and same-Tenant relationships. They must avoid real production secrets. No test
implementation is included in this draft.

## 48. Mandatory Security and Isolation Test Matrix

The corrected matrix has 48 scenarios. Each becomes a targeted automated
assertion across the appropriate domain/application/auth/persistence/API,
architecture, or Playwright layer; no tests are implemented by this draft.

| # | Required assertion |
|---:|---|
| 1 | Cross-Tenant read is denied |
| 2 | Cross-Tenant write is denied |
| 3 | Cross-Tenant search/report/export/file access is denied |
| 4 | Client Tenant ID cannot expand authority |
| 5 | Valid Tenant A working state is not visible in Tenant B |
| 6 | Valid Tenant A working state is not automatically deleted by a context switch |
| 7 | Returning to Tenant A requires current Membership, lifecycle, organization scope, Permission, session and support-grant re-evaluation |
| 8 | Separate concurrent Tenant contexts, caches and workspaces remain isolated |
| 9 | Revoked Membership is denied |
| 10 | Role/scope revocation invalidates affected sessions |
| 11 | Suspended Tenant denies ordinary interactive work |
| 12 | Suspended Tenant denies ordinary asynchronous business work |
| 13 | Draft, Provisioning and Configuration Required guards reject premature ordinary work |
| 14 | Ready for Activation requires all approved activation prerequisites |
| 15 | Activation is allowed only from the approved Ready for Activation state |
| 16 | Grace Period applies its approved guard and does not invent a duration |
| 17 | Reactivation re-evaluates all affected access and does not auto-restore it |
| 18 | Export Requested is scoped, authorized and evidenced |
| 19 | Termination Pending blocks ordinary work until approved guards pass |
| 20 | Terminated revokes ordinary access while preserving evidence |
| 21 | Retained state remains subject to MESP-50 and no purge executes |
| 22 | Parent Tenant/unit suspension blocks descendants |
| 23 | Offboarded User is denied |
| 24 | User suspension revokes affected sessions |
| 25 | User reactivation does not automatically restore prior privileges |
| 26 | Expired support grant is denied |
| 27 | Support grant cannot reach another Tenant |
| 28 | Support grant alone cannot export |
| 29 | Privileged operation requires MFA and operation-bound fresh authentication |
| 30 | Five-attempt lockout lasts 15 minutes |
| 31 | Ordinary session absolute expiry cannot exceed 8 hours |
| 32 | Cookie renewal cannot extend the original absolute maximum |
| 33 | Inactivity at 30 minutes expires the session |
| 34 | Every protected request validates server-side UserSession revocation |
| 35 | Password reset invalidates affected sessions |
| 36 | Missing or invalid antiforgery is denied |
| 37 | Inactive or closed units reject new work |
| 38 | Authorized historical reference remains readable |
| 39 | Used parent ownership cannot be rewritten |
| 40 | Same-Tenant composite relationships enforce Branch -> Company, Warehouse -> Branch and Department -> Company |
| 41 | Tenant Membership, Role Assignment and Support Case/Grant cannot reference another Tenant |
| 42 | Missing TenantId is rejected by the write pipeline |
| 43 | Mismatched TenantId versus trusted context is rejected |
| 44 | Restricted IgnoreQueryFilters/raw SQL/bulk/maintenance paths are unavailable to ordinary Tenant calls |
| 45 | Background/outbox work revalidates initiating Tenant, scope, lifecycle and ownership |
| 46 | Denied cross-Tenant audit/telemetry records do not leak target data |
| 47 | Tenant-aware alternate/unique keys and architecture dependency direction remain valid |
| 48 | MESP-48/MESP-50 gates cannot be bypassed and no production purge is authorized |

## 49. Traceability Matrix

| Source baseline | Covered sections and evidence |
|---|---|
| MESP-28 IAM v0.2 | 9-10, 13-18, 20-30, 37-48; identity, session record, revocation, MFA, Roles, support, user lifecycle and security controls |
| MESP-29 Multi-Tenancy v0.2 | 4-5, 7-8, 13-15, 17-19, 24-26, 30, 33-36, 40-48, 52; complete Tenant lifecycle, context-bound working state, isolation, suspension, termination and gates |
| MESP-30 Organization v0.2 | 9-10, 13-15, 17-19, 28, 31-36, 37-38, 48; hierarchy, configuration, controlled Draft parent changes and lifecycle |
| Architecture baseline | 6-8, 26, 33-47, 51-53; modular seam, SQL, cookies, work, files and telemetry |
| PRD/glossary/Decisions | 2-5, 9, 31, 45, 52-55; Release 1, B2B, Wafra, Arabic/RTL and gates |
| MESP-86 Jira scope | 1-5, 16-18, 37, 47-55; design-only boundary, correction cycle, catalogues, review control and no-implementation gate |

Coverage is 100% for the three approved BRDs, approved technology direction,
and the required foundation safety controls. Items intentionally deferred are
named with owners and gates rather than guessed.

## 50. Implementation Slicing Recommendation

This draft recommends an order, not a start authorization. The smallest safe
first implementation slice is the existing architecture seam plus tenant-aware
contracts; it must remain gated by this document's approval and MESP-48/MESP-50.

| Order | Existing Enabler | Proposed refinement before Ready | Dependency |
|---:|---|---|---|
| 1 | MESP-58 Shared SQL persistence and Tenant-context guard | Explicit context object, ownership constraints, no unscoped query path, negative tests | Approved foundation specification; MESP-48 evidence before scale claims |
| 2 | MESP-59 Authentication and authorization seam | Identity cookie/session/revocation, policy/resource checks, MFA hooks, scope composition | MESP-58 context contract; technical ADR-004 details |
| 3 | MESP-60 REST/OpenAPI/errors/correlation/idempotency | Problem contract, safe errors, correlation and retry semantics | MESP-58/59 contracts |
| 4 | MESP-62 Immutable audit/OpenTelemetry | Redaction, correlation, denial evidence and audit ownership | MESP-58/59/60 decisions |
| 5 | MESP-63 Angular shell/components/RTL | Context indicator, safe states, localization/accessibility shell | MESP-59/60 contracts |
| 6 | MESP-64 local/critical-flow test harness | Isolated Tenant fixtures, xUnit/Playwright harness and 48-case matrix | All preceding seams |
| 7 | MESP-61 durable work/files/notification adapters | Tenant-carrying work, private storage interface, no provider decision assumed | MESP-58/60; MESP-50 gates |

MESP-58 through MESP-64 remain unchanged in Jira by this task. No Enabler is
marked Ready, placed in a Sprint, or started.

## 51. Existing Jira Enabler Impact

The refinement table above is the proposed description/acceptance/dependency
impact for MESP-58 through MESP-64. It preserves the existing issue ownership
and scope. Before implementation, each issue needs a source link to this
specification, explicit Tenant-isolation acceptance, and a Definition of Ready
review. The recommended order is sequential because context and authorization
contracts are blocking for all later slices.

## 52. MESP-48 and MESP-50 Gates

MESP-48 remains the supported-volume and performance evidence gate. This draft
defines hooks for telemetry, isolation, and test fixtures but invents no record
counts, rates, payload limits, concurrency targets, storage limits, or capacity
claims.

MESP-50 remains the retention, privacy, legal-hold, purge, residency, backup,
restoration and evidence-governance gate. This draft defines immutable audit and
deactivation hooks but no retention duration, purge schedule, physical purge
execution, legal policy, region, RPO/RTO, or provider commitment.

## 53. Open Technical Decisions

Eight genuine technical decisions remain. Each records the decision, safe
Release 1 recommendation, blocking status, owner, latest resolution point, and
whether specialist review is required. None changes an approved business value.

| ID / decision | Safe Release 1 recommendation | Blocking status | Owner / latest resolution point | Specialist review |
|---|---|---|---|---|
| TD-01 RLS adoption | Keep application and relational ownership guards mandatory; do not depend on RLS. Adopt RLS only through ADR-016 with measured policy behavior. | Blocking before production if selected; not blocking this draft | Security/Architecture; before production | Security and database specialist required |
| TD-02 Hosting, region, topology | Use Docker/local and a documented single-deployable topology for development; do not imply a production region or capacity. | Blocking before production | Product Owner/Operations; before production and MESP-48/MESP-50 closure | Architecture, operations and privacy review required |
| TD-03 Object storage provider, scanning, signed downloads | Keep a private adapter with deny-by-default access; defer provider, scanning and signed-download policy. | Blocking before file implementation/production | Architecture/Security; before MESP-61 Ready and production | Security/privacy specialist required |
| TD-04 Session technical mechanism | Use server-side UserSession, opaque protected-cookie identifier, per-request revocation validation, eight-hour absolute maximum, thirty-minute inactivity and non-extending renewal. | Blocking before MESP-59 Ready | Security/Architecture; before MESP-59 Ready | Security specialist required |
| TD-05 External partner authentication | Use first-party cookie authentication only; defer partner authentication until an approved integration requires it. | Non-blocking until first partner integration | Product/Architecture; before first partner integration | Security review required when opened |
| TD-06 Module ownership reconciliation | Identity owns Access Scope grants; Tenant Lifecycle owns Tenant identity/status; Organization owns hierarchy and lifecycle; consuming modules own later effects through contracts. | Non-blocking for this foundation; blocking affected Enabler readiness if a boundary changes | Architecture; before affected Enabler Ready | Solution/domain architect review required |
| TD-07 Arabic search/collation | Provide localized display and deterministic comparison hooks; do not promise search behavior until SQL/search validation is complete. | Blocking before search implementation | Architecture/Product; before search implementation | Database/localization specialist required |
| TD-08 Fresh-auth assurance reuse | Bind fresh authentication to the specific protected operation or challenge completion; do not introduce a reusable assurance duration. | Blocking before MESP-59 Ready if reuse is proposed | Security/Architecture; before MESP-59 Ready | Security specialist required |

## 54. Definition of Ready

The specification is implementation-ready only after critical architecture and
security review confirms: all three BRDs remain unchanged; Tenant ownership and
negative paths are testable; session/MFA technical decisions preserve approved
values; API and UI contracts are safe; persistence constraints are reviewable;
MESP-48/MESP-50 gates have explicit owners; and the relevant Enabler description,
acceptance criteria, dependencies, and scope are refined. Ready does not itself
start work: a separate controlled authorization, Sprint decision, and Jira
transition are required.

## 55. Review and Approval

**Current state:** Draft — Not Approved for Implementation.
**Required reviewers:** Product Owner, Solution Architect, Security Architect,
and a critical Tenant-isolation reviewer.  
**Approval evidence:** Jira MESP-86 comment referencing the reviewed commit and
document version, followed by explicit founder approval.  
**Next action:** Critical architecture and security review of this file.  
**Stop condition:** Do not create code, migrations, APIs, Angular pages, tests,
Sprints, implementation Stories, MESP-31 work, MESP-58/MESP-59 work, Retail POS,
or Wafra-specific behavior from this draft.
