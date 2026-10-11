# CivicConnect Database Design

**Document type:** Database / Persistence Design  
**Project:** CivicConnect – Community Service Request Management Platform  
**Milestone:** Milestone 2  
**Persistence baseline:** MySQL + Dapper + MySqlConnector  
**Owner:** Jaco – Database/Persistence Lead  

---

## 1. Purpose

This document defines the M2 database and persistence baseline for CivicConnect. It converts the project requirements and Assignment 2 persistence research into a concrete relational design that can be implemented, reviewed and verified in the repository.

The database must support the core request lifecycle:

1. a registered requester submits a service request;
2. the system stores a unique Ticket Number and initial status;
3. staff can view and assign requests;
4. authorised staff can update request status and add resolution information;
5. important request changes are recorded in `RequestHistory`;
6. application/developer activity required for engineering traceability is recorded in `DeveloperLogs`;
7. management can query request, status and assignment data for oversight.

The design deliberately separates business rules from structural database protection. Service-layer code applies business validation and workflow decisions. The repository layer performs parameterised Dapper queries and coordinates transactions. MySQL enforces keys, required values, uniqueness and relational consistency.

---

## 2. Technology and Data-Access Decision

The current M2 persistence baseline is:

- **Database:** MySQL
- **Backend:** C# / ASP.NET Core
- **Data access:** Dapper
- **Driver:** MySqlConnector
- **Schema evolution:** version-controlled SQL migration scripts
- **Architecture:** Controller → Service → Repository → MySQL

Entity Framework Core is **not** part of the current baseline. The team selected Dapper because Request Management is already implemented using repository-based explicit SQL and changing persistence technology at this stage would add unnecessary rework.

Dapper does not remove the need for architecture boundaries. Controllers must not contain database queries. SQL belongs in the repository/data-access layer, while workflow validation remains in the service layer.

---

## 3. Requirements Driving the Database

| Requirement | Persistence impact |
|---|---|
| **FR-001 – Service Request Submission** | Store requester, title, description, location, category, status and unique ticket number. |
| **FR-002 – Request Tracking & History** | Store current request state and an ordered lifecycle history. |
| **FR-003 – Request Assignment** | Store current and previous staff assignments with timestamps. |
| **FR-004 – Status Transition and Resolution** | Store status changes, resolution notes and related audit/history records. |
| **FR-005 – Dashboard Oversight** | Support efficient queries by status, category, date and assignment. |
| **RBAC** | Persist a logical user/role identity or integrate with the final authentication identity model. |
| **Auditability** | Record actor, request, action/state and timestamp for important request lifecycle events. |
| **Data integrity** | Prevent invalid references and partial multi-table operations. |
| **Developer/application logging** | Persist selected engineering/application trace events in `DeveloperLogs`. |

---

## 4. ERD

The editable M2 ERD is stored in:

`docs/diagrams/CivicConnect_ERD.md`

The previously exported PNG remains supporting visual evidence. The updated ERD includes the lecturer-requested `DeveloperLogs` entity.

---

## 5. Entity Summary

| Entity | Purpose |
|---|---|
| `Users` | Logical CivicConnect users and role context. |
| `Categories` | Controlled request categories. |
| `RequestStatuses` | Controlled statuses: Pending, In Progress and Resolved. |
| `ServiceRequests` | Current state of each CivicConnect ticket. |
| `Assignments` | Staff responsibility and reassignment history. |
| `RequestComments` | General notes and resolution information. |
| `RequestHistory` | Business/audit history of important request lifecycle events. |
| `DeveloperLogs` | Application/developer trace events required for engineering visibility and troubleshooting. |

---

## 6. Data Dictionary

### 6.1 Users

| Field | Type | Constraint | Purpose |
|---|---|---|---|
| `UserId` | INT | PK, AUTO_INCREMENT | Internal user identifier |
| `FullName` | VARCHAR(150) | NOT NULL | User display name |
| `Email` | VARCHAR(255) | NOT NULL, UNIQUE | User email |
| `Role` | VARCHAR(50) | NOT NULL | Requester, Staff or Management context |
| `IsActive` | BOOLEAN | NOT NULL, default TRUE | Soft lifecycle flag |
| `CreatedAt` | DATETIME | NOT NULL | Creation timestamp |

The final authentication implementation may replace or map this logical entity to framework-managed identity tables. Password storage must not be invented independently in this table.

### 6.2 Categories

