# CivicConnect Initial Database ERD

This is the editable M2 ERD source. GitHub renders the Mermaid diagram directly.

```mermaid
erDiagram
    USERS ||--o{ SERVICE_REQUESTS : submits
    USERS ||--o{ ASSIGNMENTS : receives
    USERS ||--o{ REQUEST_COMMENTS : writes
    USERS ||--o{ REQUEST_HISTORY : performs

    CATEGORIES ||--o{ SERVICE_REQUESTS : classifies
    REQUEST_STATUSES ||--o{ SERVICE_REQUESTS : current_status
    REQUEST_STATUSES ||--o{ REQUEST_HISTORY : old_status
    REQUEST_STATUSES ||--o{ REQUEST_HISTORY : new_status

    SERVICE_REQUESTS ||--o{ ASSIGNMENTS : has
    SERVICE_REQUESTS ||--o{ REQUEST_COMMENTS : contains
    SERVICE_REQUESTS ||--o{ REQUEST_HISTORY : records
    SERVICE_REQUESTS ||--o{ DEVELOPER_LOGS : relates_to

    USERS {
        int UserId PK
        varchar FullName
        varchar Email UK
        varchar Role
        boolean IsActive
        datetime CreatedAt
    }

    CATEGORIES {
        int CategoryId PK
        varchar CategoryName UK
        boolean IsActive
    }

    REQUEST_STATUSES {
        int StatusId PK
        varchar StatusName UK
    }

    SERVICE_REQUESTS {
        int RequestId PK
        varchar TicketNumber UK
        int RequesterId FK
        int CategoryId FK
        int StatusId FK
        varchar Title
        text Description
        varchar Location
        datetime RequestDate
        datetime UpdatedAt
        int Version
    }

    ASSIGNMENTS {
        int AssignmentId PK
        int RequestId FK
        int StaffUserId FK
        datetime AssignedAt
        datetime UnassignedAt
        boolean IsCurrent
    }

    REQUEST_COMMENTS {
        int CommentId PK
        int RequestId FK
        int UserId FK
        text CommentText
        varchar CommentType
        datetime CreatedAt
    }

    REQUEST_HISTORY {
        int HistoryId PK
        int RequestId FK
        int ChangedByUserId FK
        int OldStatusId FK
        int NewStatusId FK
        varchar ActionType
        text Notes
        datetime ChangedAt
    }

    DEVELOPER_LOGS {
        bigint LogId PK
        int RequestId FK
        varchar Actor
        varchar Action
        varchar EntityType
        varchar EntityId
        varchar Severity
        text Details
        datetime LoggedAt
    }
```

## Notes

- `ServiceRequests` stores the current state of a ticket.
- `RequestHistory` stores the business/audit history of request lifecycle changes.
- `DeveloperLogs` stores developer/application trace events such as request creation, status updates and caught persistence failures; it is not a replacement for `RequestHistory`.
- `Version` is used for optimistic concurrency so stale updates do not silently overwrite newer changes.
- The logical `Users` structure may later be adapted to the team's final authentication/identity design.
