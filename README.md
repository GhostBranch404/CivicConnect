# CivicConnect

**SEN381 — Community Service Request Management Platform**

CivicConnect is a web application for submitting, tracking, assigning and resolving community service requests. The project is being developed as a working application; Milestone 3 is a checkpoint, not the end of implementation.

## Current project position

- **Backend:** C# / ASP.NET Core Web API on .NET 9
- **Architecture:** 3-tier layered monolith — Controller → Service → Repository
- **Persistence:** MySQL with Dapper and MySqlConnector
- **Schema management:** Version-controlled SQL migration scripts
- **Application hosting target:** Azure App Service (`civicconnect1`)
- **Hosted database provider:** Aiven for MySQL is the current documented choice; a successful deployed connection still needs to be evidenced.
- **Current release status:** **Not production-ready.** On 15 October 2026, the Azure site returned 404 for `/` and `/swagger`. The published .NET files and a process listening on port 8080 were observed, but a successful homepage/API/database operation has not yet been recorded as release evidence.

## Why these technologies?

| Decision | Reason | Trade-off / condition |
|---|---|---|
| **MySQL (relational)** | Requests, users, categories, statuses, assignments and history have defined relationships. Foreign keys, unique constraints and transactions help protect consistency. | Schema changes need controlled migrations; queries and mappings need tests. |
| **Dapper + MySqlConnector** | Keeps SQL explicit and fits the repository code already in the project without an ORM migration. | The team must maintain SQL, mappings, transactions and concurrency checks itself. |
| **Aiven for MySQL (documented choice)** | Managed MySQL reduces the server administration the student team must do and leaves the application on the same database engine. | Plan limits, TLS/connection settings, costs, backups and a real application connection must be verified. A provider choice is not proof that deployment works. |
| **Azure App Service (hosting target)** | Managed hosting for the existing ASP.NET Core application, with GitHub Actions deployment and Azure monitoring options. | Deployment must be tied to the intended branch/commit and verified using real HTTP and database checks. The current 404 is still an open blocker. |
| **Layered monolith** | A small team can build and test one deployable application while keeping HTTP, business rules and SQL separate. | Boundaries must be maintained; microservices would add deployment and operational overhead without a current need. |

A document database was considered, but is not the best fit for the current core workflow: many records reference other records, status changes must be consistent, and the system needs history and reporting. MySQL is a project-fit decision, not a claim that document databases are generally inferior.

## Main features and intended roles

- **Requester:** submit a request and follow its progress.
- **Staff:** access the work queue and manage request progress/assignments.
- **Management:** view oversight and reporting information.

The current API includes request creation, collection/detail retrieval and status updates. Authentication/RBAC, assignment, management reporting and the full user experience remain work to complete or verify. Do not treat a documented requirement or test method as proof that a feature is finished.

## Architecture

```text
HTTP / UI
   ↓
RequestController
   ↓
RequestService (validation and workflow rules)
   ↓
IRequestRepository / RequestRepository
   ↓
Dapper + MySqlConnector
   ↓
MySQL
```

Request history (`RequestHistory`) records business lifecycle events. `DeveloperLogs` records selected technical/application events for troubleshooting. They have different purposes and should not be treated as interchangeable.

## Repository guide

- `src/CivicConnect/Controllers/` — HTTP endpoints
- `src/CivicConnect/Services/` — business rules and service operations
- `src/CivicConnect/Data/` — repository interfaces and SQL persistence
- `src/CivicConnect/Models/` — request, history and log models
- `src/CivicConnect/database/migrations/001_initial_schema.sql` — initial schema and reference seed data
- `src/CivicConnect/database/migrations/002_user_registration_confirmation.sql` — registration and contact-confirmation fields
- `Tests/CivicConnect.Tests/` — unit, integration and persistence test code
- `src/docs/adr/` — architecture decision records
- `CivicConnect_PED_M3.md` — milestone engineering document

## Local setup

Prerequisites: .NET 9 SDK, Git and a MySQL instance.

```bash
git clone https://github.com/GhostBranch404/CivicConnect.git
cd CivicConnect
dotnet restore CivicConnect.sln
```

Configure `ConnectionStrings:DefaultConnection` with .NET User Secrets or an environment variable. Never commit real credentials.

For local development secrets:

```bash
cd src/CivicConnect
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=CivicConnect;User=YOUR_USER;Password=YOUR_PASSWORD;"
```

Apply the schema to the intended development database using a MySQL client:

```bash
mysql -h localhost -P 3306 -u YOUR_USER -p CivicConnect < ../../src/CivicConnect/database/migrations/001_initial_schema.sql
mysql -h localhost -P 3306 -u YOUR_USER -p CivicConnect < ../../src/CivicConnect/database/migrations/002_user_registration_confirmation.sql
```

