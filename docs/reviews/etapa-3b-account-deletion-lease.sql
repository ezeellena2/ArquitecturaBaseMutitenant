START TRANSACTION;
ALTER TABLE identity."AspNetUsers" ADD "DeletionLeaseExpiresAtUtc" timestamp with time zone;

ALTER TABLE identity."AspNetUsers" ADD "DeletionLeaseId" uuid;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260930171731_AccountDeletionLease', '10.0.12');

COMMIT;

