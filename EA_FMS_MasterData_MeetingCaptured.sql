-- EA FMS operational master configuration
-- Authoritative central catalog rows required for Meeting workflow creation.
-- Not a schema migration. Idempotent: does not create duplicates.

START TRANSACTION;

-- Business Module: Meeting
-- Resolution: MeetingService requires exactly one active, non-deleted row
-- where lower(trim(Name)) = 'meeting'.
UPDATE public.ea_business_modules
SET
    "Name" = 'Meeting',
    "IsActive" = TRUE,
    "IsDeleted" = FALSE,
    "ModifiedBy" = 'ea-config',
    "ModifiedDate" = NOW() AT TIME ZONE 'utc',
    "Description" = COALESCE(NULLIF(TRIM("Description"), ''), 'EA FMS Meeting Management business module')
WHERE lower(trim("Name")) IN ('meeting', 'meetings')
  AND (
      "IsDeleted" = TRUE
      OR "IsActive" = FALSE
      OR trim("Name") <> 'Meeting'
  );

INSERT INTO public.ea_business_modules (
    "Name",
    "Description",
    "IsActive",
    "IsDeleted",
    "CreatedBy",
    "CreatedDate"
)
SELECT
    'Meeting',
    'EA FMS Meeting Management business module',
    TRUE,
    FALSE,
    'ea-config',
    NOW() AT TIME ZONE 'utc'
WHERE NOT EXISTS (
    SELECT 1
    FROM public.ea_business_modules
    WHERE lower(trim("Name")) = 'meeting'
);

-- Workflow Status: Captured
-- Resolution: WorkflowService CreateCapturedAsync uses
-- ILike(Name, 'Captured') AND IsActive AND NOT IsDeleted.
UPDATE public.ea_statuses
SET
    "Name" = 'Captured',
    "IsActive" = TRUE,
    "IsDeleted" = FALSE,
    "ModifiedBy" = 'ea-config',
    "ModifiedDate" = NOW() AT TIME ZONE 'utc',
    "Description" = COALESCE(NULLIF(TRIM("Description"), ''), 'Initial shared workflow status')
WHERE lower(trim("Name")) IN ('captured', 'capture')
  AND (
      "IsDeleted" = TRUE
      OR "IsActive" = FALSE
      OR trim("Name") <> 'Captured'
  );

INSERT INTO public.ea_statuses (
    "Name",
    "Description",
    "DisplayOrder",
    "IsActive",
    "IsDeleted",
    "CreatedBy",
    "CreatedDate"
)
SELECT
    'Captured',
    'Initial shared workflow status',
    0,
    TRUE,
    FALSE,
    'ea-config',
    NOW() AT TIME ZONE 'utc'
WHERE NOT EXISTS (
    SELECT 1
    FROM public.ea_statuses
    WHERE lower(trim("Name")) = 'captured'
);

COMMIT;
