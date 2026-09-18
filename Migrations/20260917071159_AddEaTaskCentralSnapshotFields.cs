using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEaTaskCentralSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Added nullable first, backfilled deterministically from the existing
            // BusinessModuleId -> ea_business_modules relationship (same two-step pattern
            // already used by 20260915103914_AddModuleNameToEaTatRules), then locked to
            // NOT NULL. No fabricated history: every existing ea_tasks row already has a
            // valid, non-nullable BusinessModuleId FK, so this backfill is 100% deterministic.
            migrationBuilder.AddColumn<string>(
                name: "ModuleName",
                schema: "public",
                table: "ea_tasks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE public.ea_tasks AS t
                SET "ModuleName" = m."Name"
                FROM public.ea_business_modules AS m
                WHERE m."Id" = t."BusinessModuleId";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ModuleName",
                schema: "public",
                table: "ea_tasks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subtype",
                schema: "public",
                table: "ea_tasks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TatRuleId",
                schema: "public",
                table: "ea_tasks",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TatUsedMinutes",
                schema: "public",
                table: "ea_tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "public",
                table: "ea_tasks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ea_tasks_TatRuleId",
                schema: "public",
                table: "ea_tasks",
                column: "TatRuleId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ea_tasks_TatUsedMinutes_NonNegative",
                schema: "public",
                table: "ea_tasks",
                sql: "\"TatUsedMinutes\" IS NULL OR \"TatUsedMinutes\" >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_ea_tasks_ea_tat_rules_TatRuleId",
                schema: "public",
                table: "ea_tasks",
                column: "TatRuleId",
                principalSchema: "public",
                principalTable: "ea_tat_rules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ea_tasks_ea_tat_rules_TatRuleId",
                schema: "public",
                table: "ea_tasks");

            migrationBuilder.DropIndex(
                name: "IX_ea_tasks_TatRuleId",
                schema: "public",
                table: "ea_tasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ea_tasks_TatUsedMinutes_NonNegative",
                schema: "public",
                table: "ea_tasks");

            migrationBuilder.DropColumn(
                name: "ModuleName",
                schema: "public",
                table: "ea_tasks");

            migrationBuilder.DropColumn(
                name: "Subtype",
                schema: "public",
                table: "ea_tasks");

            migrationBuilder.DropColumn(
                name: "TatRuleId",
                schema: "public",
                table: "ea_tasks");

            migrationBuilder.DropColumn(
                name: "TatUsedMinutes",
                schema: "public",
                table: "ea_tasks");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "public",
                table: "ea_tasks");
        }
    }
}
