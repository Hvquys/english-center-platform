# English Center Platform

English Center Platform is a learning and portfolio project that combines a
business application, a data platform, observability, and an AI assistant.

## Current milestone

M2 — Backend ASP.NET Core Web API + EF Core + SQL Server

The initial application consists of:

- React and TypeScript frontend in `apps/web`
- ASP.NET Core Web API in `apps/backend/EnglishCenter.Api`
- SQL Server OLTP as the operational source of truth

The data, observability, and AI components will be introduced in later
milestones according to the project roadmap.

## EF Core database workflow

The backend maps the seven initial OLTP entities with EF Core. Migrations are
managed by the repository-local `dotnet-ef` tool, so every developer uses the
same tool version.

Restore the tool and compile the backend:

```powershell
dotnet tool restore
dotnet build .\EnglishCenterPlatform.slnx
```

Apply the migration to an isolated local verification database. The script
reads the SQL Server password from the ignored `infrastructure/.env` file,
keeps it in the current process only, and never prints or commits it:

```powershell
.\scripts\database\update-ef-database.ps1 -DatabaseName EnglishCenterEfTest
```

Verify schema objects, migration history, deterministic seed data, audit
timestamps, and row-version behavior:

```powershell
.\scripts\database\verify-ef-database.ps1 -DatabaseName EnglishCenterEfTest
```

`EnglishCenterEfTest` is intentionally separate from the operational
`EnglishCenter` database created in TASK-003. This proves that the migration
can build a clean database without overwriting local operational data.

## Project documentation

- The project-wide build journal covers the complete M1-M10 delivery process.
- Journal entries follow [`docs/PROJECT_JOURNAL_GUIDE.md`](docs/PROJECT_JOURNAL_GUIDE.md).
- After each completed or materially changed task, update the journal and the
  project management Sheet, then create a Git checkpoint when the state is
  coherent and verified.

## Prerequisites

- .NET SDK 10
- Node.js 24 or a supported LTS release
- Docker Desktop with Linux containers
- Git

## Build

```powershell
dotnet build .\EnglishCenterPlatform.slnx
npm.cmd install --prefix .\apps\web
npm.cmd run build --prefix .\apps\web
```

## Docker Compose

The default stack starts SQL Server (the OLTP source of truth), the ASP.NET
Core API, and the React frontend:

```powershell
Copy-Item .\infrastructure\.env.example .\infrastructure\.env
# Replace every CHANGE_ME value in infrastructure\.env before continuing.
docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml config
docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml up -d --build
docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml ps
```

After every service reports healthy:

- Frontend: <http://localhost:5173>
- API health: <http://localhost:8080/api/health>

Redis and RabbitMQ are defined for M5 under the `integration` profile. The
PostgreSQL analytical DWH and MinIO RAW/Bronze storage are defined for M6 under
the `data` profile. Defining these profiles does not make them part of the
initial core runtime.

```powershell
docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml --profile integration up -d
docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml --profile data up -d
```

Stop containers without deleting their persistent data:

```powershell
docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml down
```
Do not commit passwords, tokens, connection-string credentials, private keys,
or other secrets. Use local environment files derived from committed
`.env.example` files when configuration is introduced.
