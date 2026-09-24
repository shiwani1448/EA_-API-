using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <summary>
    /// Gives every existing non-deleted Followup its own "Follow-up" EaTask. Open follow-ups become
    /// NotStarted and snapshot the (Type, Actual) rule only when exactly one active rule matches —
    /// otherwise no TAT (null, never 0). Completed follow-ups copy CompletedAt, get no TAT and no
    /// phase row (their real start time was never recorded).
    /// </summary>
    public partial class BackfillFollowupEaTasks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO public.ea_tasks ("BusinessModuleId", "ModuleName", "BusinessRecordId", "Task", "Description", "Type",
                                             "TatRuleId", "AllottedTatMinutes", "ExecutionStatus", "CompletedAt",
                                             "IsActive", "IsDeleted", "CreatedBy", "CreatedDate")
                SELECT m."Id", m."Name", f."Id"::text,
                       left(COALESCE(NULLIF(btrim(f."Subject"), ''), 'Follow-up'), 500),
                       f."Note", NULLIF(btrim(f."Type"), ''),
                       CASE WHEN f."CompletedAt" IS NULL THEN r."Id" END,
                       CASE WHEN f."CompletedAt" IS NULL THEN r."TatMinutes" END,
                       CASE WHEN f."CompletedAt" IS NULL THEN 'NotStarted' ELSE 'Completed' END,
                       f."CompletedAt",
                       true, false, 'system-backfill', now()
                FROM public.ea_followups f
                CROSS JOIN LATERAL (
                    SELECT "Id", "Name" FROM public.ea_business_modules
                    WHERE lower(btrim("Name")) = 'follow-up' AND "IsActive" AND NOT "IsDeleted"
                    ORDER BY "Id" LIMIT 1) m
                LEFT JOIN LATERAL (
                    SELECT CASE WHEN count(*) = 1 THEN min(x."Id") END AS "Id",
                           CASE WHEN count(*) = 1 THEN min(x."TatMinutes") END AS "TatMinutes"
                    FROM public.ea_tat_rules x
                    WHERE x."BusinessModuleId" = m."Id" AND x."IsActive" AND NOT x."IsDeleted" AND x."TatMinutes" > 0
                      AND x."Type" IS NOT NULL AND f."Type" IS NOT NULL
                      AND lower(btrim(x."Type")) = lower(btrim(f."Type"))
                      AND lower(btrim(x."TaskType")) = 'actual'
                      AND (x."Subtype" IS NULL OR btrim(x."Subtype") = '')) r ON true
                WHERE NOT f."IsDeleted" AND f."EaTaskId" IS NULL
                  AND NOT EXISTS (SELECT 1 FROM public.ea_tasks t
                                  WHERE t."BusinessModuleId" = m."Id" AND t."BusinessRecordId" = f."Id"::text AND NOT t."IsDeleted");
                """);
            migrationBuilder.Sql("""
                UPDATE public.ea_followups f
                SET "EaTaskId" = t."Id"
                FROM public.ea_tasks t
                JOIN public.ea_business_modules m ON m."Id" = t."BusinessModuleId" AND lower(btrim(m."Name")) = 'follow-up'
                WHERE f."EaTaskId" IS NULL AND NOT f."IsDeleted" AND NOT t."IsDeleted"
                  AND t."BusinessRecordId" = f."Id"::text;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE public.ea_followups f SET "EaTaskId" = NULL
                FROM public.ea_tasks t WHERE t."Id" = f."EaTaskId" AND t."CreatedBy" = 'system-backfill';
                DELETE FROM public.ea_tasks WHERE "CreatedBy" = 'system-backfill';
                """);
        }
    }
}
