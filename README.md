# CivicConnect

**SEN381 Project – Community Service Request Management Platform**

CivicConnect is a controlled digital platform for submitting, tracking, assigning and resolving community service requests with engineering traceability.

## Current M2 baseline

- C# / ASP.NET Core Web API
- 3-tier layered monolith
- MySQL
- Dapper + MySqlConnector
- Controller → Service → Repository → Database
- Request history and database-backed developer/application logging

## Repository structure

```text
CivicConnect/
├── Controllers/
├── Data/
├── Models/
├── Services/
├── database/
│   └── migrations/
└── docs/
    ├── adr/
    ├── diagrams/
    └── database-design.md
```

## Prerequisites

- .NET 8 SDK
- MySQL development database
- Git

## Local setup

```bash
git clone https://github.com/GhostBranch404/CivicConnect.git
cd CivicConnect/CivicConnect
dotnet restore
```

Copy `appsettings.example.json` to a local configuration file or provide `ConnectionStrings:DefaultConnection` through ASP.NET User Secrets/environment variables.

Do **not** commit real database credentials.

Example User Secrets setup:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=CivicConnect;User=YOUR_USER;Password=YOUR_PASSWORD;"
```

## Database setup

Apply the SQL migration in order:

```text
CivicConnect/database/migrations/001_initial_schema.sql
```

The initial schema creates the request, reference, assignment, history, comment and developer-log tables and seeds the initial request statuses/categories.

## Run

```bash
dotnet run
```

When running in Development, Swagger is enabled for the Web API.

## Request status update body

Status updates now include the acting user and expected concurrency version:

```json
{
  "newStatus": "In Progress",
  "changedByUserId": 2,
  "expectedVersion": 1,
  "notes": "Request acknowledged and work started."
}
```

## Engineering documentation

- `CivicConnect/docs/adr/ADR-001_architectural_style.md`
- `CivicConnect/docs/adr/ADR-002_Tech_stack_and_persistence.md`
- `CivicConnect/docs/adr/ADR-003_data_persistence.md`
- `CivicConnect/docs/database-design.md`
- `CivicConnect/docs/diagrams/CivicConnect_ERD.md`

## External project documentation

Project documentation workspace: Belgium Campus SharePoint.

Project/task tracking: ClickUp.