Configure SMTP settings under `Email` and Twilio credentials under `Sms` using User Secrets or environment variables (for example, `Email__Password` and `Sms__AuthToken`). Keep real credentials out of source control. ASP.NET Core Data Protection keys must persist across restarts and be shared between app instances in a multi-instance deployment; configure a persistent, protected key ring before deploying confirmation flows.

Then run:

```bash
dotnet run --project src/CivicConnect
```

Swagger is configured for the Development environment. The application also maps `GET /health`; this endpoint reports application health and should not by itself be taken as proof that database writes succeed.

## API surface currently defined

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/requests` | Create a service request |
| `GET` | `/api/requests` | Retrieve requests |
| `GET` | `/api/requests/{id}` | Retrieve one request |
| `PATCH` | `/api/requests/{id}/status` | Update status with actor and expected version |
| `POST` | `/api/auth/register` | Create a requester account and send an email or SMS confirmation code |
| `POST` | `/api/auth/confirm` | Confirm an account using its destination and code |
| `POST` | `/api/auth/resend-confirmation` | Resend a code for an unconfirmed account |
| `GET` | `/health` | Application health endpoint |

Registration accepts `FullName`, `Password` (at least 10 characters), `ConfirmationMethod` (`Email` or `Sms`), and either `Email` or `PhoneNumber` (E.164 format, such as `+15551234567`). Confirmation codes expire after 10 minutes; a code can be tried five times before it must be resent. Signup and code endpoints are rate-limited. A confirmed contact does not yet enable login or grant an authentication token.

Example registration and confirmation requests:

```json
{
  "fullName": "Aiden Example",
  "password": "a-long-user-chosen-password",
  "confirmationMethod": "Email",
  "email": "aiden@example.com"
}
```

```json
{
  "confirmationMethod": "Email",
  "destination": "aiden@example.com",
  "code": "123456"
}
```

Example status update:

```json
{
  "newStatus": "In Progress",
  "changedByUserId": 2,
  "expectedVersion": 1,
  "notes": "Work started"
}
```

These routes are defined in source code; their deployed availability must be checked against the current Azure release.

## Local staging stack

The repository contains a Docker Compose stack for an isolated API + MySQL environment:

```bash
docker compose -f docker-compose.staging.yml up --build
```

It maps the API to `http://localhost:8080` and the local MySQL port to `3307`. The Compose file currently contains development-only example credentials; replace them with environment-supplied values before using it outside a disposable local environment. Docker Compose staging is separate from the Azure deployment.

## Build and test

```bash
dotnet restore CivicConnect.sln
dotnet build CivicConnect.sln --configuration Release
dotnet test CivicConnect.sln --configuration Release
```

Database-backed tests require `CIVICCONNECT_TEST_CONNECTION` to point to a test database that has the migration applied. A test method existing in source is not proof it passed; record the run result and environment.

The current CI workflow in `.github/workflows/ci.yml` runs on pushes and pull requests targeting `main` or `dev`, not `staging`. The Azure deployment workflow in `.github/workflows/main_civicconnect1.yml` deploys on pushes to `main` or manual dispatch. A commit on `staging` is therefore not automatically proof of what is running in Azure.

## Release evidence still required

- [ ] Confirm the actual database provider/endpoint used by the deployed app without publishing credentials.
- [ ] Apply the migration and record a successful result.
- [ ] Create a request and verify the saved row, history and developer log.
- [ ] Update status, reload the request and verify persistence/history/actor.
- [ ] Run and record the relevant unit, API integration and database-backed tests.
- [ ] Verify Azure homepage, one real API response and database connectivity.
- [ ] Record the deployed commit SHA and successful workflow run.
- [ ] Verify secret configuration, TLS, backups/restore and cost/plan limits.
- [ ] Complete and verify authentication/RBAC, staff assignment and management reporting before claiming those features are ready.

## Security and team workflow

- Use parameterised SQL; do not concatenate user input into queries.
- Keep credentials in User Secrets, environment variables or deployment secrets, never in source control or logs.
- Keep business rules in services and SQL in repositories.
- Work on a feature branch, open a pull request, request peer review and update the PED/RTM/risk register when a requirement or decision changes.
- AI-assisted code and documentation must be reviewed by the team and verified through tests or other appropriate evidence.

## Engineering decision records

- [ADR-001 — Layered architecture](src/docs/adr/ADR-001_architectural_style.md)
- [ADR-002 — Technology stack and persistence](src/docs/adr/ADR-002_Tech_stack_and_persistence.md)
- [ADR-003 — Persistence and data integrity](src/docs/adr/ADR-003_data_persistence.md)
- [ADR-004 — Hosting and release environment](src/docs/adr/ADR-004_hosting_and_release.md)
- [Database design](src/docs/database-design.md)
- [SQL migration](src/CivicConnect/database/migrations/001_initial_schema.sql)
