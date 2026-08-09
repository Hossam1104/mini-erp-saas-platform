# ADR-002 — Backend project structure and module enforcement

| Field | Decision |
|---|---|
| Status | Approved for Release 1 module implementation; production/provider validation remains separately gated |
| Date | 9 August 2026 |
| Owners | Hossam / Solution Architecture |
| Related Jira | MESP-100, MESP-99, MESP-48, MESP-50 |
| Supersedes | The ADR-002 timing placeholder in `docs/Decisions.md` and the three-project wording in the Technology Architecture Baseline |
| Superseded by | None |

> **Current MESP-102 implementation evidence - 9 August 2026.** The bounded
> Product identity slice confirmed this four-project topology in source: public
> Product contracts remain in `MiniErp.Contracts`, Product application behavior
> and policy remain in `MiniErp.App`, Product EF/module persistence remains in
> `MiniErp.Infrastructure`, and API endpoint composition remains in
> `MiniErp.Api`. No fifth project, direct cross-module persistence path, or
> alternate composition route was introduced. Provider, migration, and
> production validation remain gated by ADR-006 and the open MESP-48/MESP-49/
> MESP-50 controls.

## Context

The approved Release 1 architecture is a modular monolith. The repository has
four existing production projects, not three:

- `MiniErp.Contracts`
- `MiniErp.App`
- `MiniErp.Infrastructure`
- `MiniErp.Api`

The original baseline described a three-project starting point and did not
define how the already-existing provider/persistence project participates in
the composition root. That omission is a readiness blocker before the first
data-bearing Master Data slice. The decision must also preserve ADR-006's
module-owned persistence and shared SQL Server direction without creating a
project per table, a fifth production project, or a microservice boundary.

## Decision

Preserve one modular-monolith deployment and enforce the following production
project responsibilities and dependency direction:

```text
MiniErp.Api -> MiniErp.Infrastructure -> MiniErp.App -> MiniErp.Contracts
     |                 |                    |
     +---------------> MiniErp.App         +--> shared public contracts only
     +---------------> MiniErp.Contracts
```

The direct `MiniErp.Api -> MiniErp.Infrastructure` reference is the approved
composition-root path. The API host may also reference `MiniErp.App` and
`MiniErp.Contracts` for its existing host and public-contract composition. No
reverse dependency is permitted and the graph must remain acyclic.

### `MiniErp.Contracts`

`MiniErp.Contracts` owns stable public contracts, module descriptors, public
request/response/value contracts, and cross-module event contracts. It has no
project dependency on `MiniErp.App`, `MiniErp.Infrastructure`, or
`MiniErp.Api`. It must not contain EF Core or provider-specific implementation.

### `MiniErp.App`

`MiniErp.App` owns application and module behavior, use-case orchestration,
server-authoritative authorization seams, and module-internal implementation.
It depends on `MiniErp.Contracts` only. It must not depend on EF Core,
`MiniErp.Infrastructure`, or `MiniErp.Api`. Application module internals remain
internal and are exposed through explicit public contracts/composition seams.

### `MiniErp.Infrastructure`

`MiniErp.Infrastructure` is the existing provider/persistence implementation
project. It may depend on `MiniErp.App` and `MiniErp.Contracts`; it must not
depend on `MiniErp.Api`. EF Core, SQL Server provider code, persistence
sessions, mappings, provider adapters, and migrations belong here.

Business-module ownership remains explicit inside this shared project. Future
Master Data persistence will use a Master Data-owned structure such as:

```text
MiniErp.Infrastructure/
  Persistence/
    Modules/
      MasterData/
      MasterDataDbContext.cs
      Configurations/
      Migrations/
```

The names above describe the ownership boundary; MESP-100 does not create the
context, entities, tables, mappings, or migrations. When a data-bearing slice
is implemented, the Master Data persistence boundary must ensure that:

1. the Master Data context, mappings, schema, and migrations are owned by the
   Master Data module;
2. another module cannot add a Master Data `DbSet`, repository, table mapping,
   or migration operation;
3. cross-module reads use approved contracts or application ports rather than
   direct table access;
