# Mini ERP backend foundation

This directory contains the first MESP-57 backend seam. It is intentionally a
small Modular Monolith foundation and does not implement platform business
workflows, persistence, authentication, tenant selection, or any downstream
ERP module.

## Prerequisites

- .NET SDK 10.0.302 (the repository pins this SDK in `global.json`)

## Commands

Run these commands from `backend/`:

```powershell
dotnet restore .\MiniErp.sln
dotnet build .\MiniErp.sln --configuration Release
dotnet test .\tests\MiniErp.ArchitectureTests\MiniErp.ArchitectureTests.csproj --configuration Release --no-restore
dotnet run --project .\src\MiniErp.Api\MiniErp.Api.csproj --configuration Release --no-build
```

While the API is running, verify the composition evidence:

```powershell
Invoke-RestMethod http://localhost:5000/health
Invoke-RestMethod http://localhost:5000/api/v1/module-registration
```

The port may be selected by the local ASP.NET Core launch environment; use the
URL printed by `dotnet run` when it differs from port 5000.

## Three-project direction

- `MiniErp.Contracts` contains only stable public module contracts and module
  identity records. It has no dependency on application internals.
- `MiniErp.App` contains the composition entry point and the internal Platform
  Administration implementation. The internal implementation is not public.
- `MiniErp.Api` is the host. It registers the Platform module through
  `PlatformModuleRegistration` and consumes only the public contract.

The permitted dependency direction is `MiniErp.Api -> MiniErp.App ->
MiniErp.Contracts`. `MiniErp.Api` also references `MiniErp.Contracts` for its
composition type. Contracts never reference the host or application internals;
the application never references the host. The architecture tests enforce the
forbidden directions and the absence of a cycle.
