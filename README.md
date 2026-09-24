# English Center Platform

English Center Platform is a learning and portfolio project that combines a
business application, a data platform, observability, and an AI assistant.

## Current milestone

M1 — Project Setup & Infrastructure

The initial application consists of:

- React and TypeScript frontend in `apps/web`
- ASP.NET Core Web API in `apps/backend/EnglishCenter.Api`
- SQL Server OLTP as the future operational source of truth

The data, observability, and AI components will be introduced in later
milestones according to the project roadmap.

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

Do not commit passwords, tokens, connection-string credentials, private keys,
or other secrets. Use local environment files derived from committed
`.env.example` files when configuration is introduced.
