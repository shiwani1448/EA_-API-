START TRANSACTION;

ALTER TABLE public.ea_tasks ALTER COLUMN "AllottedTatMinutes" SET NOT NULL;

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260916044619_MakeEaTaskAllottedTatMinutesNullable';

COMMIT;

