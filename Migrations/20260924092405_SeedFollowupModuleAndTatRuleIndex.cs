using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <summary>
    /// Seeds the "Follow-up" business module (only if missing) and widens the type-only
    /// active-rule uniqueness index — previously hard-coded to Delegation — to Follow-up too.
    /// </summary>
    [DbContext(typeof(EaFmsDbContext))]
    [Migration("20260924092405_SeedFollowupModuleAndTatRuleIndex")]
    public partial class SeedFollowupModuleAndTatRuleIndex : Migration
    {
        private const string IndexWhere = """
            WHERE "IsActive" AND NOT "IsDeleted"
              AND "Type" IS NOT NULL AND btrim("Type") <> ''
              AND "TaskType" IS NOT NULL AND btrim("TaskType") <> ''
              AND ("Subtype" IS NULL OR btrim("Subtype") = '')
            """;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO public.ea_business_modules ("Name", "Description", "IsActive", "IsDeleted", "CreatedBy", "CreatedDate")
                SELECT 'Follow-up', 'EA follow-up activity (Actual-phase TAT only)', true, false, 'system', now()
                WHERE NOT EXISTS (
                    SELECT 1 FROM public.ea_business_modules
                    WHERE lower(btrim("Name")) = 'follow-up' AND NOT "IsDeleted");
                """);
            migrationBuilder.Sql("DROP INDEX public.\"UX_ea_tat_rules_ActiveDelegationClassification\";");
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_ea_tat_rules_ActiveDelegationClassification"
                ON public.ea_tat_rules ("BusinessModuleId", lower(btrim("Type")), lower(btrim("TaskType")))
                """ + "\n" + IndexWhere + "\n  AND lower(btrim(\"ModuleName\")) IN ('delegation', 'follow-up');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX public.\"UX_ea_tat_rules_ActiveDelegationClassification\";");
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_ea_tat_rules_ActiveDelegationClassification"
                ON public.ea_tat_rules ("BusinessModuleId", lower(btrim("Type")), lower(btrim("TaskType")))
                """ + "\n" + IndexWhere + "\n  AND lower(btrim(\"ModuleName\")) = 'delegation';");
            // The seeded module row is kept: tasks/rules may already reference it.
        }
    }
}
