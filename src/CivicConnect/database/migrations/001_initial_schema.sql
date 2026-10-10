-- CivicConnect M2 initial schema
-- Dapper + MySqlConnector persistence baseline

CREATE TABLE IF NOT EXISTS Users (
    UserId INT AUTO_INCREMENT PRIMARY KEY,
    FullName VARCHAR(150) NOT NULL,
    Email VARCHAR(255) NOT NULL UNIQUE,
    Role VARCHAR(50) NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS Categories (
    CategoryId INT AUTO_INCREMENT PRIMARY KEY,
    CategoryName VARCHAR(100) NOT NULL UNIQUE,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS RequestStatuses (
    StatusId INT AUTO_INCREMENT PRIMARY KEY,
    StatusName VARCHAR(50) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS ServiceRequests (
    RequestId INT AUTO_INCREMENT PRIMARY KEY,
    TicketNumber VARCHAR(30) NOT NULL UNIQUE,
    RequesterId INT NOT NULL,
    CategoryId INT NOT NULL,
    StatusId INT NOT NULL,
    Title VARCHAR(200) NOT NULL,
    Description TEXT NOT NULL,
    Location VARCHAR(255) NOT NULL,
    RequestDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Version INT NOT NULL DEFAULT 1,

    CONSTRAINT FK_ServiceRequests_Users
        FOREIGN KEY (RequesterId) REFERENCES Users(UserId),
    CONSTRAINT FK_ServiceRequests_Categories
        FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
    CONSTRAINT FK_ServiceRequests_Statuses
        FOREIGN KEY (StatusId) REFERENCES RequestStatuses(StatusId)
);

CREATE INDEX IX_ServiceRequests_RequesterId ON ServiceRequests(RequesterId);
CREATE INDEX IX_ServiceRequests_CategoryId ON ServiceRequests(CategoryId);
CREATE INDEX IX_ServiceRequests_StatusId ON ServiceRequests(StatusId);
CREATE INDEX IX_ServiceRequests_RequestDate ON ServiceRequests(RequestDate);

CREATE TABLE IF NOT EXISTS Assignments (
    AssignmentId INT AUTO_INCREMENT PRIMARY KEY,
    RequestId INT NOT NULL,
    StaffUserId INT NOT NULL,
    AssignedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UnassignedAt DATETIME NULL,
    IsCurrent BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT FK_Assignments_Request
        FOREIGN KEY (RequestId) REFERENCES ServiceRequests(RequestId),
    CONSTRAINT FK_Assignments_StaffUser
        FOREIGN KEY (StaffUserId) REFERENCES Users(UserId)
);

CREATE INDEX IX_Assignments_RequestId ON Assignments(RequestId);
CREATE INDEX IX_Assignments_StaffUserId ON Assignments(StaffUserId);
CREATE INDEX IX_Assignments_Current ON Assignments(RequestId, IsCurrent);

CREATE TABLE IF NOT EXISTS RequestComments (
    CommentId INT AUTO_INCREMENT PRIMARY KEY,
    RequestId INT NOT NULL,
    UserId INT NOT NULL,
    CommentText TEXT NOT NULL,
    CommentType VARCHAR(30) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT FK_RequestComments_Request
        FOREIGN KEY (RequestId) REFERENCES ServiceRequests(RequestId),
    CONSTRAINT FK_RequestComments_User
        FOREIGN KEY (UserId) REFERENCES Users(UserId)
);

CREATE INDEX IX_RequestComments_RequestId ON RequestComments(RequestId);

CREATE TABLE IF NOT EXISTS RequestHistory (
    HistoryId INT AUTO_INCREMENT PRIMARY KEY,
    RequestId INT NOT NULL,
    ChangedByUserId INT NOT NULL,
    OldStatusId INT NULL,
    NewStatusId INT NULL,
    ActionType VARCHAR(50) NOT NULL,
    Notes TEXT NULL,
    ChangedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT FK_RequestHistory_Request
        FOREIGN KEY (RequestId) REFERENCES ServiceRequests(RequestId),
    CONSTRAINT FK_RequestHistory_User
        FOREIGN KEY (ChangedByUserId) REFERENCES Users(UserId),
    CONSTRAINT FK_RequestHistory_OldStatus
        FOREIGN KEY (OldStatusId) REFERENCES RequestStatuses(StatusId),
    CONSTRAINT FK_RequestHistory_NewStatus
        FOREIGN KEY (NewStatusId) REFERENCES RequestStatuses(StatusId)
);

CREATE INDEX IX_RequestHistory_RequestId ON RequestHistory(RequestId);
CREATE INDEX IX_RequestHistory_RequestChangedAt ON RequestHistory(RequestId, ChangedAt);

CREATE TABLE IF NOT EXISTS DeveloperLogs (
    LogId BIGINT AUTO_INCREMENT PRIMARY KEY,
    RequestId INT NULL,
    Actor VARCHAR(150) NOT NULL,
    Action VARCHAR(100) NOT NULL,
    EntityType VARCHAR(100) NOT NULL,
    EntityId VARCHAR(100) NULL,
    Severity VARCHAR(30) NOT NULL DEFAULT 'Information',
    Details TEXT NULL,
    LoggedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT FK_DeveloperLogs_Request
        FOREIGN KEY (RequestId) REFERENCES ServiceRequests(RequestId)
);

CREATE INDEX IX_DeveloperLogs_RequestId ON DeveloperLogs(RequestId);
CREATE INDEX IX_DeveloperLogs_LoggedAt ON DeveloperLogs(LoggedAt);
CREATE INDEX IX_DeveloperLogs_Severity ON DeveloperLogs(Severity);

INSERT INTO RequestStatuses (StatusId, StatusName)
VALUES
    (1, 'Pending'),
    (2, 'In Progress'),
    (3, 'Resolved')
ON DUPLICATE KEY UPDATE StatusName = VALUES(StatusName);

INSERT INTO Categories (CategoryId, CategoryName, IsActive)
VALUES
    (1, 'Maintenance', TRUE),
    (2, 'IT Support', TRUE),
    (3, 'Security', TRUE),
    (4, 'Facilities', TRUE),
    (5, 'Other', TRUE)
ON DUPLICATE KEY UPDATE
    CategoryName = VALUES(CategoryName),
    IsActive = VALUES(IsActive);
