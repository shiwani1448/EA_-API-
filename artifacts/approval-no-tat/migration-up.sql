START TRANSACTION;

ALTER TABLE public.ea_tasks ALTER COLUMN "AllottedTatMinutes" DROP NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260916044619_MakeEaTaskAllottedTatMinutesNullable', '8.0.30');

COMMIT;

