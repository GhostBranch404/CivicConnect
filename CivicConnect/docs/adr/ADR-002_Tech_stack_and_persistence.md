# ADR-002: Technology Stack and Data Access Layer Alignment

## Status
Accepted

## Context
CivicConnect requires a maintainable C# backend, relational MySQL persistence and a data-access approach that is understandable to the three-person team within the milestone schedule. The project already contains a repository/service/controller structure and Dapper-based request persistence, so the M2 baseline should align the documented decision with the implementation rather than introduce a second ORM approach.

## Decision
The team will use the following baseline:

- **Backend:** C# / ASP.NET Core Web API
- **Database:** MySQL
- **Data access:** Dapper
- **MySQL driver:** MySqlConnector
- **Architecture:** 3-tier layered monolith using Controller -> Service -> Repository -> MySQL
- **Schema evolution:** version-controlled SQL migration scripts

Entity Framework Core is not part of the current M2 persistence baseline.

## Rationale
### C# / ASP.NET Core
The team already works in C#, and ASP.NET Core provides the web runtime, dependency injection and HTTP pipeline needed by CivicConnect.

### MySQL
CivicConnect data is strongly relational. Requests reference requesters, categories, statuses, assignments and history records, so foreign keys, unique constraints and transactions are useful structural safeguards.

### Dapper
Dapper fits the existing implementation and keeps SQL explicit inside the repository layer. Parameterised queries are required for user-controlled values. Business validation and transaction coordination remain outside controllers and are handled through service/repository responsibilities.

Dapper does not automatically manage schema changes or object tracking, so the team must maintain version-controlled SQL migration scripts and review SQL carefully.

## Consequences
### Positive
- Matches the code already implemented by the team.
- Keeps persistence simple and explicit for the current project size.
- Avoids an unnecessary mid-milestone migration to another persistence framework.
- Supports parameterised queries, transactions and clear repository boundaries.
- SQL migration scripts can be reviewed and version controlled.

### Negative
- More SQL must be written and maintained manually.
- Schema changes are not generated automatically.
- Mapping mistakes between C# properties and SQL columns are possible and require tests/review.
- The team must deliberately implement transaction and concurrency behaviour.

## Implementation evidence
- `Data/RequestRepository.cs`
- `Models/ServiceRequest.cs`
- `database/migrations/001_initial_schema.sql`
- `docs/database-design.md`
- `ADR-003_data_persistence.md`
