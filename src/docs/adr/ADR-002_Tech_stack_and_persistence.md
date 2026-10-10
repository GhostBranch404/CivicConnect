# ADR-002: Technology Stack and Data Access Layer Alignment

## Status
Accepted

## Context
This project requires a dependable transactional integrity for the workflows of the service requests which means the technical stack needs to be able to balance crossplatform backend support, database connectivity and a clear safty guidelines to prevent security vulnerabilities.

## Decision
The development team decided on the following core technology stack:
- **Backend Language & Framework:** C# 
- **Database Management System:** MySQL (through Oracle Services)
- **Data Access Strategy:** Direct ADO.NET / Dapper through `MySqlConnector`

## Rationale and Trade-off Analysis
1. **C#:** Provides strong typing, every member is familiar and profficient in the language providing asynchronous programming patterns (`async`/`await`), and middleware integration for web endpoints.
2. **MySQL (Oracle Services):** Allows for a garenteed compliance in relational ACID, provides a reliable method for indexing of requests allowiing for tracking of user queries, and normalized schema design.
3. **ADO.NET / Dapper:** 
   - Allows for explicit control of the SQL queries which enables performance tuning of the system.
   - Eliminates tracking overhead of ORM allowing for simple CRUD operations on service requests.
   - Implements strict parameterization (`@CitizenName`, `@Status`) at the repository level, completely mitigating the risks of SQL Injection.

## Consequences
- **Positive:** High query performances, allows for complete control over SQL query creation and has minior risks of an SQL injection form the mandatory parameterized queries.
- **Negative:** Any schema changes require a manual SQL migration script that would be updated rather than the automated ORM migration generation.