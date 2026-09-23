using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class EnforceDelegationPhaseTatIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No historical rows are changed. Existing duplicates cause this transactional migration
            // to fail so an operator can review the evidence and repair them explicitly.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_ea_tat_rules_ActiveDelegationClassification"
                ON public.ea_tat_rules ("BusinessModuleId", lower(btrim("Type")), lower(btrim("TaskType")))
                WHERE "IsActive" AND NOT "IsDeleted"
                  AND lower(btrim("ModuleName")) = 'delegation'
                  AND "Type" IS NOT NULL AND btrim("Type") <> ''
                  AND "TaskType" IS NOT NULL AND btrim("TaskType") <> ''
                  AND ("Subtype" IS NULL OR btrim("Subtype") = '');
                """);
            migrationBuilder.DropIndex(
                name: "IX_ea_delegation_phase_tat_DelegationId_TaskType_ReviewCycleNu~",
                schema: "public",
                table: "ea_delegation_phase_tat");

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegation_phase_tat_DelegationId_TaskType_ReviewCycleNu~",
                schema: "public",
                table: "ea_delegation_phase_tat",
                columns: new[] { "DelegationId", "TaskType", "ReviewCycleNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ea_delegation_phase_tat_OpenPhase",
                schema: "public",
                table: "ea_delegation_phase_tat",
                column: "DelegationId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX public.\"UX_ea_tat_rules_ActiveDelegationClassification\";");
            migrationBuilder.DropIndex(
                name: "IX_ea_delegation_phase_tat_DelegationId_TaskType_ReviewCycleNu~",
                schema: "public",
                table: "ea_delegation_phase_tat");

            migrationBuilder.DropIndex(
                name: "UX_ea_delegation_phase_tat_OpenPhase",
                schema: "public",
                table: "ea_delegation_phase_tat");

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegation_phase_tat_DelegationId_TaskType_ReviewCycleNu~",
                schema: "public",
                table: "ea_delegation_phase_tat",
                columns: new[] { "DelegationId", "TaskType", "ReviewCycleNumber" });
        }
    }
}
