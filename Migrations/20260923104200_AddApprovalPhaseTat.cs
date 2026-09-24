using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalPhaseTat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ea_approval_phase_tat",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalRequestId = table.Column<long>(type: "bigint", nullable: false),
                    TaskType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewCycleNumber = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AllottedTatMinutes = table.Column<int>(type: "integer", nullable: true),
                    TatRuleId = table.Column<long>(type: "bigint", nullable: true),
                    TatUsedMinutes = table.Column<int>(type: "integer", nullable: true),
                    TatPausedMinutes = table.Column<int>(type: "integer", nullable: true),
                    PauseCount = table.Column<int>(type: "integer", nullable: true),
                    TatUsedSeconds = table.Column<decimal>(type: "numeric(20,7)", precision: 20, scale: 7, nullable: true),
                    TatPausedSeconds = table.Column<decimal>(type: "numeric(20,7)", precision: 20, scale: 7, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_approval_phase_tat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_approval_phase_tat_ea_approval_requests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "public",
                        principalTable: "ea_approval_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_phase_tat_ApprovalRequestId_TaskType_ReviewCycl~",
                schema: "public",
                table: "ea_approval_phase_tat",
                columns: new[] { "ApprovalRequestId", "TaskType", "ReviewCycleNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ea_approval_phase_tat_OpenPhase",
                schema: "public",
                table: "ea_approval_phase_tat",
                column: "ApprovalRequestId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            // Approval's own Type+TaskType phase-scoped TAT rule uniqueness — a NEW, separately-named
            // index. UX_ea_tat_rules_ActiveDelegationClassification (Delegation's) is left untouched.
            // No historical rows are changed; existing duplicates would fail this transactional
            // migration so an operator can review the evidence and repair them explicitly.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_ea_tat_rules_ActiveApprovalTaskTypeClassification"
                ON public.ea_tat_rules ("BusinessModuleId", lower(btrim("Type")), lower(btrim("TaskType")))
                WHERE "IsActive" AND NOT "IsDeleted"
                  AND lower(btrim("ModuleName")) = 'ea approval'
                  AND "Type" IS NOT NULL AND btrim("Type") <> ''
                  AND "TaskType" IS NOT NULL AND btrim("TaskType") <> ''
                  AND ("Subtype" IS NULL OR btrim("Subtype") = '');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX public.\"UX_ea_tat_rules_ActiveApprovalTaskTypeClassification\";");

            migrationBuilder.DropTable(
                name: "ea_approval_phase_tat",
                schema: "public");
        }
    }
}