| Field | Type | Constraint | Purpose |
|---|---|---|---|
| `CategoryId` | INT | PK, AUTO_INCREMENT | Category identifier |
| `CategoryName` | VARCHAR(100) | NOT NULL, UNIQUE | Controlled category name |
| `IsActive` | BOOLEAN | NOT NULL | Allows retirement without destroying history |

Initial development categories are Maintenance, IT Support, Security, Facilities and Other. These are seed values and can be changed through a controlled team decision.

### 6.3 RequestStatuses

| Field | Type | Constraint | Purpose |
|---|---|---|---|
| `StatusId` | INT | PK, AUTO_INCREMENT | Status identifier |
| `StatusName` | VARCHAR(50) | NOT NULL, UNIQUE | Controlled status label |

M2 seed values are:

1. Pending
2. In Progress
3. Resolved

Additional workflow states should only be introduced when the team deliberately baselines them.

### 6.4 ServiceRequests

| Field | Type | Constraint | Purpose |
|---|---|---|---|
| `RequestId` | INT | PK, AUTO_INCREMENT | Internal request identifier |
| `TicketNumber` | VARCHAR(30) | NOT NULL, UNIQUE | User-facing ticket reference |
| `RequesterId` | INT | FK → Users | Request owner |
| `CategoryId` | INT | FK → Categories | Request category |
| `StatusId` | INT | FK → RequestStatuses | Current request status |
| `Title` | VARCHAR(200) | NOT NULL | Short request title |
| `Description` | TEXT | NOT NULL | Full issue description |
| `Location` | VARCHAR(255) | NOT NULL | Request location |
| `RequestDate` | DATETIME | NOT NULL | Submission timestamp |
| `UpdatedAt` | DATETIME | NOT NULL | Last state-change timestamp |
| `Version` | INT | NOT NULL, default 1 | Optimistic-concurrency token |

`ServiceRequests` stores the **current state** of the ticket. Historical state changes belong in `RequestHistory`.

### 6.5 Assignments

| Field | Type | Constraint | Purpose |
|---|---|---|---|
| `AssignmentId` | INT | PK, AUTO_INCREMENT | Assignment identifier |
| `RequestId` | INT | FK → ServiceRequests | Assigned request |
| `StaffUserId` | INT | FK → Users | Responsible staff member |
| `AssignedAt` | DATETIME | NOT NULL | Assignment timestamp |
| `UnassignedAt` | DATETIME | NULL | End of responsibility |
| `IsCurrent` | BOOLEAN | NOT NULL | Indicates active assignment |

Assignments are kept in a separate table so reassignment does not destroy previous responsibility evidence.

### 6.6 RequestComments

| Field | Type | Constraint | Purpose |
|---|---|---|---|
| `CommentId` | INT | PK, AUTO_INCREMENT | Comment identifier |
| `RequestId` | INT | FK → ServiceRequests | Parent request |
| `UserId` | INT | FK → Users | Comment author |
| `CommentText` | TEXT | NOT NULL | Comment/resolution content |
| `CommentType` | VARCHAR(30) | NOT NULL | General, Resolution, etc. |
| `CreatedAt` | DATETIME | NOT NULL | Timestamp |

A request changed to `Resolved` must provide resolution notes according to FR-004. The service layer validates this before persistence.

### 6.7 RequestHistory

| Field | Type | Constraint | Purpose |
|---|---|---|---|
| `HistoryId` | INT | PK, AUTO_INCREMENT | History identifier |
| `RequestId` | INT | FK → ServiceRequests | Affected request |
| `ChangedByUserId` | INT | FK → Users | User responsible for change |
| `OldStatusId` | INT | nullable FK | Previous status |
| `NewStatusId` | INT | nullable FK | New status |
| `ActionType` | VARCHAR(50) | NOT NULL | RequestCreated, StatusChanged, etc. |
| `Notes` | TEXT | NULL | Additional detail |
| `ChangedAt` | DATETIME | NOT NULL | Event time |

`RequestHistory` is business/audit evidence. It should be append-oriented and important lifecycle changes should write history within the same transaction as the request change.

### 6.8 DeveloperLogs

| Field | Type | Constraint | Purpose |
|---|---|---|---|
| `LogId` | BIGINT | PK, AUTO_INCREMENT | Log identifier |
| `RequestId` | INT | nullable FK → ServiceRequests | Related request where applicable |
| `Actor` | VARCHAR(150) | NOT NULL | Application/user/developer actor label |
| `Action` | VARCHAR(100) | NOT NULL | CREATE, UPDATE_STATUS, ERROR, etc. |
| `EntityType` | VARCHAR(100) | NOT NULL | Affected logical entity |
| `EntityId` | VARCHAR(100) | NULL | Affected identifier |
| `Severity` | VARCHAR(30) | NOT NULL | Information, Warning, Error |
| `Details` | TEXT | NULL | Human-readable diagnostic detail |
| `LoggedAt` | DATETIME | NOT NULL | Log timestamp |

