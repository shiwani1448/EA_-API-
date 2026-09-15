using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace hrms_api.Migrations
{
    public partial class AddEaApprovalManagement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<long>(
                name: "ea_approval_no_seq");

            migrationBuilder.CreateTable(
                name: "ea_approval_requests",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNo = table.Column<string>(type: "varchar(40)", nullable: false),
                    RequestTitle = table.Column<string>(maxLength: 500, nullable: true),
                    RequestType = table.Column<string>(maxLength: 200, nullable: true),
                    RequestedBy = table.Column<string>(maxLength: 100, nullable: true),
                    Department = table.Column<string>(maxLength: 200, nullable: true),
                    Priority = table.Column<string>(maxLength: 100, nullable: true),
                    Description = table.Column<string>(maxLength: 4000, nullable: true),
                    Justification = table.Column<string>(maxLength: 4000, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Currency = table.Column<string>(maxLength: 10, nullable: true),
                    RequiredApprovalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApproverId = table.Column<string>(maxLength: 100, nullable: true),
                    ApproverName = table.Column<string>(maxLength: 200, nullable: true),
                    WorkflowStatus = table.Column<string>(maxLength: 100, nullable: true),
                    CurrentCycleNo = table.Column<int>(nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(maxLength: 100, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_approval_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_approval_cycles",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalRequestId = table.Column<long>(nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleNo = table.Column<int>(nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedBy = table.Column<string>(maxLength: 100, nullable: true),
                    RequiredApprovalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApproverId = table.Column<string>(maxLength: 100, nullable: true),
                    Status = table.Column<string>(maxLength: 100, nullable: true),
                    ChangeReason = table.Column<string>(maxLength: 2000, nullable: true),
                    DecisionComment = table.Column<string>(maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_approval_cycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_approval_cycles_ea_approval_requests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "public",
                        principalTable: "ea_approval_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ea_approval_documents",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalRequestId = table.Column<long>(nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<long>(nullable: true),
                    FileName = table.Column<string>(maxLength: 500, nullable: true),
                    OriginalFileName = table.Column<string>(maxLength: 500, nullable: true),
                    StorageReference = table.Column<string>(maxLength: 1000, nullable: true),
                    ContentType = table.Column<string>(maxLength: 200, nullable: true),
                    FileSize = table.Column<long>(nullable: false),
                    DocumentType = table.Column<string>(maxLength: 100, nullable: true),
                    UploadedBy = table.Column<string>(maxLength: 100, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(nullable: false),
                    AttachmentId = table.Column<long>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_approval_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_approval_history",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalRequestId = table.Column<long>(nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNo = table.Column<string>(maxLength: 40, nullable: true),
                    CycleId = table.Column<long>(nullable: true),
                    Action = table.Column<string>(maxLength: 200, nullable: true),
                    ActorId = table.Column<string>(maxLength: 100, nullable: true),
                    ActorName = table.Column<string>(maxLength: 200, nullable: true),
                    Comment = table.Column<string>(maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Metadata = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_approval_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_approval_reminders",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalRequestId = table.Column<long>(nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<long>(nullable: true),
                    ReminderType = table.Column<string>(maxLength: 100, nullable: true),
                    SentTo = table.Column<string>(maxLength: 300, nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TriggeredBy = table.Column<string>(maxLength: 100, nullable: true),
                    Comment = table.Column<string>(maxLength: 2000, nullable: true),
                    Status = table.Column<string>(maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_approval_reminders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_approval_escalations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalRequestId = table.Column<long>(nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<long>(nullable: true),
                    EscalationLevel = table.Column<string>(maxLength: 100, nullable: true),
                    EscalatedFrom = table.Column<string>(maxLength: 100, nullable: true),
                    EscalatedTo = table.Column<string>(maxLength: 100, nullable: true),
                    Reason = table.Column<string>(maxLength: 2000, nullable: true),
                    EscalatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(maxLength: 100, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_approval_escalations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_requests_TaskId",
                schema: "public",
                table: "ea_approval_requests",
                column: "TaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_requests_ReferenceNo",
                schema: "public",
                table: "ea_approval_requests",
                column: "ReferenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_requests_WorkflowStatus",
                schema: "public",
                table: "ea_approval_requests",
                column: "WorkflowStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_requests_ApproverId",
                schema: "public",
                table: "ea_approval_requests",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_requests_RequestedBy",
                schema: "public",
                table: "ea_approval_requests",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_requests_RequiredApprovalDate",
                schema: "public",
                table: "ea_approval_requests",
                column: "RequiredApprovalDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_requests_CreatedAt",
                schema: "public",
                table: "ea_approval_requests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_cycles_TaskId_CycleNo",
                schema: "public",
                table: "ea_approval_cycles",
                columns: new[] { "TaskId", "CycleNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_cycles_ApprovalRequestId",
                schema: "public",
                table: "ea_approval_cycles",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_documents_ApprovalRequestId",
                schema: "public",
                table: "ea_approval_documents",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_documents_CycleId",
                schema: "public",
                table: "ea_approval_documents",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_history_ApprovalRequestId",
                schema: "public",
                table: "ea_approval_history",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_history_CycleId",
                schema: "public",
                table: "ea_approval_history",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_reminders_ApprovalRequestId",
                schema: "public",
                table: "ea_approval_reminders",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_reminders_CycleId",
                schema: "public",
                table: "ea_approval_reminders",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_escalations_ApprovalRequestId",
                schema: "public",
                table: "ea_approval_escalations",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_approval_escalations_CycleId",
                schema: "public",
                table: "ea_approval_escalations",
                column: "CycleId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ea_approval_escalations", schema: "public");
            migrationBuilder.DropTable(name: "ea_approval_reminders", schema: "public");
            migrationBuilder.DropTable(name: "ea_approval_history", schema: "public");
            migrationBuilder.DropTable(name: "ea_approval_documents", schema: "public");
            migrationBuilder.DropTable(name: "ea_approval_cycles", schema: "public");
            migrationBuilder.DropTable(name: "ea_approval_requests", schema: "public");
            migrationBuilder.DropSequence(name: "ea_approval_no_seq");
        }
    }
}
