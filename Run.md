Run these in two PowerShell terminals:

Backend

cd "D:\AI Tools\Hossam\mini-erp-saas-platform\backend"

dotnet restore .\MiniErp.sln
dotnet build .\MiniErp.sln --configuration Release

$env:ASPNETCORE_ENVIRONMENT="Development"
$env:Scalar__Enabled="true"

dotnet run --project .\src\MiniErp.Api\MiniErp.Api.csproj `
  --configuration Release `
  --no-build `
  --urls "http://localhost:5000"


  ----------------------

Frontend

cd "D:\AI Tools\Hossam\mini-erp-saas-platform\frontend"

npm install
npm start -- --port 4300

Then use:

Frontend: http://localhost:4300
Backend: http://localhost:5000
Health: http://localhost:5000/health
OpenAPI JSON: http://localhost:5000/openapi/v1.json
Scalar API UI: http://localhost:5000/scalar