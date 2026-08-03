# ADR-004 — Identity cookie, server session, antiforgery, context resolution and authentication-assurance policy

- **Status:** Accepted for Foundation Release 1 implementation
- **Date:** 2026-08-04
- **Owner:** Product and Architecture
- **Related Jira:** MESP-28, MESP-38, MESP-55, MESP-59, MESP-89

## Decision

Release 1 uses a first-party ASP.NET Core cookie only as an encrypted locator
for a server-side `UserSession`. The approved cookie is
`__Host-MiniErp.Auth`; it is `HttpOnly`, `Secure`, scoped to `/`, has no
`Domain`, and uses `SameSite=Lax` for the first-party Angular deployment
model. The session has an eight-hour absolute lifetime, a thirty-minute
inactivity timeout, and no sliding extension beyond the absolute expiry.
Every protected request revalidates the opaque session against the server-side
user, session, membership, support-grant and platform state. Cookie presence,
claims, headers, routes and request bodies are never authorization authority.
No authentication token is placed in browser localStorage or sessionStorage.

The host registers ASP.NET Core cookie authentication, authorization and
antiforgery services. Unsafe cookie-authenticated methods validate a framework-
generated request token against its framework-managed antiforgery cookie and
the configured request header. Static or predictable tokens are not accepted;
GET, HEAD and OPTIONS do not mutate state or require antiforgery validation.

The server owns exactly one authorization path for a request:

1. `OrdinaryMembership`;
2. `SupportGrant`; or
3. `PlatformGovernanceContext`, for Platform operations only.

Platform governance is not a Tenant path. The server lists eligible contexts,
stores the selected context in trusted session state, and confirms every
context switch against current eligibility and an optimistic version. A
client-supplied Tenant, path, role, permission, scope or grant cannot create or
change authority. Cross-Tenant, stale and mixed-path selections fail closed.
MFA and fresh-authentication evidence remain server-validated; the unavailable
production assurance provider fails closed whenever that assurance is required.

Successful protected writes append immutable safe evidence through
`FoundationAuditCoordinator` before applying their effect. Evidence append
failure prevents the effect. If an effect fails after an allowed evidence
record, the original record is preserved and a linked neutral `EffectFailed`
outcome is appended without exception or provider details. No Tenant-,
SupportGrant- or Platform-authorized write may bypass the coordinator. A
session-only sign-out with no selected authorization path is a lifecycle
revocation (not a Tenant or Platform business effect); it still requires
antiforgery and is idempotent, while a sign-out from a selected path is
evidenced before revocation.

## Scope and provider levels

This ADR covers the Foundation HTTP host integration and its bounded local,
in-memory development seams. It does not claim production readiness for the
provider layer. Production deployment must separately select and validate:

- durable Identity persistence;
- an external identity provider, if required;
- a production MFA/fresh-authentication provider;
- production email/SMS delivery;
- distributed session storage;
- durable distributed idempotency;
- a durable audit store and exporter;
- SQL Server migrations and deployment configuration.

The local store and assurance source are intentionally bounded test/development
implementations. They are not a production persistence, key-management,
retention, purge, residency, legal-hold, backup or restore decision.

## Consequences

The API host can safely establish the minimum session and context contract
needed by the Angular shell while preserving the MESP-59 server authority. The
implementation remains a modular monolith and does not introduce microservices,
an external IdP, a production database or a deployment topology. MESP-63
frontend implementation remains gated on review and approval of this host
integration.

## Alternatives rejected

- bearer tokens in local/session storage;
- Tenant or authorization claims treated as sufficient authority;
- client-selected Tenant/path headers;
- static antiforgery tokens;
- a global raw idempotency-key namespace;
- applying a protected effect before evidence is appended;
- treating Platform governance as a Tenant authorization path.

## Review and supersession

This ADR is the Foundation Release 1 implementation baseline. Production
provider, deployment, retention and assurance decisions require separate
approved ADRs and validation before production. It may only be superseded by a
new approved ADR with explicit security, tenant-isolation and audit evidence.
