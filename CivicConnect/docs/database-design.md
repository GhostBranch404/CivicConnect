# CivicConnect Database Design

**Document type:** Database Persistence Design  
**Project:** CivicConnect – Community Service Request Management Platform  
---

## 1. Purpose

This document defines the initial database and persistence design for CivicConnect. It translates the approved CivicConnect requirements and the persistence decisions researched in Assignment 2 into a project-specific data model that can be implemented and verified during Milestone 2.

The design is intended to support the main CivicConnect workflow:

1. a registered requester submits a service request;
2. the request receives a unique ticket number and initial status;
3. staff can view and assign requests;
4. authorised staff can update request status and add resolution information;
5. important request changes are recorded in a traceable history;
6. management can later query request information for oversight and reporting.

The database design is not intended to contain business logic that belongs in the application layer. CivicConnect will use the application/service layer to coordinate business validation, transactions and workflow rules, while MySQL provides structural safeguards such as primary keys, foreign keys, required fields and uniqueness constraints.

---

## 2. Scope

### 2.1 In scope for the M2 database baseline

This document covers:

- the main CivicConnect data entities;
- entity relationships;
- primary and foreign keys;
- data ownership;
- lifecycle implications;
- structural integrity rules;
- transaction boundaries;
- validation responsibilities;
- audit/history persistence;
- optimistic concurrency;
- indexing considerations;
- migrations and schema control;
- database configuration and secret handling;
- initial availability, backup/recovery and single-point-of-failure considerations;
- database verification;
- requirement traceability;
- database-related risks and assumptions.

### 2.2 Not yet treated as final

The following are not considered fully final at this stage:

- production hosting configuration;
- final backup retention periods;
- final database sizing and performance tuning;
- final dashboard/reporting query optimisation;
- final identity-provider implementation;
- final deletion/retention policy;
- final disaster-recovery targets;
- final production monitoring.

These items should be revisited as CivicConnect progresses beyond the M2 baseline.

---

## 3. Source Requirements Driving the Data Model

The database model is based on the current CivicConnect requirements.

| Requirement | Database implication |
|---|---|
| **FR-001 – Service Request Submission** | A registered requester must be able to submit a request containing a title, description, location and category. The system must generate and store a unique Ticket ID. |
| **FR-002 – Request Tracking & History** | Requesters must be able to view their previous requests and the current status of each request. |
| **FR-003 – Request Assignment** | Staff must be able to assign incoming requests to themselves or another suitable staff member. Assignment and acknowledgement changes must be recorded. |
| **FR-004 – Status Transition and Resolution** | Authorised staff must be able to update request progress and record resolution notes. |
| **FR-005 – Dashboard Oversight** | Management must be able to obtain request-volume, status and staff-related information for oversight. |
| **RBAC requirement** | The system distinguishes Requester, Staff and Management responsibilities. |
| **Auditability requirement** | Important actions must be traceable to a user, request and timestamp. |
| **Data-integrity requirement** | Request, assignment, history and status information must remain consistent even where an operation requires multiple writes. |

---

## 4. Persistence Approach

### 4.1 Selected direction

CivicConnect will use a relational **MySQL** database.

The application will access persistence through the selected C#/.NET data-access approach rather than placing raw SQL directly inside controllers. The current project direction is to use **Entity Framework Core** as the persistence abstraction, with version-controlled migrations used to evolve the database schema.

### 4.2 Responsibility split

CivicConnect uses a multi-tier validation and integrity strategy:

**Presentation layer**
- provides immediate formatting and required-field feedback;
- prevents obviously incomplete forms where practical.

**Application/service layer**
- applies CivicConnect business rules;
- controls valid workflow transitions;
- performs authorisation checks;
- coordinates multi-step operations;
- begins/commits/rolls back transactions;
- handles concurrency conflicts.

**Database layer**
- enforces primary keys;
- enforces foreign keys;
- enforces required values;
- enforces uniqueness;
- preserves relational consistency.

This avoids moving application workflow logic into stored procedures while still ensuring that invalid relational data cannot be inserted freely.

---

