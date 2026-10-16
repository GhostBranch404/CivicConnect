-- CivicConnect: two-admin approval for user access changes.
-- Every promote / remove-access request, and every new Admin account, is stored
-- here as 'Pending' until a SECOND admin (or dev) approves it. Only then is the
-- Users table changed. Run ONCE against each database (Aiven, local, test).
-- Safe to re-run.

CREATE TABLE IF NOT EXISTS AdminApprovals (
    ApprovalId        INT AUTO_INCREMENT PRIMARY KEY,
    ActionType        VARCHAR(30)  NOT NULL,              -- Promote | RemoveAccess | CreateUser
    TargetUserId      INT          NULL,                  -- NULL for CreateUser until approved
    OldRole           VARCHAR(50)  NULL,
    NewRole           VARCHAR(50)  NULL,
    NewFullName       VARCHAR(150) NULL,                  -- CreateUser only
    NewEmail          VARCHAR(255) NULL,                  -- CreateUser only
    NewPasswordHash   VARCHAR(255) NULL,                  -- CreateUser only (hashed, never plain text)
    RequestedByUserId INT          NOT NULL,
    RequestedAt       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Status            VARCHAR(20)  NOT NULL DEFAULT 'Pending',  -- Pending | Approved | Declined
    DecidedByUserId   INT          NULL,
    DecidedAt         DATETIME     NULL,

    INDEX IX_AdminApprovals_Status (Status),
    INDEX IX_AdminApprovals_Target (TargetUserId),

    CONSTRAINT FK_AdminApprovals_Target
        FOREIGN KEY (TargetUserId) REFERENCES Users(UserId),
    CONSTRAINT FK_AdminApprovals_RequestedBy
        FOREIGN KEY (RequestedByUserId) REFERENCES Users(UserId),
    CONSTRAINT FK_AdminApprovals_DecidedBy
        FOREIGN KEY (DecidedByUserId) REFERENCES Users(UserId)
);

-- Roles used by the app (Users.Role):
--   'Requester' (shown as Requester)   'Staff'   (shown as Worker)
--   'Manager'   (shown as Admin)       'Dev'     (shown as Dev)
-- Admins and Devs can see the user-management section and approve requests.
