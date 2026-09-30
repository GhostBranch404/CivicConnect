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

## Project status

CivicConnect is currently in Milestone 2 development.

Implemented or in progress:
- Service request submission
- Request retrieval and status updates
- MySQL persistence using Dapper
- Request history logging
- Developer/application logging
- Basic database transaction and concurrency handling

Still in progress:
- Authentication and Role-Based Access Control
- Staff assignment workflow
- Management dashboard
- Full verification and testing
- Production hosting and deployment

## User roles

- **Requester** – submits requests and views their own request progress.
- **Staff** – manages, assigns and updates service requests.
- **Management** – views higher-level request and performance information.

## Core features

- Submit a service request
- Generate and track a unique ticket
- View request history and status
- Assign requests to staff
- Update progress and resolution information
- Record request history
- Record selected developer/application logs
- Provide management oversight through a dashboard

## Architecture

CivicConnect follows a 3-tier layered structure:

```text
Controller
    ↓
Service
    ↓
Repository
    ↓
Dapper / MySqlConnector
    ↓
MySQL
```

This keeps request handling, business rules and database access separated.

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

## Testing and verification

Basic project checks:

```bash
dotnet restore
dotnet build
```

Database verification also includes:
- Applying the SQL migration
- Creating and retrieving a request
- Updating request status
- Checking request history and developer logs
- Checking rollback behaviour
- Checking concurrency conflicts

Some of these checks are still being completed during M2.

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

## Known limitations

- Authentication and RBAC are still being finalised.
- Production hosting has not yet been selected.
- Backup and recovery procedures are not finalised.
- Performance/load testing is still outstanding.
- The user interface is still under development.

## Git workflow

- Do not commit substantive changes directly to `main`.
- Use a feature branch for development work.
- Open a Pull Request before merging.
- Team members should review changes before they are merged.

## Security notes

- Do not commit passwords or database credentials.
- Use User Secrets or environment variables for connection strings.
- Database queries should remain parameterised.
- Sensitive values should not be written to developer logs.

## Engineering documentation

- `CivicConnect/docs/adr/ADR-001_architectural_style.md`
- `CivicConnect/docs/adr/ADR-002_Tech_stack_and_persistence.md`
- `CivicConnect/docs/adr/ADR-003_data_persistence.md`
- `CivicConnect/docs/database-design.md`
- `CivicConnect/docs/diagrams/CivicConnect_ERD.md`

## External project documentation

Project documentation workspace: Belgium Campus SharePoint.

Project/task tracking: ClickUp.
