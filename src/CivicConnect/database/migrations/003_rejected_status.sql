-- CivicConnect: add a "Rejected" status so staff and managers can reject
-- unreasonable or fake requests. Existing statuses (1-3) are unchanged.
-- Run ONCE against each database (Aiven, local, test). Safe to re-run.

INSERT INTO RequestStatuses (StatusId, StatusName)
VALUES (4, 'Rejected')
ON DUPLICATE KEY UPDATE StatusName = VALUES(StatusName);