#### Developer logging purpose

The lecturer requested developer logging in the database. This table provides persistent technical/application trace evidence for selected operations. It does **not** replace request audit history.

Examples worth storing:

- request successfully created;
- status update completed;
- assignment operation completed;
- persistence error caught at an application boundary;
- controlled administrative operation.

The system should avoid logging passwords, tokens, connection strings or other secrets. It also should not log every trivial UI interaction.

---

## 7. Relationships

| Parent | Child | Cardinality |
|---|---|---|
| Users | ServiceRequests | 1:M |
| Categories | ServiceRequests | 1:M |
| RequestStatuses | ServiceRequests | 1:M |
| ServiceRequests | Assignments | 1:M |
| Users | Assignments | 1:M |
| ServiceRequests | RequestComments | 1:M |
| Users | RequestComments | 1:M |
| ServiceRequests | RequestHistory | 1:M |
| Users | RequestHistory | 1:M |
| ServiceRequests | DeveloperLogs | 1:M / optional relationship |

---

## 8. Data Ownership

| Data | Logical owner | Responsibility |
|---|---|---|
| Users/roles | Authentication/Authorisation | Identity and role context |
| Categories | Request Management | Classification |
| ServiceRequests | Request Management | Core ticket lifecycle |
| Assignments | Request Management | Staff responsibility |
| RequestComments | Request Management | Notes and resolution |
| RequestHistory | Audit/History | Business lifecycle trace |
| DeveloperLogs | Application/Engineering logging | Technical trace/troubleshooting |
| RequestStatuses | Shared reference data | Controlled status vocabulary |

All tables currently live in the same MySQL database. Logical ownership prevents unrelated modules from bypassing their service/repository boundaries.

---

## 9. Integrity Controls

### Primary keys
Every persistent table has a stable primary key.

### Foreign keys
Foreign keys prevent orphaned records, for example:

- `ServiceRequests.RequesterId → Users.UserId`
- `ServiceRequests.CategoryId → Categories.CategoryId`
- `ServiceRequests.StatusId → RequestStatuses.StatusId`
- `RequestHistory.RequestId → ServiceRequests.RequestId`
- `Assignments.StaffUserId → Users.UserId`

### Required values
`NOT NULL` is used where absence would make the record invalid, including title, description, location, status and major timestamps.

### Uniqueness
Unique constraints protect:

- user email;
- ticket number;
- category name;
- request-status name.

### Parameterised SQL
All values originating from application input must be passed to Dapper as parameters. SQL strings must not be constructed by concatenating raw user input.

---

## 10. Transaction Strategy

Multi-step operations are treated as a single logical transaction.

### Create request

1. insert `ServiceRequests`;
2. retrieve `LAST_INSERT_ID()`;
3. insert initial `RequestHistory`;
4. insert `DeveloperLogs` trace;
5. commit.

If any required statement fails, roll back the entire operation.

### Change request status

1. lock/read the current request state;
2. verify the expected `Version`;
3. resolve the target controlled status;
4. update request status and increment `Version`;
5. insert `RequestHistory`;
6. insert `DeveloperLogs` trace;
7. commit.

This prevents the current request state from changing without its corresponding trace records.

---

## 11. Optimistic Concurrency

`ServiceRequests.Version` is used to detect stale updates.

Example:

1. Staff A reads version 3.
2. Staff B reads version 3.
3. Staff A updates successfully; version becomes 4.
4. Staff B tries to update using expected version 3.
5. CivicConnect rejects the stale update instead of silently overwriting Staff A's work.

The update SQL includes both `RequestId` and the expected `Version` and increments the version on success.

---

## 12. Indexing

Initial indexes are deliberately limited to known access paths:

- `ServiceRequests.RequesterId`
- `ServiceRequests.CategoryId`
- `ServiceRequests.StatusId`
- `ServiceRequests.RequestDate`
- `Assignments.RequestId`
- `Assignments.StaffUserId`
- `(Assignments.RequestId, IsCurrent)`
- `RequestHistory.RequestId`
- `(RequestHistory.RequestId, ChangedAt)`
- `DeveloperLogs.RequestId`
- `DeveloperLogs.LoggedAt`
- `DeveloperLogs.Severity`

