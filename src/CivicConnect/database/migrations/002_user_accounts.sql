-- CivicConnect: user accounts for sign-up / sign-in.
-- Run ONCE against each database (local, test and Azure) before deploying the
-- version of the API that includes /api/auth.

ALTER TABLE Users
    ADD COLUMN PasswordHash VARCHAR(255) NULL AFTER Email;

-- Accounts created through the Register page get the role 'Requester'.
-- To make someone staff or a manager, have them register first, then run e.g.:
--   UPDATE Users SET Role = 'Staff'   WHERE Email = 'staff.member@example.com';
--   UPDATE Users SET Role = 'Manager' WHERE Email = 'manager@example.com';
