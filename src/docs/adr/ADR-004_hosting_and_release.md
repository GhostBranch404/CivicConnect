# ADR-004: Hosting and Release Environment

## Status
Accepted hosting direction. The application is reported accessible and the team reports successful database connectivity/persistence checks. Production readiness is still not approved pending the release checks below.

## Context
CivicConnect needs a reachable ASP.NET Core application and a separate persistent relational database. The team is small, has a student cloud budget, and needs time for implementation and verification rather than managing servers. The database engine choice (MySQL) is separate from the providers used to host the application and database.

## Decision
- **Application hosting target:** Azure App Service, web app `civicconnect1`, configured for .NET 9.
- **Database hosting:** Aiven for MySQL hosts both the separate Staging and Production databases. Azure App Service hosts the application, not the databases.
- **Database engine:** MySQL, accessed by Dapper and MySqlConnector.
- **Release automation:** `.github/workflows/main_civicconnect1.yml` builds/publishes and deploys on pushes to `main` or manual dispatch. `.github/workflows/ci.yml` runs tests on pushes and pull requests targeting `main` and `dev`; it does not currently trigger on `staging`.

These are the current project decisions. The team reports that the deployed site is accessible and database connectivity/persistence checks succeeded; this document does not independently reproduce those checks. The staging branch is the working branch for this documentation update and is not automatically deployed by the current Azure workflow.

## Why Azure App Service?
App Service provides managed hosting for the existing ASP.NET Core application and integrates with GitHub Actions. This avoids the extra operating and deployment complexity of managing a VM or Kubernetes cluster for a small student project. A container platform remains an option if future requirements need it, but no current requirement justifies Kubernetes-level control.

## Why Aiven for MySQL?
Aiven offers managed MySQL and reduces direct database-server administration. It preserves the MySQL engine and existing schema, unlike changing database technology. Alternatives considered include Amazon RDS for MySQL and DigitalOcean Managed MySQL. They can also host MySQL, but would introduce a different provider configuration and potentially different cost/networking arrangements. The selected plan's TLS, connectivity, backup/restore, limits and total cost must be checked; no provider is assumed to be production-grade merely because it is managed.

## Why a relational database instead of a document database?
Requests link to users, categories, statuses, assignments and history. Foreign keys, uniqueness, joins and transactions are useful for keeping these linked records consistent. A document database can suit flexible, self-contained records, but would not remove the need to enforce the current cross-record rules. MySQL is therefore the simpler fit for the requirements presently documented.

## Consequences and trade-offs
**Benefits**
- Managed application hosting and managed database reduce server administration.
- The existing .NET, MySQL and Dapper implementation can be retained.
- Application hosting and database hosting can be scaled or changed independently.

**Trade-offs / risks**
- Two cloud services mean separate credentials, network/TLS settings, billing and troubleshooting.
- Aiven plan limits, connectivity, backup/restore and cost remain to be verified.
- A successful deployment workflow does not prove that routes or database operations work.
- Azure's current deployment workflow targets `main`; a change made only on `staging` is not automatically deployed.
- The 404 responses for `/` and `/swagger` recorded on 15 October 2026 are historical and were subsequently reported as resolved. Record the exact deployed commit and successful workflow run, plus a current API/database operation, so release evidence is traceable.

## Verification required before release
- Record the deployed commit SHA and corresponding GitHub Actions run.
- Verify homepage, `/health`, and one real `/api/requests` response.
- Verify the app's configured database provider without exposing secrets.
- Apply/confirm the migration and create/retrieve/update a test request; verify RequestHistory and DeveloperLogs.
- Record test output, date, environment and evidence links.
- Verify secret storage, TLS, encryption-at-rest, backup/restore, plan limits and costs directly with the providers.
- Confirm authentication token/session behaviour and server-side role restrictions; the current login endpoint does not issue an authentication token.

## Evidence links
- [README](../../../README.md)
- [Azure deployment workflow](../../../.github/workflows/main_civicconnect1.yml)
- [CI workflow](../../../.github/workflows/ci.yml)
- [Initial schema migration](../../../src/CivicConnect/database/migrations/001_initial_schema.sql)
- [Database design](../database-design.md)
- [Dapper/persistence decision](ADR-002_Tech_stack_and_persistence.md)