## 5. ERD in "docs" folder.

---

## 6. Entity Definitions and Data Dictionary

### 6.1 Users

**Purpose:** Stores the CivicConnect user identity required by service-request, assignment, comment and audit relationships.

> Note: the final authentication mechanism may introduce framework-managed identity tables. The `Users` structure below represents the current logical CivicConnect user entity and may later map to or be replaced by the selected identity framework's tables.

| Field | Proposed type | Null? | Key/constraint | Purpose |
|---|---|---:|---|---|
| `UserId` | INT | No | PK, auto increment | Internal user identifier |
| `FullName` | VARCHAR(150) | No |  | Display/user name |
| `Email` | VARCHAR(255) | No | UNIQUE | Login/contact identity |
| `Role` | VARCHAR(50) | No | controlled by application | Requester, Staff or Management |
| `IsActive` | BOOLEAN | No | default TRUE | Allows account deactivation without deleting history |
| `CreatedAt` | DATETIME | No |  | Account creation timestamp |

**Rules**
- `Email` must be unique.
- Historical rows should not be broken when a user is deactivated.
- The application must control valid roles.
- The final identity design must avoid storing passwords directly in this table unless the selected identity framework explicitly manages secure password hashes.

### 6.2 Categories

**Purpose:** Provides a controlled classification for service requests.

| Field | Proposed type | Null? | Key/constraint | Purpose |
|---|---|---:|---|---|
| `CategoryId` | INT | No | PK, auto increment | Category identifier |
| `CategoryName` | VARCHAR(100) | No | UNIQUE | Human-readable category |
| `IsActive` | BOOLEAN | No | default TRUE | Allows categories to be retired without deleting historical references |

**Example initial values**
- Maintenance
- IT Support
- Security
- Facilities
- Other

These values are examples and should be confirmed by the team before being baselined.

### 6.3 RequestStatuses

**Purpose:** Provides a controlled set of valid request-status values.

| Field | Proposed type | Null? | Key/constraint | Purpose |
|---|---|---:|---|---|
| `StatusId` | INT | No | PK, auto increment | Status identifier |
| `StatusName` | VARCHAR(50) | No | UNIQUE | Status label |

**Current requirement-supported values**
- Pending
- In Progress
- Resolved

Additional values such as Assigned, Closed or Rejected should only be added if the team approves them as project-specific workflow decisions.

### 6.4 ServiceRequests

**Purpose:** Main persistent entity representing a CivicConnect service request/ticket.

| Field | Proposed type | Null? | Key/constraint | Purpose |
|---|---|---:|---|---|
| `ServiceRequestId` | INT | No | PK, auto increment | Internal relational identifier |
| `TicketNumber` | VARCHAR(30) | No | UNIQUE | User-facing ticket reference |
| `RequesterId` | INT | No | FK → `Users.UserId` | Request owner/requester |
| `CategoryId` | INT | No | FK → `Categories.CategoryId` | Request classification |
| `StatusId` | INT | No | FK → `RequestStatuses.StatusId` | Current request status |
| `Title` | VARCHAR(200) | No |  | Short request title |
| `Description` | TEXT | No |  | Full issue description |
| `Location` | VARCHAR(255) | No |  | Location supplied for the request |
| `CreatedAt` | DATETIME | No |  | Submission time |
| `UpdatedAt` | DATETIME | No |  | Last modification time |
| `Version` | INT | No | default 1 | Optimistic-concurrency token |

**Rules**
- A request cannot exist without a valid requester.
- A request cannot exist without a valid category.
- A request cannot exist without a valid status.
- `TicketNumber` must be unique.
- The application determines the initial status.
- A resolved request must satisfy the approved resolution-note rule before the transaction is committed.
- `Version` must change when protected mutable state changes.

### 6.5 Assignments

**Purpose:** Records responsibility for service requests over time.

A separate assignment table is used instead of storing only a single staff ID directly on `ServiceRequests`. This preserves reassignment history and allows CivicConnect to determine who was responsible for the request at different points in its lifecycle.

