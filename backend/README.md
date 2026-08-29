# Mini ERP backend

## Current boundary

MESP-136 is accepted, merged, and Jira-closed. The backend currently provides
the reusable Tenant/Company/Branch ERP spine through Procurement, Inventory,
Finance, Tenant-aware entry, and B2B Sales quotations/orders. MESP-137 and
later capabilities are inactive; no reservation fulfillment, Delivery, Sales
Invoice, Customer Return/Credit Note, generic Reporting catalogue, external
provider, statutory, or Wafra-specific core behavior is added here.

## Project graph

```text
MiniErp.Api -> MiniErp.App -> MiniErp.Contracts
MiniErp.Api -> MiniErp.Infrastructure -> MiniErp.App / MiniErp.Contracts
MiniErp.ArchitectureTests -> all four production projects
MiniErp.DevelopmentDataCutover -> separate local data tool
```

`MiniErp.Api` owns HTTP endpoint composition, authentication/authorization
composition, antiforgery, OpenAPI, and the process root. `MiniErp.App` owns
module services, validation, authorization seams, and persistence interfaces.
`MiniErp.Contracts` owns public request/response and shared contracts.
`MiniErp.Infrastructure` owns module persistence implementations, EF contexts,
migrations, and SQL/SQLite provider configuration.

## Module boundaries

Endpoint files register cohesive module groups from `Program.cs`. Application
modules include Platform/Tenant entry, Master Data, Business Parties,
Procurement, Inventory, Finance, and Sales. Persistence is module-owned and
keeps Tenant ownership and Company/Branch scope checks at the server boundary.

The repository has six module-owned EF contexts plus the tenancy context:

| Context | Schema/ownership |
| --- | --- |
| `TenantPersistenceDbContext` | `tenancy`, shared Tenant-owned record base |
| `MasterDataDbContext` | `masterdata`, master data reference data |
| `BusinessPartiesDbContext` | `businessparties`, suppliers and customers |
| `ProcurementDbContext` | `procurement`, sourcing through invoice matching |
| `InventoryDbContext` | `inventory`, warehouse, ledger and valuation |
| `FinanceDbContext` | `finance`, journals, subledgers, tax/FX and reports |
| `SalesDbContext` | `sales`, quotations, orders, approvals and credit evidence |

Each context has its own additive migration history. Do not delete or squash
applied migrations or edit snapshots for cosmetic cleanup.

## API contract discipline

Every public operation is catalogued with route, permission, scope,
antiforgery, audit, and unsafe-effect metadata; mapped by the real endpoint;
represented in generated OpenAPI with a stable operation ID; and covered by
architecture/contract tests. Scalar renders the generated document for local
inspection and is not a second contract.

## Commands

From the repository root:

```powershell
dotnet build .\backend\MiniErp.sln --configuration Release
.\scripts\Test-MiniErpBackend.ps1 -NoBuild
```

Focused backend tests can be run against the architecture test project:

```powershell
dotnet test .\backend\tests\MiniErp.ArchitectureTests\MiniErp.ArchitectureTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~SalesTests'
```

The full runner provisions and cleans only a disposable `MiniErpFoundation_*`
LocalDB target for SQL safety. Never point it at the persistent `MESP`
database. See [Run.md](../Run.md) for provider selection, migrations,
cutover, and runtime startup.

## Development data cutover

`backend/tools/MiniErp.DevelopmentDataCutover` is a separate inventory-first
local SQLite-to-SQL Server tool. It preserves source hashes and IDs, writes
recoverable local backups, and is not a production migration or deployment
workflow. Keep it because the runtime guide and local data workflow reference
it.
