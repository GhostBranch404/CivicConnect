# ADR-003: CivicConnect Persistence and Data-Integrity Strategy

## Status
Accepted as the current persistence design; execution evidence remains outstanding

## Context
CivicConnect stores service requests, requester details, controlled categories and statuses, assignments, request history and developer/application logs. Several business operations require more than one database write. For example, creating a request should also create an initial history entry and developer log. Updating status should update the request and record the change. If only part of such an operation succeeds, the database becomes inconsistent.

## Decision
CivicConnect will use MySQL with Dapper and MySqlConnector.

Business rules remain in the service layer, while repository methods execute parameterised SQL and control database transactions where an operation spans multiple statements.

MySQL will enforce structural integrity through:

- primary keys;
- foreign keys;
- NOT NULL constraints;
- UNIQUE constraints;
- controlled reference tables for categories and request statuses.

Multi-step writes will use a single MySQL transaction. If any required statement fails, the transaction is rolled back.

Optimistic concurrency will be implemented on `ServiceRequests` using an integer `Version` column. Updates will include the expected version in the WHERE clause and increment the version only when the update succeeds.

Important request lifecycle events will be written to `RequestHistory`. Developer/application activity required for traceability will be written to `DeveloperLogs`.

## Example transactional operation

Creating a request:

1. Insert `ServiceRequests`.
2. Retrieve the generated request ID.
3. Insert the initial `RequestHistory` entry.
4. Insert the related `DeveloperLogs` entry.
5. Commit if every statement succeeds; otherwise roll back.

## Consequences
### Positive
- Prevents partial request/history/log updates.
- Keeps workflow rules outside SQL stored procedures.
- Preserves explicit SQL control while maintaining layered separation.
- Makes stale concurrent updates detectable.
- Supports lecturer-requested database logging and request auditability.

### Negative
- Transaction and concurrency code must be implemented deliberately.
- Dapper requires manual SQL and mapping maintenance.
- Version-controlled SQL migrations must be maintained by the team.

## Verification status

The repository contains persistence implementation and test methods for create/retrieve, invalid references, duplicate tickets, transactional rollback and optimistic concurrency. The existence of these methods does not establish that they passed. Record a successful database-backed run and the relevant output before claiming the behaviour is verified. The current hosted provider connection and live migration result also remain to be evidenced.

## Evidence
- `../../CivicConnect/database/migrations/001_initial_schema.sql`
- `../../CivicConnect/Data/RequestRepository.cs`
- `../../CivicConnect/Models/RequestHistory.cs`
- `../../CivicConnect/Models/DeveloperLog.cs`
- `../database-design.md`
- `../diagrams/CivicConnect_ERD.md`