| Field | Proposed type | Null? | Key/constraint | Purpose |
|---|---|---:|---|---|
| `AssignmentId` | INT | No | PK, auto increment | Assignment record |
| `ServiceRequestId` | INT | No | FK → `ServiceRequests.ServiceRequestId` | Assigned request |
| `StaffUserId` | INT | No | FK → `Users.UserId` | Assigned staff member |
| `AssignedAt` | DATETIME | No |  | Assignment timestamp |
| `UnassignedAt` | DATETIME | Yes |  | Time responsibility ended |
| `IsCurrent` | BOOLEAN | No | default TRUE | Identifies active assignment |

**Rules**
- The application must ensure that the selected assignee is authorised as staff.
- A request should have no more than one current assignment unless the team explicitly changes the business rule.
- Previous assignment records should be retained for traceability.
- Reassignment should close the old assignment and create a new assignment inside a controlled operation.

### 6.6 RequestComments

**Purpose:** Stores notes associated with a request, including resolution information.

| Field | Proposed type | Null? | Key/constraint | Purpose |
|---|---|---:|---|---|
| `CommentId` | INT | No | PK, auto increment | Comment identifier |
| `ServiceRequestId` | INT | No | FK → `ServiceRequests.ServiceRequestId` | Parent request |
| `UserId` | INT | No | FK → `Users.UserId` | Author |
| `CommentText` | TEXT | No |  | Comment/resolution text |
| `CommentType` | VARCHAR(30) | No | application-controlled | e.g. General, Resolution |
| `CreatedAt` | DATETIME | No |  | Comment timestamp |

**Rules**
- Empty comments must not be persisted.
- A resolution comment must be present where the FR-004 resolution acceptance criteria require it.
- The application controls whether a user may create a particular comment type.

### 6.7 RequestHistory

**Purpose:** Provides the persistent audit/history trail for important request lifecycle changes.

| Field | Proposed type | Null? | Key/constraint | Purpose |
|---|---|---:|---|---|
| `HistoryId` | INT | No | PK, auto increment | History record identifier |
| `ServiceRequestId` | INT | No | FK → `ServiceRequests.ServiceRequestId` | Affected request |
| `ChangedByUserId` | INT | No | FK → `Users.UserId` | Responsible user |
| `OldStatusId` | INT | Yes | FK → `RequestStatuses.StatusId` | Previous status, if applicable |
| `NewStatusId` | INT | Yes | FK → `RequestStatuses.StatusId` | New status, if applicable |
| `ActionType` | VARCHAR(50) | No | application-controlled | Action performed |
| `Notes` | TEXT | Yes |  | Additional change detail |
| `ChangedAt` | DATETIME | No |  | Change timestamp |

**Typical `ActionType` examples**
- RequestCreated
- RequestAssigned
- RequestReassigned
- StatusChanged
- ResolutionAdded

These values should be treated as a controlled application set rather than arbitrary user input.

**Rules**
- History entries must reference a valid request and responsible user.
- History should be appended rather than casually edited.
- Important lifecycle changes should write their history entry as part of the same logical transaction as the state change.
- Request Management should communicate with the Audit/History responsibility through the agreed application boundary rather than scattering direct audit-table access through controllers.

---

## 7. Relationships and Cardinality

| Parent | Child | Relationship | Reason |
|---|---|---|---|
| Users | ServiceRequests | 1-to-many | One requester may submit many requests |
| Categories | ServiceRequests | 1-to-many | One category may classify many requests |
| RequestStatuses | ServiceRequests | 1-to-many | Many requests may share the same current status |
| ServiceRequests | Assignments | 1-to-many | A request may be reassigned over time |
| Users | Assignments | 1-to-many | A staff member may receive many assignments |
| ServiceRequests | RequestComments | 1-to-many | A request may have multiple comments/notes |
| Users | RequestComments | 1-to-many | A user may author multiple comments |
| ServiceRequests | RequestHistory | 1-to-many | A request has multiple lifecycle/audit events |
| Users | RequestHistory | 1-to-many | A user may perform multiple auditable actions |
| RequestStatuses | RequestHistory | 1-to-many | Status transition history refers to controlled statuses |

