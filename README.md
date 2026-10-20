# Phone Numbers Management

.NET 10 backend for ranges, phone numbers, reservations, and scheduled Teams reconciliation. Start with [the architecture, schema, lifecycle, and decisions](docs/architecture.md).

## Solution

Open `Backend/PhoneNumbersManagement/PhoneNumbersManagement.slnx` in Visual Studio 2026. The existing PhoneNumbersManagement project is the API. Domain and Application have no ASP.NET, EF Core, or Hangfire dependencies. Infrastructure supplies adapters; Worker is a separate executable. Tests exercise business rules and HTTP behavior.

## Build and test

From the repository root:

```powershell
dotnet tool restore
dotnet build Backend/PhoneNumbersManagement/PhoneNumbersManagement.slnx
dotnet test Backend/PhoneNumbersManagement/PhoneNumbersManagement.slnx
```

## Configure and migrate

Provision a PostgreSQL database through the product team or an isolated local instance. Do not commit credentials. Use environment variables or your secret manager:

```powershell
$env:ConnectionStrings__Registry = 'Host=localhost;Database=phone_registry;Username=YOUR_USER;Password=YOUR_PASSWORD'
$env:Entra__TenantId = 'YOUR_TENANT_GUID'
$env:Entra__Audience = 'YOUR_API_TOKEN_AUDIENCE'
dotnet ef database update --project Backend/PhoneNumbersManagement/PhoneNumbersManagement.Infrastructure
```

Set Audience to the audience actually issued for this API. Register the read, write, and admin app roles in Entra and assign them to authorized callers. Startup deliberately fails if the database connection or Entra settings are missing. No insecure authentication bypass is provided for local development.

Migrations are applied explicitly by deployment tooling, not automatically at application startup. Inspect the migration before applying it to shared environments. Generate a deployment script with:

```powershell
dotnet ef migrations script --idempotent --project Backend/PhoneNumbersManagement/PhoneNumbersManagement.Infrastructure
```

## Run the API

```powershell
dotnet run --project Backend/PhoneNumbersManagement/PhoneNumbersManagement --launch-profile https
```

Read the OpenAPI 3.1 document at `https://localhost:7185/openapi/v1.json`. Use the checked-in `.http` examples with a real Entra access token containing app roles. In Development, Swagger UI is available at `https://localhost:7185/swagger` and opens automatically when launching the API from Visual Studio. It groups endpoints by controller, uses standard colored method labels, supports filtering and expandable requests, and provides an Authorize button for an Entra bearer token. Tokens are not persisted in browser storage. GET is blue and POST is green in the standard Swagger palette. The UI and its custom stylesheet are served only in Development. All business endpoints require authentication.

## Run background jobs

The Worker needs the same Registry connection string. Optionally set ConnectionStrings__Hangfire to a separate PostgreSQL database; otherwise Hangfire uses the Registry database with its own schema. Its account needs appropriate Hangfire schema-creation permissions.

Configure a managed identity or environment credentials recognized by DefaultAzureCredential. The identity needs tenant-admin-approved Graph application permissions TeamsTelephoneNumber.Read.All and User.Read.All. Never put client secrets in appsettings or commit them.

```powershell
dotnet run --project Backend/PhoneNumbersManagement/PhoneNumbersManagement.Worker
```

The two recurring jobs run in UTC: Teams synchronization every 15 minutes and reservation cleanup every five minutes. Override Jobs__TeamsCron and Jobs__CleanupCron if agreed. Cleanup processes up to 1,000 expired reservations per run. Offboarding deadlines survive restarts in PostgreSQL; execution occurs on the next cleanup tick. The worker uses standard Hangfire features; no private Pro feed/license is needed for this implementation.

## Current delivery limits

The repository includes the initial PostgreSQL migration, API, jobs, Graph adapter, and automated tests. LEGO-specific catalogue registration, production provisioning, existing-data migration, and real-tenant integration testing remain environment-dependent. Range boundary editing, reservation history, and actual Teams unassignment require product decisions documented in the architecture.
