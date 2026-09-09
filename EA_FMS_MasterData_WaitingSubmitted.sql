-- EA FMS operational master configuration
-- Authoritative central catalog rows required for Waiting / Continue lifecycle.
-- Not a schema migration. Idempotent: does not create duplicates.
--
-- Resolution (same approach as WorkflowService CreateCapturedAsync):
--   ILike(Name, '<canonical name>') AND IsActive AND NOT IsDeleted

START TRANSACTION;

-- Workflow Status: Waiting / Follow-up
UPDATE public.ea_statuses
SET
    "Name" = 'Waiting / Follow-up',
    "IsActive" = TRUE,
    "IsDeleted" = FALSE,
    "ModifiedBy" = 'ea-config',
    "ModifiedDate" = NOW() AT TIME ZONE 'utc',
    "Description" = COALESCE(NULLIF(TRIM("Description"), ''), 'Shared workflow status when progress depends on another party'),
    "DisplayOrder" = CASE WHEN "DisplayOrder" = 0 THEN 2 ELSE "DisplayOrder" END
WHERE lower(trim("Name")) IN (
        'waiting / follow-up',
        'waiting/follow-up',
        'waiting / followup',
        'waiting/followup',
        'waiting',
        'follow-up',
        'followup'
    )
  AND (
      "IsDeleted" = TRUE
      OR "IsActive" = FALSE
      OR trim("Name") <> 'Waiting / Follow-up'
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
    'Waiting / Follow-up',
    'Shared workflow status when progress depends on another party',
    2,
    TRUE,
    FALSE,
    'ea-config',
    NOW() AT TIME ZONE 'utc'
WHERE NOT EXISTS (
    SELECT 1
    FROM public.ea_statuses
    WHERE lower(trim("Name")) IN (
        'waiting / follow-up',
        'waiting/follow-up',
        'waiting / followup',
        'waiting/followup',
        'waiting',
        'follow-up',
        'followup'
    )
);

-- Workflow Status: Submitted
UPDATE public.ea_statuses
SET
    "Name" = 'Submitted',
    "IsActive" = TRUE,
    "IsDeleted" = FALSE,
    "ModifiedBy" = 'ea-config',
    "ModifiedDate" = NOW() AT TIME ZONE 'utc',
    "Description" = COALESCE(NULLIF(TRIM("Description"), ''), 'Shared workflow status after work is submitted for review'),
    "DisplayOrder" = CASE WHEN "DisplayOrder" = 0 THEN 3 ELSE "DisplayOrder" END
WHERE lower(trim("Name")) IN (
        'submitted',
        'submit'
    )
  AND (
      "IsDeleted" = TRUE
      OR "IsActive" = FALSE
      OR trim("Name") <> 'Submitted'
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
    'Submitted',
    'Shared workflow status after work is submitted for review',
    3,
    TRUE,
    FALSE,
    'ea-config',
    NOW() AT TIME ZONE 'utc'
WHERE NOT EXISTS (
    SELECT 1
    FROM public.ea_statuses
    WHERE lower(trim("Name")) IN (
        'submitted',
        'submit'
    )
);

COMMIT;
