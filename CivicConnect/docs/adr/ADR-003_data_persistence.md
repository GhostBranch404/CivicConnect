ADR-003 - CivicConnect Persistence and Data-Integrity Strategy

Status: Proposed for M2 baseline

Context
CivicConnect must store service requests, requester information, controlled categories, assignments, request status, resolution information and an auditable history of important request changes. The data is relational and several important operations affect multiple related records. Incomplete writes could result in a service request being updated without the related audit or assignment information.

Decision
CivicConnect will use MySQL as the relational persistence store, continuing the technology direction established during Milestone 1. Business validation and transactional coordination will primarily occur in the application/service layer, while MySQL will enforce structural integrity through primary keys, foreign keys, required fields and unique constraints.
Entity Framework Core will be used as the intended persistence abstraction, with schema changes managed through version-controlled migrations rather than unmanaged manual changes.
Multi-step operations that must remain consistent will execute within a transaction. If a required operation fails, the transaction will roll back.
An optimistic concurrency mechanism will also be used on mutable service-request data to detect conflicting updates by multiple users.

Main consequences
The approach improves maintainability and testability because business rules remain in the application layer rather than being distributed across stored procedures. Database constraints still protect relational integrity if invalid references are attempted. Transactions prevent partially completed business operations.
The approach adds additional implementation work because transaction boundaries, concurrency conflicts, migrations and entity mappings must be managed deliberately.
This lines up with your A2 recommendation that the service/application layer handle transactional boundaries while MySQL retains structural safeguards.
