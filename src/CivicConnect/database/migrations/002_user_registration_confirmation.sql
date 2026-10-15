-- Adds fields required for pending account registration and email/SMS confirmation.
-- Apply after 001_initial_schema.sql.

ALTER TABLE Users
    MODIFY COLUMN Email VARCHAR(255) NULL,
    ADD COLUMN PhoneNumber VARCHAR(16) NULL,
    ADD COLUMN PasswordHash VARCHAR(255) NULL,
    ADD COLUMN EmailConfirmed BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN PhoneNumberConfirmed BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN ConfirmationMethod VARCHAR(10) NULL,
    ADD COLUMN ConfirmationCodeProtected TEXT NULL,
    ADD COLUMN ConfirmationCodeExpiresAt DATETIME NULL,
    ADD COLUMN ConfirmationAttempts INT NOT NULL DEFAULT 0,
    ADD UNIQUE KEY UX_Users_PhoneNumber (PhoneNumber);