---

## 8. Data Ownership

Logical ownership is assigned by responsibility rather than by physical database isolation.

| Data | Logical owner | Responsibility |
|---|---|---|
| Users / roles | Authentication / Authorisation | User identity and role context |
| Categories | Request Management | Controlled request classification |
| ServiceRequests | Request Management | Main request lifecycle |
| Assignments | Request Management | Request responsibility/reassignment |
| RequestComments | Request Management | Notes and resolution information |
| RequestHistory | Audit / History | Traceable record of significant lifecycle changes |
| RequestStatuses | Request Management / shared reference data | Controlled status vocabulary |

All data currently resides in the same relational store. Logical ownership is used to prevent unrelated components from directly manipulating another component's persistence responsibilities without a defined application boundary.

---

## 9. Integrity Strategy

CivicConnect will use both application-level and database-level integrity controls.

### 9.1 Primary keys

Every persistent entity has a stable primary key.

### 9.2 Foreign keys

Foreign keys prevent orphaned relational data.

Examples:
- `ServiceRequests.RequesterId → Users.UserId`
- `ServiceRequests.CategoryId → Categories.CategoryId`
- `ServiceRequests.StatusId → RequestStatuses.StatusId`
- `Assignments.ServiceRequestId → ServiceRequests.ServiceRequestId`
- `Assignments.StaffUserId → Users.UserId`
- `RequestHistory.ServiceRequestId → ServiceRequests.ServiceRequestId`

### 9.3 Required fields

`NOT NULL` should be used where absence would invalidate the record, including request title, description, location, requester, category, status, assignment staff member, history actor and required timestamps.

### 9.4 Unique constraints

At minimum:
- `Users.Email`;
- `Categories.CategoryName`;
- `RequestStatuses.StatusName`;
- `ServiceRequests.TicketNumber`.

### 9.5 Delete behaviour

The default M2 design should prefer preserving traceability over destructive cascades.

The implementation should therefore be cautious with cascade deletes on users, service requests, assignments, request history and comments. Where possible, lifecycle flags such as `IsActive` should be used for reference/user records that need to be retired without destroying historical evidence.

Final delete/retention behaviour remains an item for team approval.

---

## 10. Transaction Management

Some CivicConnect actions require multiple related writes and must behave as one logical operation.

### 10.1 Example – service-request submission

A submission may require:

1. create the `ServiceRequest`;
2. generate/store the Ticket Number;
3. create the initial `RequestHistory` entry;
4. optionally create routing/assignment information when that workflow is implemented.

If any required write fails, the operation should roll back.

### 10.2 Example – resolve request

A resolve operation may require:

1. validate that the acting user is authorised;
2. validate that resolution notes are supplied;
3. update `ServiceRequests.StatusId`;
4. update `ServiceRequests.UpdatedAt`;
5. increment/update the concurrency value;
6. insert the resolution `RequestComment`;
7. insert a `RequestHistory` entry;
8. commit.

```text
BEGIN TRANSACTION
    Update ServiceRequest
    Insert Resolution Comment
    Insert RequestHistory
IF all succeed
    COMMIT
ELSE
    ROLLBACK
```

### 10.3 Responsibility

Transaction boundaries are controlled in the application/service layer rather than hidden inside UI controllers or spread across stored procedures.

---

## 11. Concurrency Strategy

CivicConnect must account for two staff members attempting to update the same request using stale information.

### 11.1 Problem example

1. Staff A opens Ticket 152 at version 3.
2. Staff B opens Ticket 152 at version 3.
3. Staff A updates the request successfully; it becomes version 4.
4. Staff B attempts to save an update based on version 3.

Without a concurrency control, Staff B could overwrite Staff A's more recent change.

### 11.2 Proposed approach

Use optimistic concurrency on mutable request data.

The current logical model includes:

`ServiceRequests.Version`

The persistence implementation should compare the version expected by the application with the current stored version before accepting the update.

If the values conflict:

- do not silently overwrite the newer data;
- roll back the current operation where required;
- return a controlled conflict to the application;
- require the user/application to reload the current request state.

