# MiniERP Local Development & Integrated Runtime Guide

Run these commands in two separate PowerShell terminals to launch the backend and frontend development environments.

## Terminal 1: Backend (`MiniErp.Api`)

```powershell
cd "D:\AI Tools\Hossam\mini-erp-saas-platform\backend"

dotnet restore .\MiniErp.sln
dotnet build .\MiniErp.sln --configuration Release

# Environment & Development Bootstrap Configuration
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:Scalar__Enabled="true"

# Development-Only Bootstrap (Seeds local admin user & generic tenant)
# NEVER set MESP_DEV_BOOTSTRAP_ENABLED in non-Development environments.
$env:MESP_DEV_BOOTSTRAP_ENABLED="true"
$env:MESP_DEV_ADMIN_LOGIN="admin@minierp.local"
$env:MESP_DEV_ADMIN_PASSWORD="<YOUR-LOCAL-PASSWORD>"

dotnet run --project .\src\MiniErp.Api\MiniErp.Api.csproj `
  --configuration Release `
  --no-build `
  --urls "http://localhost:5000"
```
---

## Terminal 2: Frontend (`Angular`)

```powershell
cd "D:\AI Tools\Hossam\mini-erp-saas-platform\frontend"
npm install
npm start -- --port 4300
```

The Angular dev server automatically proxies `/api` requests to `http://localhost:5000` via `proxy.conf.json`.

---

## Application & API Endpoints

- **Frontend Application**: `http://localhost:4300`
- **Backend API Base**: `http://localhost:5000`
- **Health Check**: `http://localhost:5000/health`
- **OpenAPI v1 Spec**: `http://localhost:5000/openapi/v1.json`
- **Scalar Interactive API Documentation**: `http://localhost:5000/scalar`

---

## Local Development Authentication Flow

1. **Sign In**: `POST http://localhost:5000/api/v1/auth/sign-in`
   - Body: `{ "login": "admin@minierp.local", "password": "<YOUR-LOCAL-PASSWORD>" }` (supplied in `MESP_DEV_ADMIN_PASSWORD`)
   - Sets secure session cookie `__Host-MiniErp.Auth`.
2. **Context Selection**: `GET /api/v1/auth/contexts` -> select Tenant context -> `POST /api/v1/auth/context-switch`.
3. **Antiforgery Bootstrap**: `GET /api/v1/auth/antiforgery` -> returns `X-CSRF-TOKEN` header.
4. **Master Data & Price List API Operations**:
   - `GET /api/v1/master-data/price-lists`
   - `POST /api/v1/master-data/price-lists` (requires `X-CSRF-TOKEN` and `Idempotency-Key`)