# CivicConnect

**SEN381 — Community Service Request Management Platform**

CivicConnect is a web application for submitting, tracking, assigning and resolving community service requests. This repository contains the final engineering documentation branch prepared for the SEN381 final submission. The release decision remains conditional on the evidence recorded in the PED and final release register.

## Current project position

- **Backend:** C# / ASP.NET Core Web API on .NET 9
- **Architecture:** 3-tier layered monolith — Controller → Service → Repository
- **Persistence:** MySQL with Dapper and MySqlConnector
- **Schema management:** Version-controlled SQL migration scripts
- **Application host:** Azure App Service (`civicconnect1`).
- **Database host:** Aiven for MySQL hosts separate Staging and Production database environments; Azure hosts the application, not the databases.
- **Current release status:** The team reports that the website is accessible and database connectivity/persistence checks succeeded. This documentation branch does not independently rerun those checks. Recommended decision: staging / conditional no-go for production until the remaining release checks are closed.

## Why these technologies?

| Decision | Reason | Trade-off / condition |
|---|---|---|
| **MySQL (relational)** | Requests, users, categories, statuses, assignments and history have defined relationships. Foreign keys, unique constraints and transactions help protect consistency. | Schema changes need controlled migrations; queries and mappings need tests. |
| **Dapper + MySqlConnector** | Keeps SQL explicit and fits the repository code already in the project without an ORM migration. | The team must maintain SQL, mappings, transactions and concurrency checks itself. |
| **Managed MySQL** | A relational database fits the relationships between users, requests, statuses, assignments and history. | Keep the provider and connection details in deployment configuration; verify TLS, backups and restore procedures without publishing secrets. |
| **Azure App Service** | Managed hosting for the existing ASP.NET Core application, with Azure configuration and deployment workflows. | Record the deployed commit and workflow evidence; continue security and release-readiness checks before production approval. |
| **Layered monolith** | A small team can build and test one deployable application while keeping HTTP, business rules and SQL separate. | Boundaries must be maintained; microservices would add deployment and operational overhead without a current need. |

A document database was considered, but is not the best fit for the current core workflow: many records reference other records, status changes must be consistent, and the system needs history and reporting. MySQL is a project-fit decision, not a claim that document databases are generally inferior.

## Main features and intended roles

- **Requester:** submit a request and follow its progress.
- **Staff:** access the work queue and manage request progress/assignments.
- **Management:** view oversight and reporting information.

