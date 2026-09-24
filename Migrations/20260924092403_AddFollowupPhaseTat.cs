using System;
using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <summary>One Actual-phase TAT row per Followup (mirrors ea_delegation_phase_tat).</summary>
    [DbContext(typeof(EaFmsDbContext))]
    [Migration("20260924092403_AddFollowupPhaseTat")]
    public partial class AddFollowupPhaseTat : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ea_followup_phase_tat",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FollowupId = table.Column<long>(type: "bigint", nullable: false),
                    TaskType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewCycleNumber = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EndedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EndedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AllottedTatMinutes = table.Column<int>(type: "integer", nullable: true),
                    TatRuleId = table.Column<long>(type: "bigint", nullable: true),
                    TatUsedMinutes = table.Column<int>(type: "integer", nullable: true),
                    TatPausedMinutes = table.Column<int>(type: "integer", nullable: true),
                    PauseCount = table.Column<int>(type: "integer", nullable: true),
                    TatUsedSeconds = table.Column<decimal>(type: "numeric(20,7)", precision: 20, scale: 7, nullable: true),
                    TatPausedSeconds = table.Column<decimal>(type: "numeric(20,7)", precision: 20, scale: 7, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_followup_phase_tat", x => x.Id);
                    table.CheckConstraint("CK_followup_phase_actual", "\"TaskType\" = 'Actual' AND \"ReviewCycleNumber\" = 0");
                    table.ForeignKey(name: "FK_ea_followup_phase_tat_ea_followups_FollowupId", column: x => x.FollowupId,
                        principalSchema: "public", principalTable: "ea_followups", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(name: "IX_ea_followup_phase_tat_FollowupId", schema: "public", table: "ea_followup_phase_tat",
                column: "FollowupId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_ea_followup_phase_tat_FollowupId_TaskType_ReviewCycleNumber", schema: "public", table: "ea_followup_phase_tat",
                columns: new[] { "FollowupId", "TaskType", "ReviewCycleNumber" }, unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "ea_followup_phase_tat", schema: "public");
    }
}