The exact EF Core provider-specific implementation must be confirmed against the selected MySQL provider.

---

## 12. Audit and History Design

Auditability is a major CivicConnect requirement because status and assignment changes must remain traceable.

The `RequestHistory` table is therefore not merely a copy of the current `ServiceRequests.StatusId`. It records **how the request reached its current state**.

| ChangedAt | Action | Old status | New status | Actor |
|---|---|---|---|---|
| 09:00 | RequestCreated | — | Pending | Requester |
| 10:15 | RequestAssigned | Pending | Pending | Staff |
| 10:20 | StatusChanged | Pending | In Progress | Staff |
| 15:40 | ResolutionAdded | In Progress | Resolved | Staff |

The `ServiceRequests` table provides the current state efficiently, while `RequestHistory` preserves the traceable lifecycle.

---

## 13. Indexing Strategy

The initial schema should keep indexing proportionate to M2 rather than attempting premature optimisation.

### 13.1 Indexes provided by keys

Primary and unique keys cover entity IDs, ticket number, user email, category name and status name.

### 13.2 Candidate additional indexes

The following should be considered as actual query patterns become visible:

- `ServiceRequests.RequesterId`
- `ServiceRequests.StatusId`
- `ServiceRequests.CategoryId`
- `ServiceRequests.CreatedAt`
- `Assignments.ServiceRequestId`
- `Assignments.StaffUserId`
- `Assignments.IsCurrent`
- `RequestHistory.ServiceRequestId`
- `RequestHistory.ChangedAt`

A composite index should only be added where an observed/required query justifies it. A later candidate could be `(StatusId, CreatedAt)` for dashboard queries.

---

## 14. Expected Access Patterns

### Requester
- submit a request;
- retrieve a request by ticket number;
- list own requests;
- view current status;
- view request history.

### Staff
- list/filter pending or open requests;
- assign a request;
- retrieve assigned requests;
- update request status;
- record comments/resolution notes;
- view request history.

### Management
- query counts by status;
- query request volumes;
- query outstanding/overdue work where the overdue rule is later defined;
- query assignment/staff information required by the dashboard.

---

## 15. Schema Evolution and EF Core Migrations

Schema changes must be controlled through source control.

```text
Change entity/model
        ↓
Generate EF Core migration
        ↓
Review migration
        ↓
Commit migration to GitHub
        ↓
Peer review / CI
        ↓
Apply migration to development database
```

Initial migration names could include:
- `InitialCreate`
- `AddRequestHistory`
- `AddConcurrencyVersion`

### Rules
- Avoid unrecorded manual production-schema changes.
- Migrations must be committed.
- Schema changes affecting requirements should update relevant documentation/RTM evidence.
- Breaking changes should be reviewed before merge.
- Credentials must never be embedded inside migrations.

---

## 16. Configuration and Secret Handling

The repository may contain configuration templates and setup instructions, but it must not contain live credentials.

Do **not** commit:
- MySQL passwords;
- Oracle/hosting passwords;
- API keys;
- private keys;
- access tokens;
- production connection strings containing credentials;
- `.env` files containing live values.

