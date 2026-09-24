using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowupEscalationAiTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ea_followup_at_risk_checks",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FollowupId = table.Column<long>(type: "bigint", nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reasoning = table.Column<string>(type: "text", nullable: true),
                    SuggestedAction = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_followup_at_risk_checks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_followup_at_risk_checks_ea_followups_FollowupId",
                        column: x => x.FollowupId,
                        principalSchema: "public",
                        principalTable: "ea_followups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ea_followup_escalation_suggestions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FollowupId = table.Column<long>(type: "bigint", nullable: false),
                    RecommendedEscalationLevelId = table.Column<int>(type: "integer", nullable: true),
                    RecommendedEscalationLevelName = table.Column<string>(type: "text", nullable: true),
                    Reasoning = table.Column<string>(type: "text", nullable: true),
                    WarningMessage = table.Column<string>(type: "text", nullable: true),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedEscalationId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                    // xmin intentionally not declared — it exists implicitly on every
                    // Postgres table from creation (confirmed empirically: declaring it is
                    // rejected with "column name "xmin" conflicts with a system column
                    // name"). UseXminAsConcurrencyToken() in EaFmsDbContext only needs to
                    // map it, never create it.
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_followup_escalation_suggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_followup_escalation_suggestions_ea_escalations_AppliedEs~",
                        column: x => x.AppliedEscalationId,
                        principalSchema: "public",
                        principalTable: "ea_escalations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ea_followup_escalation_suggestions_ea_followups_FollowupId",
                        column: x => x.FollowupId,
                        principalSchema: "public",
                        principalTable: "ea_followups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ea_followup_reminder_suggestions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FollowupId = table.Column<long>(type: "bigint", nullable: false),
                    SuggestedSubject = table.Column<string>(type: "text", nullable: true),
                    SuggestedBody = table.Column<string>(type: "text", nullable: true),
                    Reasoning = table.Column<string>(type: "text", nullable: true),
                    WarningMessage = table.Column<string>(type: "text", nullable: true),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedRecipientEmail = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                    // xmin intentionally not declared — see the note in the sibling
                    // ea_followup_escalation_suggestions table above.
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_followup_reminder_suggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_followup_reminder_suggestions_ea_followups_FollowupId",
                        column: x => x.FollowupId,
                        principalSchema: "public",
                        principalTable: "ea_followups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ea_followup_resolution_predictions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FollowupId = table.Column<long>(type: "bigint", nullable: false),
                    PredictedResolutionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Basis = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: true),
                    WarningMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_followup_resolution_predictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_followup_resolution_predictions_ea_followups_FollowupId",
                        column: x => x.FollowupId,
                        principalSchema: "public",
                        principalTable: "ea_followups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_followup_at_risk_checks_CreatedDate",
                schema: "public",
                table: "ea_followup_at_risk_checks",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_followup_at_risk_checks_FollowupId",
                schema: "public",
                table: "ea_followup_at_risk_checks",
                column: "FollowupId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_followup_escalation_suggestions_AppliedEscalationId",
                schema: "public",
                table: "ea_followup_escalation_suggestions",
                column: "AppliedEscalationId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_followup_escalation_suggestions_CreatedDate",
                schema: "public",
                table: "ea_followup_escalation_suggestions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_followup_escalation_suggestions_FollowupId",
                schema: "public",
                table: "ea_followup_escalation_suggestions",
                column: "FollowupId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_followup_reminder_suggestions_CreatedDate",
                schema: "public",
                table: "ea_followup_reminder_suggestions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_followup_reminder_suggestions_FollowupId",
                schema: "public",
                table: "ea_followup_reminder_suggestions",
                column: "FollowupId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_followup_resolution_predictions_CreatedDate",
                schema: "public",
                table: "ea_followup_resolution_predictions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_followup_resolution_predictions_FollowupId",
                schema: "public",
                table: "ea_followup_resolution_predictions",
                column: "FollowupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ea_followup_at_risk_checks",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_followup_escalation_suggestions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_followup_reminder_suggestions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_followup_resolution_predictions",
                schema: "public");
        }
    }
}
