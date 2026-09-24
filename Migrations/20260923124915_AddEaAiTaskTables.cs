using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEaAiTaskTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The four per-module suggestion tables this migration replaces with eleven
            // per-task tables below were already dropped manually in development before
            // this migration was generated, so there is nothing left here to drop.

            migrationBuilder.CreateTable(
                name: "ea_approval_approver_recommendations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalRequestId = table.Column<long>(type: "bigint", nullable: false),
                    RecommendedApproverName = table.Column<string>(type: "text", nullable: true),
                    HistoricalSampleSize = table.Column<int>(type: "integer", nullable: false),
                    Reasoning = table.Column<string>(type: "text", nullable: true),
                    WarningMessage = table.Column<string>(type: "text", nullable: true),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedApproverId = table.Column<string>(type: "text", nullable: true),
                    AppliedApproverName = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_approval_approver_recommendations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_approval_readiness_checks",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalRequestId = table.Column<long>(type: "bigint", nullable: false),
                    IsLikelyReady = table.Column<bool>(type: "boolean", nullable: false),
                    MissingFieldsJson = table.Column<string>(type: "jsonb", nullable: false),
                    SuggestedDocumentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    WarningMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_approval_readiness_checks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_approval_status_summaries",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalRequestId = table.Column<long>(type: "bigint", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    WorkflowStatus = table.Column<string>(type: "text", nullable: true),
                    CurrentCycleNo = table.Column<int>(type: "integer", nullable: false),
                    DueState = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_approval_status_summaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_delegation_delay_risk_checks",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DelegationId = table.Column<long>(type: "bigint", nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reasoning = table.Column<string>(type: "text", nullable: true),
                    SuggestedNudgeMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_delegation_delay_risk_checks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_delegation_due_date_predictions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DelegationId = table.Column<long>(type: "bigint", nullable: false),
                    SuggestedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Basis = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: true),
                    WarningMessage = table.Column<string>(type: "text", nullable: true),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_delegation_due_date_predictions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_delegation_owner_suggestions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DelegationId = table.Column<long>(type: "bigint", nullable: false),
                    SuggestedDoerId = table.Column<string>(type: "text", nullable: true),
                    SuggestedDoerName = table.Column<string>(type: "text", nullable: true),
                    HistoricalSampleSize = table.Column<int>(type: "integer", nullable: false),
                    Reasoning = table.Column<string>(type: "text", nullable: true),
                    WarningMessage = table.Column<string>(type: "text", nullable: true),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedDoerId = table.Column<string>(type: "text", nullable: true),
                    AppliedDoerName = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_delegation_owner_suggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_meeting_action_extractions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<long>(type: "bigint", nullable: false),
                    MomUsed = table.Column<bool>(type: "boolean", nullable: false),
                    PdfUsed = table.Column<bool>(type: "boolean", nullable: false),
                    WarningMessage = table.Column<string>(type: "text", nullable: true),
                    ProposedActionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedActionsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_meeting_action_extractions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_travel_checklist_drafts",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TravelRequestId = table.Column<long>(type: "bigint", nullable: false),
                    ChecklistItemsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_travel_checklist_drafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_travel_itinerary_drafts",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TravelRequestId = table.Column<long>(type: "bigint", nullable: false),
                    Itinerary = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_travel_itinerary_drafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_travel_option_comparisons",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TravelRequestId = table.Column<long>(type: "bigint", nullable: false),
                    ComparedOptionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CanCreateBooking = table.Column<bool>(type: "boolean", nullable: false),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedBookingIdsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_travel_option_comparisons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_travel_option_suggestions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TravelRequestId = table.Column<long>(type: "bigint", nullable: false),
                    ProposedOptionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    WarningMessage = table.Column<string>(type: "text", nullable: true),
                    CanCreateBooking = table.Column<bool>(type: "boolean", nullable: false),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedBookingIdsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_travel_option_suggestions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_approver_recommendations_ApprovalRequestId",
                schema: "public",
                table: "ea_approval_approver_recommendations",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_approver_recommendations_CreatedDate",
                schema: "public",
                table: "ea_approval_approver_recommendations",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_readiness_checks_ApprovalRequestId",
                schema: "public",
                table: "ea_approval_readiness_checks",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_readiness_checks_CreatedDate",
                schema: "public",
                table: "ea_approval_readiness_checks",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_status_summaries_ApprovalRequestId",
                schema: "public",
                table: "ea_approval_status_summaries",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_status_summaries_CreatedDate",
                schema: "public",
                table: "ea_approval_status_summaries",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegation_delay_risk_checks_CreatedDate",
                schema: "public",
                table: "ea_delegation_delay_risk_checks",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegation_delay_risk_checks_DelegationId",
                schema: "public",
                table: "ea_delegation_delay_risk_checks",
                column: "DelegationId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegation_due_date_predictions_CreatedDate",
                schema: "public",
                table: "ea_delegation_due_date_predictions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegation_due_date_predictions_DelegationId",
                schema: "public",
                table: "ea_delegation_due_date_predictions",
                column: "DelegationId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegation_owner_suggestions_CreatedDate",
                schema: "public",
                table: "ea_delegation_owner_suggestions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegation_owner_suggestions_DelegationId",
                schema: "public",
                table: "ea_delegation_owner_suggestions",
                column: "DelegationId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_action_extractions_CreatedDate",
                schema: "public",
                table: "ea_meeting_action_extractions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_action_extractions_MeetingId",
                schema: "public",
                table: "ea_meeting_action_extractions",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_checklist_drafts_CreatedDate",
                schema: "public",
                table: "ea_travel_checklist_drafts",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_checklist_drafts_TravelRequestId",
                schema: "public",
                table: "ea_travel_checklist_drafts",
                column: "TravelRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_itinerary_drafts_CreatedDate",
                schema: "public",
                table: "ea_travel_itinerary_drafts",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_itinerary_drafts_TravelRequestId",
                schema: "public",
                table: "ea_travel_itinerary_drafts",
                column: "TravelRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_option_comparisons_CreatedDate",
                schema: "public",
                table: "ea_travel_option_comparisons",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_option_comparisons_TravelRequestId",
                schema: "public",
                table: "ea_travel_option_comparisons",
                column: "TravelRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_option_suggestions_CreatedDate",
                schema: "public",
                table: "ea_travel_option_suggestions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_option_suggestions_TravelRequestId",
                schema: "public",
                table: "ea_travel_option_suggestions",
                column: "TravelRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ea_approval_approver_recommendations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_approval_readiness_checks",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_approval_status_summaries",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_delegation_delay_risk_checks",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_delegation_due_date_predictions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_delegation_owner_suggestions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_meeting_action_extractions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_travel_checklist_drafts",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_travel_itinerary_drafts",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_travel_option_comparisons",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_travel_option_suggestions",
                schema: "public");

            migrationBuilder.CreateTable(
                name: "ea_approval_ai_suggestions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedValueJson = table.Column<string>(type: "jsonb", nullable: true),
                    ApprovalRequestId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FeatureName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                    SuggestedJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_approval_ai_suggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_delegation_ai_suggestions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedValueJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DelegationId = table.Column<long>(type: "bigint", nullable: false),
                    FeatureName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                    SuggestedJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_delegation_ai_suggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_meeting_ai_suggestions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedValueJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FeatureName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                    MeetingId = table.Column<long>(type: "bigint", nullable: false),
                    SuggestedJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_meeting_ai_suggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_travel_ai_suggestions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedValueJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FeatureName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsApplied = table.Column<bool>(type: "boolean", nullable: false),
                    SuggestedJson = table.Column<string>(type: "jsonb", nullable: false),
                    TravelRequestId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_travel_ai_suggestions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_ai_suggestions_ApprovalRequestId",
                schema: "public",
                table: "ea_approval_ai_suggestions",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_ai_suggestions_CreatedDate",
                schema: "public",
                table: "ea_approval_ai_suggestions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegation_ai_suggestions_CreatedDate",
                schema: "public",
                table: "ea_delegation_ai_suggestions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegation_ai_suggestions_DelegationId",
                schema: "public",
                table: "ea_delegation_ai_suggestions",
                column: "DelegationId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_ai_suggestions_CreatedDate",
                schema: "public",
                table: "ea_meeting_ai_suggestions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_ai_suggestions_MeetingId",
                schema: "public",
                table: "ea_meeting_ai_suggestions",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_ai_suggestions_CreatedDate",
                schema: "public",
                table: "ea_travel_ai_suggestions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_ai_suggestions_TravelRequestId",
                schema: "public",
                table: "ea_travel_ai_suggestions",
                column: "TravelRequestId");
        }
    }
}
