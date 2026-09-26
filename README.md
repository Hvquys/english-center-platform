# English Center Platform

English Center Platform is a learning and portfolio project that combines a
business application, a data platform, observability, and an AI assistant.

## Current milestone

M5 — Application Integration with RabbitMQ

The initial application consists of:

- React and TypeScript frontend in `apps/web`
- ASP.NET Core Web API in `apps/backend/EnglishCenter.Api`
- SQL Server OLTP as the operational source of truth
- RabbitMQ for durable notification integration events

The data, observability, and AI components will be introduced in later
milestones according to the project roadmap.

## Frontend foundation and login

The React application now provides routing, a protected application shell,
typed API contracts, login form validation, JWT session handling, refresh-token
rotation, logout, and a responsive dashboard foundation. Browser requests use
the relative `/api` path. Vite proxies it to `http://localhost:8080` during
local development, while Nginx proxies it to the `api` Compose service in the
container. Override `VITE_API_BASE_URL` only when the API uses another origin.

The current backend returns tokens in JSON, so the web client stores the
session in `sessionStorage`: it survives a page refresh in the same tab and is
removed when that tab closes. Passwords are never stored. A production release
should prefer an HttpOnly, Secure, SameSite refresh-token cookie when the API
contract is extended to support it.

Run the frontend locally:

```powershell
Set-Location .\apps\web
Copy-Item .env.example .env.local
npm.cmd install
npm.cmd run dev
```

Rebuild the Compose web service and run the repeatable acceptance check. The
script verifies SPA fallback, the Nginx API proxy, invalid-login Problem
Details, admin login, and `/api/auth/me` without printing credentials or
tokens:

```powershell
docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml up -d --build web
.\scripts\web\verify-web-foundation.ps1
```

## RabbitMQ notification topology

The API now owns a durable RabbitMQ topology for notification requests:

- topic exchange `english-center.notifications`
- durable dispatch queue `english-center.notifications.dispatch`
- binding `notification.*.requested`
- direct dead-letter exchange `english-center.notifications.dlx`
- durable dead-letter queue `english-center.notifications.dead-letter`
- dead-letter routing key `notification.dead-letter`

`POST /api/notifications` requires an `ADMIN` or `STAFF` bearer token. It
accepts the request only after RabbitMQ confirms the persistent message. Email
requests use `notification.email.requested`; in-app requests use
`notification.in-app.requested`. TASK-015 will add the worker that consumes the
dispatch queue, retries failures, and makes processing idempotent.

RabbitMQ is enabled in Docker Compose and its username/password come from the
ignored `infrastructure/.env` file. The committed settings contain no real
credentials. Start or rebuild the stack, then run the repeatable acceptance
check:

```powershell
docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml up -d --build
.\scripts\messaging\verify-rabbitmq-topology.ps1
```

The script verifies broker health, exchanges, queues, bindings, `401`, invalid
request `400`, authorized `202`, publisher-confirmed routing, dead-letter
routing, and final queue cleanup. It reads local credentials without printing
passwords or tokens.

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

If the operational database was originally created by the TASK-003 SQL script,
record that verified schema as the initial EF baseline once, then apply later
migrations normally:

```powershell
.\scripts\database\baseline-ef-operational-database.ps1
.\scripts\database\update-ef-database.ps1 -DatabaseName EnglishCenter
```

The baseline script refuses to continue unless all seven original business
tables exist. It does not recreate or delete operational data.

## API foundation verification

The backend is organized as a modular monolith. Each business area keeps its
controller, request/response contracts, service, and dependency registration
under `Modules/<ModuleName>`. Shared HTTP behavior lives under `Api`, including
OpenAPI, model validation, RFC Problem Details responses, and global exception
handling.

Start or rebuild the API and its SQL Server dependency:

```powershell
docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml up -d --build api
```

Run the repeatable acceptance check:

```powershell
.\scripts\api\verify-api-foundation.ps1
```

The script verifies API and SQL Server health, OpenAPI paths, `404`, `405`, and
validation `400` Problem Details responses, the Students module boundary, and
the browser CORS preflight. Every check must report `PASS`.

## JWT authentication and RBAC verification

The API issues short-lived JWT access tokens and rotates opaque refresh tokens.
Only a SHA-256 hash of each refresh token is stored. Passwords use ASP.NET Core
Identity's password hasher. Roles are `ADMIN`, `STAFF`, `TEACHER`, and
`STUDENT`.

Set strong local-only values in `infrastructure/.env` for
`JWT_SIGNING_KEY`, `AUTH_BOOTSTRAP_ADMIN_EMAIL`, and
`AUTH_BOOTSTRAP_ADMIN_PASSWORD`. The Compose file passes them to the API; the
real values stay outside Git. Then run:

```powershell
.\scripts\api\verify-auth-rbac.ps1
```

The test checks unauthenticated `401`, invalid-login `401`, forbidden `403`,
all four roles, policy boundaries, `/api/auth/me`, refresh-token rotation,
logout revocation, cleanup, and OpenAPI auth paths. The existing API acceptance
scripts obtain a local admin token without printing credentials or tokens.

## People API verification

The Students and Teachers modules provide paged search, detail, create, update,
and soft-delete endpoints. Update requests carry the Base64 `rowVersion`
returned by the previous response. Delete requests send the same value in the
`If-Match` header. A stale version returns HTTP `409`, preventing a later write
from silently overwriting an earlier one.

Run the end-to-end CRUD acceptance check against the Docker API:

```powershell
.\scripts\api\verify-people-api.ps1
```

The script creates isolated test records, verifies paging and filters, updates
them, confirms stale-write protection, soft-deletes them, proves the deleted
codes remain reserved, and checks the OpenAPI operations.

## Learning API verification

The Courses, Classes, and Enrollments modules implement the core learning
workflow. Courses start as `DRAFT`; classes start as `PLANNED`; enrollment is
allowed only when the course is active, the class is open, the student and
teacher are active, and capacity remains available. Status transitions and
row-version checks prevent invalid or stale updates.

Run the end-to-end learning workflow check:

```powershell
.\scripts\api\verify-learning-api.ps1
```

The script verifies course activation, class opening, tuition defaults,
duplicate enrollment and capacity conflicts, lifecycle transitions, search and
filters, soft-delete cleanup, and OpenAPI coverage.

## Attendance and payment API verification

Attendance records are accepted only for active enrollments while the class is
`IN_PROGRESS`. A student can have one attendance record per class date. Payment
records start as `PENDING`; completing a payment checks that the total completed
amount does not exceed the enrollment's agreed tuition. Both modules support
paging, filters, soft delete, ETag/If-Match, and row-version conflict handling.

Run the end-to-end acceptance check:

```powershell
.\scripts\api\verify-attendance-payment-api.ps1
```

The script verifies attendance date and duplicate guards, payment lifecycle and
overpayment prevention, stale-write handling, filters, cleanup, and OpenAPI.

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

RabbitMQ is now part of the core Compose runtime because the M5 API publishes
notification events through it. Redis remains under the `integration` profile
until TASK-016. The PostgreSQL analytical DWH and MinIO RAW/Bronze storage are
defined for M6 under the `data` profile.

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
