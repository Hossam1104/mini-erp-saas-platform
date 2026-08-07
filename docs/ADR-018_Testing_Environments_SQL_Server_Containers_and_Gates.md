# ADR-018 — Testing environments, SQL Server harness, and production-like gates

| Field | Decision |
|---|---|
| Status | Approved foundation test strategy; production equivalence remains deferred |
| Date | 4 August 2026; reconciled 7 August 2026 (MESP-94 validation-tooling correction) |
| Owners | Solution Architecture / Test Engineering |
| Related Jira | MESP-64, MESP-48, MESP-50, MESP-94 |
| Supersedes | None |

## Context

The Foundation persistence seam is provider-neutral in its public contracts,
but SQL Server-specific behavior still needs evidence before later ERP modules
add physical schemas and migrations. Docker is not available on the current
developer machine. SQL Server LocalDB is installed and supports the machine,
so the one-developer baseline needs a deterministic, disposable option without
ever connecting to a production or shared database.

## Decision

1. MESP-64 uses SQL Server LocalDB instance `MSSQLLocalDB` for the current local
   harness. A run receives a unique database name with the
   `MiniErpFoundation_` prefix and uses Windows integrated authentication.
   No password, token, connection string, or production endpoint is committed.
2. The test fixture creates the run database from `master`, creates only the
   mapped foundation tables and test-only probe tables, and drops the database
   in fixture cleanup. A connection is accepted only when it targets the
   LocalDB instance and the required disposable name prefix; an unset or
   unsafe connection fails closed.
3. The harness separates provider-specific evidence (SQL Server schema,
   unique-index composition, `rowversion`, collation/Unicode round-trip and
   transaction behavior) from provider-neutral contract evidence (Tenant
   context, ownership, authorization path and durable-work contracts).
4. The repository command `scripts/validate-foundation.ps1` is the single
   canonical Foundation validation entry point (MESP-94 M-14). It discovers
   `SqlLocalDB.exe`/`sqlcmd.exe` dynamically — PATH first, then a
   version-agnostic scan of installed SQL Server Tools directories under
   Program Files; no SQL Server release/version is ever hard-coded (MESP-94
   M-15). It starts LocalDB, removes any stale disposable database left by an
   interrupted prior run, supplies a fresh disposable connection string to the
   test process, runs backend restore/build/the full backend regression
   (including the targeted SQL Server suite and the safety-catalogue
   validator), the Angular unit tests and production build, the Playwright
   Foundation journeys, `npm audit`, and `git diff --check`. The fixture owns
   per-run database cleanup even when a test fails; the script additionally
   proves, in a `finally` block that always runs, that zero
   `MiniErpFoundation_*` databases remain on the instance (MESP-94 M-6), and
   clears the environment variable. Every step fails the command closed.
5. Docker/Testcontainers remains a CI-compatible option to be introduced only
   through a separately approved change. This ADR does not claim that LocalDB
   is production-equivalent, nor does it select a production SQL topology.

## Fixture lifecycle and limitations

- The test fixture creates an isolated database per execution and uses unique
  Tenant, record, work and event identifiers. Test-only durable-work probes are
  created under the `test` schema and are never production models or migrations.
- The fixture verifies rollback, duplicate `(TenantId, EventId)` handling,
  single-owner lease claims, query-filter evaluation per context, stored-owner
  update/delete denial, same-Tenant relationship guards, index shape,
  concurrency and Unicode storage.
- LocalDB is a developer validation provider. It does not prove production
  sizing, throughput, failover, backup/restore, high availability, network
  isolation, deployment identity, regional residency or operational alerting.
- A missing LocalDB instance, unsafe connection, failed assertion or failed
  cleanup is a failed validation, never a warning or a skipped test.

## Provider-neutral versus provider-specific evidence

Provider-neutral architecture and SQLite tests continue to prove the public
Tenant-bound contracts and safe denial shape. SQL Server tests prove only the
semantics that require SQL Server. Neither provider grants an ordinary caller
an `IgnoreQueryFilters`, raw SQL, bulk or maintenance escape hatch.

## CI and one-developer repeatability

The command is intentionally usable by one developer on a Windows machine with
LocalDB. CI may later run the same test class with an isolated SQL Server
container/Testcontainers adapter, but that infrastructure is deferred and is
not silently substituted in this release. The test report records the provider,
database name prefix, commit and assertion-catalogue version without publishing
Tenant target data or credentials.

## Production gates and non-decisions

MESP-48 remains the owner of supported volume, throughput, queue depth, lease,
recovery and capacity thresholds. MESP-50 remains the owner of provider/vendor,
retention, residency, privacy, legal hold, purge, backup and restoration
decisions. This ADR authorizes no production migration, purge, retention
execution, performance claim, provider selection or database-per-Tenant shape.

## Evidence produced by MESP-64

`docs/96_Foundation_Release1_Safety_Validation.md` records the exact 75
assertion catalogue, the applicable local evidence, safe Not Applicable
explanations for later-domain behavior, and the MESP-48/MESP-50 gates. The
report is a foundation checkpoint, not a production-readiness approval.
