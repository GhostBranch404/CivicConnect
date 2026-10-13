# CivicConnect

**SEN381 Project – Community Service Request Management Platform**

CivicConnect is a controlled digital platform for submitting, tracking, assigning and resolving community service requests with engineering traceability.

## Current M3 working baseline

- C# / ASP.NET Core Web API
- 3-tier layered monolith
- MySQL hosted through Aiven
- Dapper + MySqlConnector
- Controller → Service → Repository → Database
- Request history and database-backed developer/application logging

## Project status

CivicConnect is currently in Milestone 3 development; implementation continues beyond this milestone toward the final working application.

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

## Database hosting

- Provider: Aiven for MySQL.
- Database engine remains MySQL; the provider change does not change the relational schema or Dapper/MySqlConnector approach.
- Store connection strings and credentials in environment variables or deployment secrets. Never commit database credentials to the repository.
- Confirm connection access, migrations, backup/restore, plan limits and costs before production use.

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
src/
└── CivicConnect/
    ├── Controllers/
    ├── Data/
    ├── Models/
    ├── Services/
    └── database/
        └── migrations/
Tests/
└── CivicConnect.Tests/
src/docs/
└── adr/
```

## Prerequisites

- .NET 9 SDK
- MySQL development database
- Git

## Local setup

```bash
git clone https://github.com/GhostBranch404/CivicConnect.git
cd CivicConnect
dotnet restore CivicConnect.sln
```

Provide `ConnectionStrings:DefaultConnection` through ASP.NET User Secrets or environment variables.

Do **not** commit real database credentials.

Example User Secrets setup:

```bash
cd src/CivicConnect
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=CivicConnect;User=YOUR_USER;Password=YOUR_PASSWORD;"
```

## Database setup

Apply the SQL migration in order:

```text
src/CivicConnect/database/migrations/001_initial_schema.sql
```

The initial schema creates the request, reference, assignment, history, comment and developer-log tables and seeds the initial request statuses/categories.

## Run

```bash
dotnet run --project src/CivicConnect
```

When running in Development, Swagger is enabled for the Web API.

## API endpoints (backend)

- `POST /api/requests`
- `GET /api/requests`
- `GET /api/requests/{id}`
- `PATCH /api/requests/{id}/status`

Status updates require:

```json
{
  "newStatus": "In Progress",
  "changedByUserId": 2,
  "expectedVersion": 1,
  "notes": "Work started"
}
```

HTTP behavior:
- `201 Created` on successful request creation
- `400 BadRequest` for invalid payloads and invalid foreign-key references
- `404 NotFound` when a request does not exist
- `409 Conflict` for duplicate tickets and concurrency conflicts

## Staging setup (Docker Compose)

Run isolated staging services (API + MySQL):

```bash
docker compose -f docker-compose.staging.yml up --build
```

Staging stack details:
- MySQL is isolated from local test and production data
- Schema is initialized from `src/CivicConnect/database/migrations/001_initial_schema.sql`
- API health endpoint: `GET /health`
- API base URL (compose): `http://localhost:8080`

## Testing and verification

Run the full suite with a MySQL test database. Set `CIVICCONNECT_TEST_CONNECTION` to its connection string and apply `src/CivicConnect/database/migrations/001_initial_schema.sql` first:

```bash
dotnet test CivicConnect.sln
```

Database verification also includes:

Required database persistence scenarios are verified in `Tests/CivicConnect.Tests/RequestPersistenceTests.cs`:
1. Valid request saves/retrieves with initial status, version, history and developer log.
2. Invalid requester fails with no partial records.
3. Duplicate ticket is rejected while preserving the original and writing no partial duplicate records.
4. Resolve request stores status/version, resolution notes, history and developer log.
5. Transaction rollback is verified by forcing a related write failure after transaction start.

API integration behavior is verified in `Tests/CivicConnect.Tests/Integration/RequestApiIntegrationTests.cs` with strict expected responses (no permissive status assertions).

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

- `src/docs/adr/ADR-001_architectural_style.md`
- `src/docs/adr/ADR-002_Tech_stack_and_persistence.md`
- `src/docs/adr/ADR-003_data_persistence.md`
- `src/docs/database-design.md`
- `src/docs/diagrams/CivicConnect_ERD.md`

## External project documentation

Project documentation workspace: Belgium Campus SharePoint: https://belgiumcampusacza-my.sharepoint.com/:f:/g/personal/601609_student_belgiumcampus_ac_za/IgCLEkD1yq6URJN_AeA2ypQ4AQFPC6EZgSYZdrtlkCiyUEU?e=XZAf1L

Project/task tracking: ClickUp.
