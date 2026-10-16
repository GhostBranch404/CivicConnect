-- CivicConnect: email confirmation for self sign-up.
-- Run ONCE on a NEW database after 001-004. The Azure database already has these columns
-- (it will report "Duplicate column name" if run again, which is safe to ignore).
--
-- Sign-up flow: the account is saved with IsActive = FALSE and a confirmation code, but only
-- if the confirmation email was sent. Entering the code sets EmailConfirmed and IsActive.

ALTER TABLE Users
    ADD COLUMN EmailConfirmed            BOOLEAN  NOT NULL DEFAULT FALSE AFTER CreatedAt,
    ADD COLUMN ConfirmationCodeProtected TEXT     NULL                   AFTER EmailConfirmed,
    ADD COLUMN ConfirmationCodeExpiresAt DATETIME NULL                   AFTER ConfirmationCodeProtected,
    ADD COLUMN ConfirmationAttempts      INT      NOT NULL DEFAULT 0     AFTER ConfirmationCodeExpiresAt;

-- Accounts that existed before email confirmation (and accounts added by an admin) count as confirmed.
UPDATE Users SET EmailConfirmed = TRUE WHERE IsActive = TRUE;
