using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAiFkConstraintsAndCalendarAiTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No AddColumn for "xmin" on any table: xmin is a Postgres system column that
            // already exists implicitly on every table from creation (confirmed empirically —
            // `ALTER TABLE ... ADD COLUMN xmin ...` is rejected with "column name "xmin"
            // conflicts with a system column name"). UseXminAsConcurrencyToken() in
            // EaFmsDbContext only needs to map it, never create it, so the AddColumn/DropColumn
            // operations EF scaffolded for these six tables have been removed from this
            // migration by hand.

            migrationBuilder.CreateTable(
                name: "ea_calendar_conflict_checks",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FromDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ToDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HasConflicts = table.Column<bool>(type: "boolean", nullable: false),
                    ConflictsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    WarningMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_calendar_conflict_checks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_calendar_quick_add_suggestions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InputText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SuggestedTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SuggestedEventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SuggestedStartDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuggestedEndDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuggestedIsAllDay = table.Column<bool>(type: "boolean", nullable: false),
                    SuggestedLocation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Reasoning = table.Column<string>(type: "text", nullable: true),
                    WarningMessage = table.Column<string>(type: "text", nullable: true),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedCalendarEventId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                    // xmin intentionally not declared — it exists implicitly on every
                    // Postgres table from creation; see the note in Up() above.
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_calendar_quick_add_suggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_calendar_quick_add_suggestions_ea_calendar_events_Applie~",
                        column: x => x.AppliedCalendarEventId,
                        principalSchema: "public",
                        principalTable: "ea_calendar_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_calendar_conflict_checks_CreatedDate",
                schema: "public",
                table: "ea_calendar_conflict_checks",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_calendar_quick_add_suggestions_AppliedCalendarEventId",
                schema: "public",
                table: "ea_calendar_quick_add_suggestions",
                column: "AppliedCalendarEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_calendar_quick_add_suggestions_CreatedDate",
                schema: "public",
                table: "ea_calendar_quick_add_suggestions",
                column: "CreatedDate");

            migrationBuilder.AddForeignKey(
                name: "FK_ea_approval_approver_recommendations_ea_approval_requests_A~",
                schema: "public",
                table: "ea_approval_approver_recommendations",
                column: "ApprovalRequestId",
                principalSchema: "public",
                principalTable: "ea_approval_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_approval_readiness_checks_ea_approval_requests_ApprovalR~",
                schema: "public",
                table: "ea_approval_readiness_checks",
                column: "ApprovalRequestId",
                principalSchema: "public",
                principalTable: "ea_approval_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_approval_status_summaries_ea_approval_requests_ApprovalR~",
                schema: "public",
                table: "ea_approval_status_summaries",
                column: "ApprovalRequestId",
                principalSchema: "public",
                principalTable: "ea_approval_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_delegation_delay_risk_checks_ea_delegations_DelegationId",
                schema: "public",
                table: "ea_delegation_delay_risk_checks",
                column: "DelegationId",
                principalSchema: "public",
                principalTable: "ea_delegations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_delegation_due_date_predictions_ea_delegations_Delegatio~",
                schema: "public",
                table: "ea_delegation_due_date_predictions",
                column: "DelegationId",
                principalSchema: "public",
                principalTable: "ea_delegations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_delegation_owner_suggestions_ea_delegations_DelegationId",
                schema: "public",
                table: "ea_delegation_owner_suggestions",
                column: "DelegationId",
                principalSchema: "public",
                principalTable: "ea_delegations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_meeting_action_extractions_ea_meetings_MeetingId",
                schema: "public",
                table: "ea_meeting_action_extractions",
                column: "MeetingId",
                principalSchema: "public",
                principalTable: "ea_meetings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_travel_checklist_drafts_ea_travel_requests_TravelRequest~",
                schema: "public",
                table: "ea_travel_checklist_drafts",
                column: "TravelRequestId",
                principalSchema: "public",
                principalTable: "ea_travel_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_travel_itinerary_drafts_ea_travel_requests_TravelRequest~",
                schema: "public",
                table: "ea_travel_itinerary_drafts",
                column: "TravelRequestId",
                principalSchema: "public",
                principalTable: "ea_travel_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_travel_option_comparisons_ea_travel_requests_TravelReque~",
                schema: "public",
                table: "ea_travel_option_comparisons",
                column: "TravelRequestId",
                principalSchema: "public",
                principalTable: "ea_travel_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_travel_option_suggestions_ea_travel_requests_TravelReque~",
                schema: "public",
                table: "ea_travel_option_suggestions",
                column: "TravelRequestId",
                principalSchema: "public",
                principalTable: "ea_travel_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ea_approval_approver_recommendations_ea_approval_requests_A~",
                schema: "public",
                table: "ea_approval_approver_recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_approval_readiness_checks_ea_approval_requests_ApprovalR~",
                schema: "public",
                table: "ea_approval_readiness_checks");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_approval_status_summaries_ea_approval_requests_ApprovalR~",
                schema: "public",
                table: "ea_approval_status_summaries");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_delegation_delay_risk_checks_ea_delegations_DelegationId",
                schema: "public",
                table: "ea_delegation_delay_risk_checks");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_delegation_due_date_predictions_ea_delegations_Delegatio~",
                schema: "public",
                table: "ea_delegation_due_date_predictions");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_delegation_owner_suggestions_ea_delegations_DelegationId",
                schema: "public",
                table: "ea_delegation_owner_suggestions");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_meeting_action_extractions_ea_meetings_MeetingId",
                schema: "public",
                table: "ea_meeting_action_extractions");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_travel_checklist_drafts_ea_travel_requests_TravelRequest~",
                schema: "public",
                table: "ea_travel_checklist_drafts");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_travel_itinerary_drafts_ea_travel_requests_TravelRequest~",
                schema: "public",
                table: "ea_travel_itinerary_drafts");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_travel_option_comparisons_ea_travel_requests_TravelReque~",
                schema: "public",
                table: "ea_travel_option_comparisons");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_travel_option_suggestions_ea_travel_requests_TravelReque~",
                schema: "public",
                table: "ea_travel_option_suggestions");

            migrationBuilder.DropTable(
                name: "ea_calendar_conflict_checks",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_calendar_quick_add_suggestions",
                schema: "public");
        }
    }
}