A safe example may be documented as:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=CivicConnect;User=YOUR_USER;Password=YOUR_PASSWORD;"
  }
}
```

Real values must be supplied using the team's approved environment/secrets mechanism.

---

## 17. Security and Sensitive Data Considerations

The database design supports security but does not replace application-level authorisation.

Key principles for the current baseline:

- only authorised application functions may change request state;
- foreign keys prevent invalid relational references;
- users should not be physically deleted if doing so destroys required audit links;
- access to database credentials must be restricted;
- raw SQL should not be constructed casually in controllers;
- user input must still be validated by the application;
- sensitive configuration must stay outside source control;
- audit/history records must preserve who performed important actions.

The final identity/password design belongs to the authentication decision and should not be invented independently in the database layer.

---

## 18. Availability, SPOF and Recovery Considerations

M2 requires database availability, bottleneck, scalability and recovery implications to be considered even though full production operations are not yet required.

### 18.1 Single point of failure

A single MySQL instance can become a single point of failure for CivicConnect because the core application depends on request persistence.

For M2 this risk is **recognised rather than fully solved**.

### 18.2 Availability

If the database becomes unavailable:

- new requests cannot be persisted reliably;
- request assignments/status changes cannot be safely committed;
- request history cannot be updated reliably;
- management reporting becomes unavailable or stale.

The application should fail safely rather than pretend an operation succeeded.

### 18.3 Backup and recovery

The final hosting platform's backup/recovery features must be confirmed before the production baseline.

The team should later define:

- backup frequency;
- retention;
- restore procedure;
- who is responsible for recovery;
- acceptable recovery point;
- acceptable recovery time.

For M2, the important engineering evidence is that database loss/availability is identified as a material risk and is not ignored.

### 18.4 Scalability

The current relational design is appropriate to the strongly related request/assignment/history data. Initial scaling attention should focus on:

- sensible indexes;
- avoiding unnecessary full-table queries;
- pagination for request lists;
- efficient dashboard queries;
- connection management through the selected framework/provider.

No distributed database architecture is currently justified by the M2 requirements.

---

## 19. Initial Verification Plan

| Test/check | Expected result |
|---|---|
| Create valid service request | Request is persisted with a unique ticket number |
| Missing/invalid requester | Write is rejected |
| Missing/invalid category | Write is rejected |
| Duplicate ticket number | Write is rejected |
| Assignment to valid staff user | Assignment is persisted |
| Assignment to invalid user ID | FK rejects invalid relationship |
| Status update with history | Current status and history entry are both stored |
| Resolution without required note | Application rejects operation |
| Failure during multi-step write | Transaction rolls back |
| Conflicting version update | Stale update is rejected/handled |
| Requester history query | Returns requests belonging to requester |
| Audit history query | Returns ordered lifecycle events for request |

Not every test must be complete in M2, but implemented persistence decisions should have initial verification where practical.

---

## 20. Requirement-to-Database Traceability

| Requirement | Database entities | Main integrity/decision evidence |
|---|---|---|
| FR-001 Service Request Submission | Users, Categories, RequestStatuses, ServiceRequests, RequestHistory | Unique TicketNumber; requester/category/status FKs; submission transaction |
| FR-002 Request Tracking & History | ServiceRequests, RequestStatuses, RequestHistory | Current state plus historical lifecycle |
| FR-003 Request Assignment | Assignments, Users, ServiceRequests, RequestHistory | Valid staff FK; assignment lifecycle; timestamp/history |
| FR-004 Status & Resolution | ServiceRequests, RequestStatuses, RequestComments, RequestHistory | controlled state update; required resolution note; transaction |
| FR-005 Dashboard | ServiceRequests, Assignments, RequestStatuses, Categories | queryable relational data; candidate indexes |
| RBAC | Users / selected identity model | role context used by application authorisation |
| Auditability | RequestHistory | actor, request, state, timestamp and notes |
| Data integrity | All related entities | PK/FK/NOT NULL/UNIQUE + transaction + concurrency |

---

## 21. Database Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Database connectivity failure | Medium | High | controlled failure handling; environment configuration; documented setup |
| Partial multi-table update | Medium | High | application-managed transaction and rollback |
| Invalid relational data | Low | High | primary/foreign keys, required fields and unique constraints |
| Lost concurrent update | Medium | Medium/High | optimistic concurrency token and controlled conflict handling |
| Schema drift between developers | Medium | Medium | version-controlled EF Core migrations |
| Credentials committed to GitHub | Low | High | environment/secrets mechanism and `.gitignore` |
| Destructive cascade/delete removes audit evidence | Low | High | conservative delete behaviour; deactivate where appropriate |
| Database instance unavailable/SPOF | Medium | High | record architecture risk; confirm hosting backup/availability strategy |
| Slow request/dashboard queries as data grows | Medium | Medium | indexes based on actual access patterns; pagination; query review |
| Application and schema become tightly coupled through raw SQL | Medium | Medium/High | defined EF Core/data-access layer and service boundaries |
