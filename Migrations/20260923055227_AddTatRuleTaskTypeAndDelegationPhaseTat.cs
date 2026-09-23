using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTatRuleTaskTypeAndDelegationPhaseTat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaskType",
                schema: "public",
                table: "ea_tat_rules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ea_delegation_phase_tat",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DelegationId = table.Column<long>(type: "bigint", nullable: false),
                    TaskType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewCycleNumber = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AllottedTatMinutes = table.Column<int>(type: "integer", nullable: true),
                    TatRuleId = table.Column<long>(type: "bigint", nullable: true),
                    TatUsedMinutes = table.Column<int>(type: "integer", nullable: true),
                    TatPausedMinutes = table.Column<int>(type: "integer", nullable: true),
                    PauseCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_delegation_phase_tat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_delegation_phase_tat_ea_delegations_DelegationId",
                        column: x => x.DelegationId,
                        principalSchema: "public",
                        principalTable: "ea_delegations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegation_phase_tat_DelegationId_TaskType_ReviewCycleNu~",
                schema: "public",
                table: "ea_delegation_phase_tat",
                columns: new[] { "DelegationId", "TaskType", "ReviewCycleNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ea_delegation_phase_tat",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "TaskType",
                schema: "public",
                table: "ea_tat_rules");
        }
    }
}
