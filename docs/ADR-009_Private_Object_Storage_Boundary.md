# ADR-009 — Private object-storage adapter and access boundary

| Field | Decision |
|---|---|
| Status | Contract baseline; production storage decision deferred |
| Date | 4 August 2026 |
| Owners | Solution Architecture / Security Engineering |
| Related Jira | MESP-61, MESP-64, MESP-38, MESP-39, MESP-50 |
| Supersedes | None |

## Context

Release 1 requires private files without allowing an object key, URL or client
Tenant value to expand authority. The Foundation needs a provider-neutral
boundary that can be tested before a production object-storage vendor, region,
retention or scanning policy is approved.

## Decision

1. `IPrivateObjectStorage` accepts a trusted TenantContext and validated
   organization scope for every operation. Object identity is opaque and is
   never a public URL or an anonymous access token.
2. Metadata records immutable Tenant/scope ownership, safe original filename,
   content type, length, SHA-256, created/optional expiry metadata and an
   optimistic concurrency version. Store/read checksum validation is mandatory
   where the adapter supports bytes.
3. Cross-Tenant reads and overwrites fail closed without returning foreign
   metadata or content. Expiry changes access disposition only; it never causes
   physical purge. A future logical-disposition operation must preserve the
   same Tenant and concurrency checks.
4. MESP-61 implements a bounded in-memory adapter for tests/development only.
   It is not evidence of a production provider, signed-download mechanism,
   malware scanner, region, retention or purge policy.
5. Production object storage, private networking, key management, scanning,
   signed-download duration and lifecycle policy require a later decision and
   MESP-50 review.

## Alternatives considered

- Public buckets/URLs were rejected because private-by-default access is an
  approved security requirement.
- Passing a caller-supplied object key or Tenant ID to the provider was
  rejected because it would make storage authority client-controlled.
- A file byte column in business tables was rejected for the Foundation because
  it couples module transactions to an unapproved storage/retention policy.

## Consequences and guardrails

- File metadata is Tenant-owned and must be audited with safe allow-listed
  access outcomes; foreign target identifiers are not exposed in denied errors.
- The adapter does not physically delete data or claim expiration cleanup.
- Production provider, region, residency, encryption-key management, backup,
  retention, legal hold, purge and scanning remain separately approved.

## Gates

MESP-48 owns supported-volume/performance and recovery evidence for file
operations. MESP-50 owns privacy, retention, residency, legal hold, purge,
backup and restoration. ADR-009 does not supersede ADR-016 or any production
security decision.
