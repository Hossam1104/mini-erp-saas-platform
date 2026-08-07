# ADR-009 — Private object-storage adapter and access boundary

| Field | Decision |
|---|---|
| Status | Contract baseline; MESP-93 access-outcome and lifecycle hardening implemented; production storage decision deferred |
| Date | 4 August 2026; reconciled 7 August 2026 |
| Owners | Solution Architecture / Security Engineering |
| Related Jira | MESP-61, MESP-64, MESP-93, MESP-38, MESP-39, MESP-50 |
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

## MESP-93 hardening (7 August 2026, implementation pending review)

Point 3's "fail closed without returning foreign metadata or content" is now
enforced as external indistinguishability, not merely non-disclosure: a
foreign-Tenant object and a genuinely missing object return the identical
`PrivateFileAccessOutcome.NotFound` to the caller (M-1). The foreign-vs-missing
distinction is preserved only in the adapter's internal safe audit-evidence
list, never in the caller-visible result. Point 3's overwrite guarantee is
extended to any prohibited lifecycle state, not only a Tenant mismatch: an
expired object or one whose live-recomputed checksum no longer matches its
recorded hash also fails closed on `OverwriteAsync`, so an invalid existing
object cannot be silently resurrected by an ordinary overwrite (M-4). Original
filename validation now normalizes to Unicode Normalization Form C and
rejects, rather than tolerantly truncates, any value containing a path
separator, traversal sequence, or Unicode bidirectional/embedding/isolate/
mark/zero-width formatting character (M-5); valid Arabic and mixed
Arabic/English filenames remain fully supported. This remains the MESP-61
bounded in-memory adapter; no production object-storage provider, signed
URL, public download or malware scanner is introduced by this correction.

A focused re-review of the above correction (M93-02) found that an object
already recorded as `ChecksumFailed` or `Disposed` was misleadingly reported
as `Expired`, since the original check treated every non-`Available`
disposition alike. `PrivateFileAccessOutcome` now has a dedicated `Disposed`
classification, and both `ReadAsync` and `OverwriteAsync` report a
previously recorded `ChecksumFailed` or `Disposed` disposition with its
exact classification through a single shared evaluation path. Separately,
the filename policy was found to over-reject: an embedded `".."` substring
in an otherwise-safe filename (e.g. `report..final.txt`) is no longer
rejected now that path separators alone are sufficient to block real
traversal (L93-01), and U+200C/U+200D (ZWNJ/ZWJ) -- which have legitimate
Arabic-script shaping uses -- were removed from the rejected code-point list,
which was never intended to cover them.