Additional indexes should be justified by actual query requirements instead of being added speculatively.

---

## 13. Schema Evolution

Dapper does not generate migrations, therefore CivicConnect uses version-controlled SQL migration scripts.

Current baseline:

`../CivicConnect/database/migrations/001_initial_schema.sql`

Workflow:

```text
Schema/design change
    ↓
Create numbered SQL migration
    ↓
Peer review migration
    ↓
Commit/PR
    ↓
Apply to development database
    ↓
Verify application behaviour
```

Manual database changes that are not represented in the repository should be avoided because they create schema drift between developers.

---

## 14. Configuration and Secrets

Connection credentials must not be committed to GitHub.

Do not commit:

- `.env` files containing live secrets;
- database passwords;
- Oracle credentials;
- access tokens;
- private keys;
- production connection strings.

The application obtains `ConnectionStrings:DefaultConnection` through normal ASP.NET configuration. Local secrets should be supplied through User Secrets, environment variables or another approved secret mechanism.

---

## 15. Availability, SPOF and Recovery

The current single MySQL database is an architectural single point of failure. If it becomes unavailable, CivicConnect cannot safely create or update requests.

For M2 the risk is recognised rather than over-engineered. Before production deployment, the team must confirm:

- hosting availability features;
- backup frequency;
- backup retention;
- restore process;
- recovery ownership;
- acceptable recovery time and recovery point.

The application should fail safely when persistence is unavailable and must not report a database-changing operation as successful when it was not committed.

---

## 16. Initial Verification

| Check | Expected result |
|---|---|
| Valid service request | Saved with unique Ticket Number |
| Invalid requester/category/status FK | Rejected by database |
| Duplicate ticket number | Rejected by UNIQUE constraint |
| Request creation | Request + history + developer log commit together |
| Failure during multi-write | Entire transaction rolls back |
| Status update | Current status and history/log update together |
| Stale version | Update rejected as concurrency conflict |
| Request list | Ordered by `RequestDate` and maps category/status names correctly |
| Resolved without notes | Service layer rejects operation |

---

## 17. Requirement Traceability

| Requirement | Main persistence evidence |
|---|---|
| FR-001 | `ServiceRequests`, unique `TicketNumber`, request-creation transaction |
| FR-002 | `ServiceRequests` + `RequestHistory` |
| FR-003 | `Assignments` + history entries |
| FR-004 | status FK, comments/resolution, history and transaction handling |
| FR-005 | indexed request/status/category/assignment data |
| Auditability | `RequestHistory` |
| Developer logging | `DeveloperLogs` |
| Data integrity | constraints + transaction rollback + concurrency version |

---

## 18. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Partial multi-table update | High | MySQL transaction + rollback |
| Invalid relational data | High | PK/FK/NOT NULL/UNIQUE constraints |
| Lost concurrent update | Medium/High | `Version` optimistic concurrency |
| Schema drift | Medium | numbered SQL migrations in Git |
| Credential exposure | High | secrets outside repository |
| Raw SQL coupling | Medium | SQL restricted to repository layer |
| Mapping mismatch | Medium | explicit aliases, tests and review |
| Database outage | High | controlled failure + later hosting/backup baseline |
| Excessive/noisy developer logs | Medium | log selected engineering events only |
| Sensitive information in logs | High | prohibit secrets/passwords/tokens in log details |

---

## 19. Open Decisions

The following remain controlled future decisions rather than assumptions hidden in code:

- final authentication/identity schema;
- final production MySQL hosting configuration;
- backup/recovery targets;
- exact category list;
- any statuses beyond Pending/In Progress/Resolved;
- long-term data retention/deletion policy;
- final developer-log retention policy;
- final human-readable ticket-number format.

---

## 20. Repository Evidence

The database baseline is represented through:

```text
src/CivicConnect/
├── Data/
│   ├── IRequestRepository.cs
│   └── RequestRepository.cs
├── Models/
│   ├── ServiceRequest.cs
│   ├── RequestHistory.cs
│   └── DeveloperLog.cs
├── database/
│   └── migrations/
│       └── 001_initial_schema.sql
src/docs/
├── database-design.md
├── diagrams/
│   └── CivicConnect_ERD.md
└── adr/
    ├── ADR-002_Tech_stack_and_persistence.md
    └── ADR-003_data_persistence.md
```

This evidence must remain consistent with the implementation as the project evolves.
