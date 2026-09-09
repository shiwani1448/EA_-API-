-- EA FMS operational master configuration
-- Authoritative central catalog row required for Complete / End lifecycle.
-- Not a schema migration. Idempotent: does not create duplicates.
--
-- Resolution (same approach as WorkflowService CreateCapturedAsync /
-- TransitionAsync Completed name match):
--   ILike(Name, 'Completed') AND IsActive AND NOT IsDeleted

START TRANSACTION;

-- Workflow Status: Completed
UPDATE public.ea_statuses
SET
    "Name" = 'Completed',
    "IsActive" = TRUE,
    "IsDeleted" = FALSE,
    "ModifiedBy" = 'ea-config',
    "ModifiedDate" = NOW() AT TIME ZONE 'utc',
    "Description" = COALESCE(NULLIF(TRIM("Description"), ''), 'Shared terminal workflow status when active work execution is complete'),
    "DisplayOrder" = CASE WHEN "DisplayOrder" = 0 THEN 4 ELSE "DisplayOrder" END
WHERE lower(trim("Name")) IN (
        'completed',
        'complete',
        'completion'
    )
  AND (
      "IsDeleted" = TRUE
      OR "IsActive" = FALSE
      OR trim("Name") <> 'Completed'
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
    'Completed',
    'Shared terminal workflow status when active work execution is complete',
    4,
    TRUE,
    FALSE,
    'ea-config',
    NOW() AT TIME ZONE 'utc'
WHERE NOT EXISTS (
    SELECT 1
    FROM public.ea_statuses
    WHERE lower(trim("Name")) IN (
        'completed',
        'complete',
        'completion'
    )
);

COMMIT;
