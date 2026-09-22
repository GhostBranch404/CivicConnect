# ADR-001: Selection of 3-Tier Layered Architecture

## Status
Accepted

## Context
This project require a clear structured backend architecture to be maintainable for the developers. The CivicConnect application should be able to handle user requests submissionsm tracking those requests and have a cleae status management. The following architectural styles were evaluated:
1. **3-Tier Monolithic Architecture**
2. **Microservices Architecture**
3. **Monolith with Hexagonal Architecture**

## Decision
In the end the we decided to adopt the **3-Tier Layered Architecture** having a clear Presentation Layer, Business Logic Layer, Data Access Layer all built using C#.

### Architectural Layers:
- **Presentation Layer:** Responsible for handling the Http requests, routing and user interactions (through HTML/JS/CSS).
- **Business Logic Layer (`RequestService`):** Fulfils the projects requirements from the stackholders containing the domain logic and validation of the user requests (such as the Request Status: `Pending` -> `In Progress` -> `Resolved`).
- **Data Access Layer (`RequestRepository`):** Responsible for maintaining the database persistence, execution of SQL queries and parameter mapping throgh MySQL.

## Rationale & Trade-off Analysis
- **Complexity vs. Timeline:** Cannot justify the introduction of microservices within a 6-week execution window as it adds excessive infrastructure, network latency, and deployment complexity.
- **Maintainability and Testability:** Implementing unit testing (using xUnit) for business logic allows us to test the system without a live database connection.
- **Team Governance:** Explicit boundary separation allows individual team members to work on the project without any merge conflicts.

## Consequences 
- **Positive:** Allows for rapid development with low deployment overhead, a straight-forward local setupp and a clear traceability of the system.
- **Negative:** If boundary rules are violated there could be a ptential of tight coupling which would requiring team dicipline to prevent the controller from directly accessing the database layer.