4. Tenant ownership, query filters, stored-owner verification, concurrency,
   and transaction rules remain enforced at the approved persistence boundary;
5. architecture tests inspect the source/project graph and reject a cross-
   module persistence shortcut.

### `MiniErp.Api`

`MiniErp.Api` remains the host and composition root. It directly references
`MiniErp.Infrastructure` so it can call provider/module registration methods
when a provider-backed composition is due. It may compose application and
contract seams directly, but it must not own business persistence or reach
Infrastructure through a new intermediary project. MESP-100 adds the explicit
project reference and architecture enforcement; it does not select a provider,
open a production database, or compose Category/UOM persistence.

## Relationship to ADR-006

ADR-002 resolves project ownership, compile-time dependency direction, and the
host's legal route to Infrastructure. ADR-006 remains authoritative for
behavior and persistence ownership:

- Release 1 uses one shared SQL Server database shape.
- Tenant ownership and stored-owner checks are mandatory.
- Each module owns its EF model, mappings, repositories, schema namespace, and
  migrations inside the approved Infrastructure project structure.
- Direct cross-module persistence access is prohibited.
- Production migrations, provider selection, and production validation are
  reviewed separately.

ADR-002 does not replace or broaden ADR-006, and it does not turn the local
Foundation provider/test seam into a production provider decision.

## Alternatives considered

1. **Keep the three-project graph (`Api -> App -> Contracts`)** — rejected
   because it omits the already-existing Infrastructure project and leaves the
   composition path ambiguous.
2. **Put EF Core and provider code in `MiniErp.App`** — rejected because it
   violates the application/provider boundary, weakens module ownership, and
   makes provider-specific dependencies available to business behavior.
3. **Create one Infrastructure project per module** — rejected for Release 1;
   explicit module folders/namespaces and architecture tests provide ownership
   without a project-per-table or project-per-module explosion. A later split
   would require a new decision and measured need.
4. **Compose Infrastructure indirectly through App or a new adapter project**
   — rejected because the host would lose an unambiguous provider composition
   path and a fifth production project would be introduced.
5. **Move to microservices or separate databases** — rejected because the
   approved Release 1 posture is a modular monolith with shared SQL Server and
   application-layer Tenant isolation.

## Enforcement and evidence

The repository must keep focused architecture tests that prove:

- Contracts do not reference App, Infrastructure, or Api;
- App references Contracts and not EF Core, Infrastructure, or Api;
- Infrastructure references App/Contracts and not Api;
- Api's project references include Infrastructure as the composition-root
  path, alongside its existing App/Contracts host references;
- the four-project graph has no cycle;
- public Infrastructure surfaces do not expose unscoped EF shapes or Tenant
  override/bypass parameters.

MESP-100's focused `ModuleBoundaryTests` enforce the project-reference graph,
and the existing compiled/source architecture tests continue to enforce the
remaining rules. These tests are structural guardrails; they do not authorize
any domain persistence by themselves.

## Consequences

Positive consequences include a truthful four-project architecture, a clear
host-to-provider composition route, preserved Contracts/App boundaries, and an
explicit legal home for future module-owned EF Core work. Master Data can add
Category/UOM persistence inside Infrastructure without granting another module
direct table access or requiring a fifth production project.

The trade-off is that Infrastructure becomes a shared provider assembly whose
internal module ownership must be maintained with folders, namespaces,
internal visibility, and architecture tests. A future independent project
split may be justified by measured boundary pressure, but it is not implied by
this ADR.

## Scope and deferred decisions

This ADR does not create Category/UOM persistence or decide any Category/UOM
business behavior. It does not decide production hosting topology, SQL Server
vendor/host, RLS, retention, privacy, residency, backup/restore targets, legal
hold, purge, production credentials, or any other MESP-48/MESP-49/MESP-50 gate.
It introduces no Retail POS or Wafra-specific core behavior and no later Master
Data slice.

## Approval and review

This decision is published as the MESP-100 readiness correction on 9 August
2026 under Hossam's standing Owner approval. It is the required project/
composition decision for MESP-99 Category/UOM implementation. Future changes
to project direction, module persistence ownership, or production provider
selection require a new reviewed ADR or an explicit superseding decision.
