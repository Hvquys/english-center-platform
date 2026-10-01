# Local Development Guide

This runbook starts the English Center Platform on Windows with Docker Desktop.
Commands use PowerShell and must be run from:

```text
D:\hoangquy\workspace\Code_Workspace\english-center-platform
```

The repository never stores real passwords, signing keys, or tokens.

## 1. Prerequisites

Install and verify:

```powershell
git --version
dotnet --version
node --version
npm.cmd --version
docker --version
docker compose version
docker info
```

Expected result:

- Git is available.
- .NET SDK 10 is available.
- Node.js and npm are available.
- `docker info` shows a running Linux Docker engine. If it reports a missing
  `dockerDesktopLinuxEngine` pipe, open Docker Desktop and wait until the
  engine finishes starting.

## 2. Get the source and enter the repository

For a new checkout:

```powershell
git clone https://github.com/Hvquys/english-center-platform.git
Set-Location .\english-center-platform
```

For the existing checkout:

```powershell
Set-Location "D:\hoangquy\workspace\Code_Workspace\english-center-platform"
git status
```

Expected result: Git reports the current branch and any local changes. Do not
discard changes you do not recognize.

## 3. Create local configuration

```powershell
Copy-Item .\infrastructure\.env.example .\infrastructure\.env
```

Open `infrastructure/.env` locally and replace every `CHANGE_ME` placeholder
with a strong development-only value. Keep the file outside Git; the repository
`.gitignore` already excludes it.

Required secret values:

- `SQLSERVER_SA_PASSWORD`
- `JWT_SIGNING_KEY` with at least 32 random bytes
- `AUTH_BOOTSTRAP_ADMIN_PASSWORD`
- `REDIS_PASSWORD`
- `RABBITMQ_PASSWORD`
- `POSTGRES_DWH_PASSWORD` and `MINIO_ROOT_PASSWORD` before using the M6 data
  profile

Check that Git does not track the file:

```powershell
git check-ignore .\infrastructure\.env
git status --short
```

Expected result: the first command prints `infrastructure/.env`, and the file
does not appear as an untracked or modified file.

## 4. Validate and start the implemented M1–M5 stack

```powershell
docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml config --quiet

docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml up -d --build

docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml ps
```

The first command resolves Compose variables without displaying secret values.
The second builds and starts SQL Server, Redis, RabbitMQ, API, and web. The last
command must show all five services as running and healthy.

Open:

- Frontend: <http://localhost:5173>
- API health: <http://localhost:8080/api/health>
- RabbitMQ management: <http://localhost:15672>

Use the bootstrap admin email and password from the local `.env` file to sign
in. Do not paste those values into source code, chat, documentation, or Git.

## 5. Build outside Docker

```powershell
dotnet restore .\EnglishCenterPlatform.slnx
dotnet build .\EnglishCenterPlatform.slnx --no-restore
npm.cmd install --prefix .\apps\web
npm.cmd run lint --prefix .\apps\web
npm.cmd run build --prefix .\apps\web
```

Expected result: .NET reports zero errors, ESLint exits successfully, and Vite
creates `apps/web/dist`.

### Windows Application Control blocks EnglishCenter.Api.exe

The project sets `<UseAppHost>false</UseAppHost>`, so a normal current build
produces `EnglishCenter.Api.dll` rather than a generated application host.
If an old `EnglishCenter.Api.exe` remains from a previous build and Windows
blocks it, rebuild without apphost:

```powershell
dotnet clean .\EnglishCenterPlatform.slnx
dotnet build .\EnglishCenterPlatform.slnx -p:UseAppHost=false
dotnet .\apps\backend\EnglishCenter.Api\bin\Debug\net10.0\EnglishCenter.Api.dll
```

The final command runs the managed DLL directly. It avoids launching the
blocked generated EXE. For the normal integrated environment, prefer Docker
Compose because it also provides SQL Server, Redis, and RabbitMQ.

## 6. Run repeatable acceptance checks

Run the complete Application Core check:

```powershell
pwsh -NoProfile -File .\scripts\integration\verify-application-core.ps1
```

Every returned property must be `PASS`. The script creates timestamped fixture
data, tests Student → Class → Enrollment → Attendance → Payment, and removes
only those records.

Run the documentation contract check:

```powershell
pwsh -NoProfile -File .\scripts\docs\verify-project-documentation.ps1
```

It checks required files, README links, architecture boundaries, local commands,
secret-handling statements, and the SVG structure.

## 7. Start planned M6 storage dependencies

PostgreSQL DWH and MinIO exist behind the `data` Compose profile:

```powershell
docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml --profile data up -d
```

This starts infrastructure only. It does not mean the Python ETL, Spark
transforms, Airflow DAGs, star schema, or Power BI work is implemented.

## 8. Stop the environment

```powershell
docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml down
```

This stops containers and preserves named volumes. Do not add `-v` unless you
intentionally want to delete local SQL Server, Redis, RabbitMQ, PostgreSQL, and
MinIO data.

## 9. Common problems

| Symptom | Confirmed check | Action |
| --- | --- | --- |
| `dotnet` is not recognized | Run `dotnet --list-sdks` in a new PowerShell window. | Install .NET SDK 10 or reopen PowerShell so PATH refreshes. |
| Docker cannot connect to Linux engine | `docker info` reports a missing named pipe. | Start Docker Desktop and wait for the engine; keep the `desktop-linux` context. |
| Port is already allocated | Compose names port 1433, 5173, 5672, 6379, 8080, or 15672. | Stop the conflicting process or change the corresponding port variable in local `.env`. |
| API is unhealthy | `docker compose ... ps` shows SQL Server, Redis, or RabbitMQ unhealthy. | Inspect only the named service with `docker compose ... logs <service>`; fix its local configuration, then restart it. |
| Login returns 401 | API is healthy but credentials are rejected. | Use the bootstrap values from the same local `.env`; never print them for diagnosis. |
| Request returns 403 | JWT is valid but role policy denies the operation. | Use an account with the required role; do not weaken backend authorization. |
| Update/delete returns 409 | The state transition, relationship, or rowVersion is stale. | Reload the record, review the current state, and repeat only a valid transition. |
| Vite reports `spawn EPERM` | Windows policy blocks a child process. | Run the same build in an allowed terminal; this is separate from TypeScript errors. |

## 10. Daily workflow

```powershell
git status
git pull --ff-only
docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml up -d
docker compose --env-file .\infrastructure\.env `
  -f .\infrastructure\docker-compose.yml ps
```

Before committing, run the checks relevant to the change, review
`git diff --check`, update the project journal and management Sheet, then push
the coherent checkpoint.