The repository includes a browser UI for request submission/history, staff workflows, user management and developer logs, alongside API endpoints for request and account operations. However, login does not issue an authentication token, Reports/Analytics are placeholders, and the completeness of server-side authorization and end-to-end behaviour must be judged from the final RTM and recorded test evidence. A screen or endpoint in source is not, by itself, proof that the requirement passed acceptance testing.

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
- `src/CivicConnect/database/migrations/002_user_accounts.sql` — password-hash field for login
- `src/CivicConnect/database/migrations/005_user_registration_confirmation.sql` — email confirmation fields
- `Tests/CivicConnect.Tests/` — unit, integration and persistence test code
- `src/docs/adr/` — architecture decision records
- `CivicConnect_PED_M3.md` — primary engineering document, updated with the PED v4.0 final-submission and release-decision section
- `src/docs/final-release-evidence.md` — release decision, evidence register and M1 scope-completion worksheet

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
mysql -h localhost -P 3306 -u YOUR_USER -p CivicConnect < ../../src/CivicConnect/database/migrations/002_user_accounts.sql
mysql -h localhost -P 3306 -u YOUR_USER -p CivicConnect < ../../src/CivicConnect/database/migrations/003_rejected_status.sql
mysql -h localhost -P 3306 -u YOUR_USER -p CivicConnect < ../../src/CivicConnect/database/migrations/004_admin_approvals.sql
mysql -h localhost -P 3306 -u YOUR_USER -p CivicConnect < ../../src/CivicConnect/database/migrations/005_user_registration_confirmation.sql
```

### Email registration confirmation

CivicConnect uses Resend to send a confirmation code when a requester signs up. The user enters the code to confirm their email address before using the account. The sending service is configured through application settings rather than hard-coded into the application. The sender identity must be configured for the project, and the Resend credential must be kept private.

For local development, use .NET User Secrets; for Azure, use the App Service application settings. Do not commit real credentials or include them in evidence. The settings used by the application are:

| Setting | Value |
|---|---|
| `Email__Host` | `smtp.resend.com` |
| `Email__Port` | `587` (465 is not supported by .NET's SMTP client) |
| `Email__EnableSsl` | `true` |
| `Email__UserName` | `resend` |
| `Email__Password` | the Resend API key |
| `Email__FromAddress` | an address on a domain verified in Resend |
| `Email__FromName` | optional, defaults to `CivicConnect` |

If the confirmation email can't be sent, registration returns `503` and **no account is created**; the reason is written to the Dev Logs page as `EMAIL_SEND_FAILED`. The app also logs a startup warning if required email settings are missing. Keep real credentials out of source control. ASP.NET Core Data Protection keys must persist across restarts and be shared between app instances in a multi-instance deployment; configure a persistent, protected key ring before deploying confirmation flows.

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
| `POST` | `/api/auth/register` | Email a confirmation code and save an unconfirmed requester account (only if the email was sent) |
| `POST` | `/api/auth/login` | Log in to an active account |
| `POST` | `/api/auth/confirm` | Confirm an account using its email and code |
| `POST` | `/api/auth/resend-confirmation` | Resend a code for an unconfirmed account |
| `POST` | `/api/auth/forgot-password` | Request a password-reset email code |
| `POST` | `/api/auth/verify-reset-code` | Verify the reset code and receive a short-lived reset token |
| `POST` | `/api/auth/reset-password` | Set a new password using the reset token |
| `GET` | `/health` | Application health endpoint |

Registration accepts `FullName`, `Email` and `Password` (at least 10 characters). The account is saved unconfirmed inside a database transaction that only commits once the confirmation email has been accepted by the mail server. Signing up again with the same email before confirming replaces the pending details and sends a new code; an email that already belongs to a confirmed, admin-created or deactivated account returns `409`. Confirmation codes expire after 10 minutes; a code can be tried five times before it must be resent. Signup and code endpoints are rate-limited. A user can log in after confirming their email; login currently returns the user profile and does not issue an authentication token.

Example registration and confirmation requests:

```json
{
  "fullName": "Aiden Example",
  "email": "aiden@example.com",
  "password": "a-long-user-chosen-password"
}
```

```json
{
  "email": "aiden@example.com",
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

The current CI workflow in `.github/workflows/ci.yml` runs on pushes and pull requests targeting `main` or `dev`, not `staging`. The Azure deployment workflow in `.github/workflows/main_civicconnect1.yml` deploys on pushes to `main` or manual dispatch. A documentation commit on `staging` does not itself trigger an Azure deployment.

## Deployment and Database Verification

### Deployed application

- **Environment:** Azure App Service
- **Application URL:** https://civicconnect1-gwasb9d4guchefe9.southafricanorth-01.azurewebsites.net/
- **Status:** The team has confirmed that the site opens successfully after the earlier HTTP 404 issue was resolved.
- **Database configuration:** The database connection credential is stored in Azure application settings and is not committed to the repository.
- **Database provider:** Aiven for MySQL.
- **Database environments:** Aiven hosts separate Staging and Production databases. Use the intended environment for each test and do not use production data for routine development testing.

### Verification recorded by the team

The team has reported successful database connectivity and persistence checks. Treat this as team-reported evidence, not as a substitute for a dated test record showing the exact environment, operation, result and deployed commit. This documentation change does not independently rerun the live checks.

Do not treat this result alone as proof that every API endpoint, role permission, transaction rollback path, encryption setting, backup or restore procedure has been verified.

### Security and data integrity

- Keep database credentials in Azure application settings or the appropriate secret store. Never place credentials in source code, documentation, screenshots or logs.
- Do not record connection strings or secret values in this repository.
- Use the Staging database for pre-production testing and keep Production separate.
- The application uses parameterized Dapper queries. Request creation and status updates use database transaction/concurrency controls in the current implementation; automated failure-path and concurrent-update test evidence should be retained.
- Passwords are hashed by the application. Server-side authentication and role-based authorization still require completion and verification before production approval.
- Confirm database TLS certificate validation and provider-side encryption/backup settings directly in the active hosting/database configuration before documenting them as verified.

### Remaining release checks

- [ ] Record the deployed commit SHA and the successful Azure workflow/run that produced it.
- [ ] Retain the exact steps and results for the database/persistence checks.
- [ ] Test and record rollback behavior when a database operation fails.
- [ ] Test and record concurrent status updates and conflict handling.
- [ ] Verify TLS, backup and restore settings with the active database provider.
- [ ] Complete and test server-side authentication and role-based authorization.
- [ ] Run the relevant automated tests before approving a production release.

## Release evidence still required

- [ ] Record the deployed application commit SHA and successful workflow run.
- [ ] Keep evidence of the database/persistence checks, including the test steps and results.
- [ ] Verify database TLS configuration and document the provider's backup/restore arrangements.
- [ ] Run and record the relevant unit, API integration and database-backed tests.
- [ ] Verify authentication and authorization end to end. Login currently returns a user profile but does not issue an authentication token; do not claim token-based session security or complete server-side RBAC until implemented and tested.
- [ ] Complete and verify staff assignment and management reporting before claiming those features are ready.

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
- [Final release evidence register](src/docs/final-release-evidence.md)
- [Final presentation and individual defence guide](src/docs/final-presentation-and-defence.md)
- [SQL migration](src/CivicConnect/database/migrations/001_initial_schema.sql)
