# Phone Numbers Management

.NET 10 backend for ranges, phone numbers, reservations, and scheduled Teams reconciliation. Start with [the architecture, schema, lifecycle, and decisions](docs/architecture.md).

For release risks, requirement traceability, architecture decisions, acceptance tests and delivery gates, read the [production architecture review](docs/production-architecture-review.md).

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

## Code First changes

The C# entities and EF configuration classes define the database model. Each entity has its own file in Domain/Entities; each mapping has its own IEntityTypeConfiguration class in Infrastructure/Configurations. RegistryDbContext discovers these mappings automatically. Migrations and the model snapshot remain generated files in Infrastructure/Migrations.

To change the schema:

1. Edit the entity and its configuration as needed. Update request/response contracts and business rules if the feature requires it. A business-method-only change does not require a migration.
2. From the repository root, generate a named migration:

   ```powershell
   dotnet ef migrations add DescribeYourChange --project Backend/PhoneNumbersManagement/PhoneNumbersManagement.Infrastructure
   ```

   EF compares the current configured model with the previous model snapshot, generates Up/Down operations and migration metadata, and updates the snapshot at this step. It does not compare against or change the live database here.
3. Review the generated migration, especially renames, required columns on existing rows, and potential data loss. EF may need manual migration edits to represent a rename or data transformation correctly. Do not hand-edit the snapshot as the normal workflow.
4. Apply the reviewed migration to your configured local database:

   ```powershell
   dotnet ef database update --project Backend/PhoneNumbersManagement/PhoneNumbersManagement.Infrastructure
   ```

   This applies pending migrations and records them in __EFMigrationsHistory. It does not generate a migration or update the source snapshot. Use the reviewed deployment process for shared/production databases.
5. Build, test, and commit the entity/configuration changes together with the generated migration, metadata, and snapshot.

To check whether the code model has changed without a migration:

```powershell
dotnet ef migrations has-pending-model-changes --project Backend/PhoneNumbersManagement/PhoneNumbersManagement.Infrastructure
```

Splitting classes into separate files does not change their model identities or database tables. This refactor therefore needs no new migration.

Reference: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/

## Code formatting and readability

Repository `.editorconfig` supplies Visual Studio and .NET formatting conventions: four-space indentation, consistent spaces around operators and parameters, braces on separate lines, and expanded multi-statement blocks. A focused StyleCop.Analyzers development dependency adds blank-line checks between members, after blocks, and around comments, while rejecting excessive blank lines. It is private to development/build tooling; it adds no application runtime library. The pinned 1.2.0-beta.556 analyzer release supports modern C# constructs. Unrelated documentation, naming, and ordering rules are disabled to keep feedback focused.

Keep one blank line between properties, constructors, and methods. Group related statements into short paragraphs and use one executable statement per line. Keep auto-properties compact; expand method/control-flow bodies. Generated EF migration files are excluded from formatting changes.

In Visual Studio, use **Ctrl+K, Ctrl+D** to format a document. Layout analyzer suggestions appear in the editor; use **Ctrl+.** to apply applicable fixes. To format the solution from the repository root:

```powershell
dotnet format whitespace Backend/PhoneNumbersManagement/PhoneNumbersManagement.slnx --no-restore
dotnet format style Backend/PhoneNumbersManagement/PhoneNumbersManagement.slnx --no-restore --diagnostics IDE0011 --severity warn
dotnet format analyzers Backend/PhoneNumbersManagement/PhoneNumbersManagement.slnx --no-restore --diagnostics SA1500 SA1502 SA1503 SA1505 SA1507 SA1513 SA1515 SA1516 --severity warn
```

Use the same commands with `--verify-no-changes` to check formatting without modifying source. Layout rules are preferences for this repository, not universal requirements of C# or DDD.
