using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialEaFms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_modules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_modules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "escalation_levels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_escalation_levels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "priority_levels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_priority_levels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "statuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_statuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "intake_requests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    BusinessModuleId = table.Column<long>(type: "bigint", nullable: true),
                    StatusId = table.Column<int>(type: "integer", nullable: true),
                    PriorityLevelId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intake_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_intake_requests_business_modules_BusinessModuleId",
                        column: x => x.BusinessModuleId,
                        principalTable: "business_modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_intake_requests_priority_levels_PriorityLevelId",
                        column: x => x.PriorityLevelId,
                        principalTable: "priority_levels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_intake_requests_statuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "statuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "intake_classifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IntakeRequestId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intake_classifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_intake_classifications_intake_requests_IntakeRequestId",
                        column: x => x.IntakeRequestId,
                        principalTable: "intake_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_instances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IntakeRequestId = table.Column<long>(type: "bigint", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_instances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_instances_intake_requests_IntakeRequestId",
                        column: x => x.IntakeRequestId,
                        principalTable: "intake_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workflow_instances_statuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "statuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "followups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IntakeRequestId = table.Column<long>(type: "bigint", nullable: false),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_followups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_followups_intake_requests_IntakeRequestId",
                        column: x => x.IntakeRequestId,
                        principalTable: "intake_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_followups_workflow_instances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "workflow_instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: false),
                    FromStatusId = table.Column<int>(type: "integer", nullable: true),
                    ToStatusId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_history_workflow_instances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "workflow_instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "escalations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FollowupId = table.Column<long>(type: "bigint", nullable: false),
                    EscalationLevelId = table.Column<int>(type: "integer", nullable: false),
                    InitiatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_escalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_escalations_escalation_levels_EscalationLevelId",
                        column: x => x.EscalationLevelId,
                        principalTable: "escalation_levels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_escalations_followups_FollowupId",
                        column: x => x.FollowupId,
                        principalTable: "followups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_modules_IsActive",
                table: "business_modules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_business_modules_Name",
                table: "business_modules",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_escalation_levels_Code",
                table: "escalation_levels",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_escalation_levels_Level",
                table: "escalation_levels",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_escalations_EscalationLevelId",
                table: "escalations",
                column: "EscalationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_escalations_FollowupId",
                table: "escalations",
                column: "FollowupId");

            migrationBuilder.CreateIndex(
                name: "IX_escalations_InitiatedAt",
                table: "escalations",
                column: "InitiatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_followups_CreatedDate",
                table: "followups",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_followups_DueAt",
                table: "followups",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_followups_IntakeRequestId",
                table: "followups",
                column: "IntakeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_followups_WorkflowInstanceId",
                table: "followups",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_intake_classifications_IntakeRequestId",
                table: "intake_classifications",
                column: "IntakeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_intake_classifications_Name",
                table: "intake_classifications",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_intake_requests_BusinessModuleId",
                table: "intake_requests",
                column: "BusinessModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_intake_requests_CreatedDate",
                table: "intake_requests",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_intake_requests_IsActive",
                table: "intake_requests",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_intake_requests_PriorityLevelId",
                table: "intake_requests",
                column: "PriorityLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_intake_requests_StatusId",
                table: "intake_requests",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_priority_levels_IsActive",
                table: "priority_levels",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_priority_levels_Level",
                table: "priority_levels",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_statuses_DisplayOrder",
                table: "statuses",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_statuses_IsActive",
                table: "statuses",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_history_ChangedAt",
                table: "workflow_history",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_history_ToStatusId",
                table: "workflow_history",
                column: "ToStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_history_WorkflowInstanceId",
                table: "workflow_history",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instances_IntakeRequestId",
                table: "workflow_instances",
                column: "IntakeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instances_IsActive",
                table: "workflow_instances",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instances_StartedAt",
                table: "workflow_instances",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instances_StatusId",
                table: "workflow_instances",
                column: "StatusId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "escalations");

            migrationBuilder.DropTable(
                name: "intake_classifications");

            migrationBuilder.DropTable(
                name: "workflow_history");

            migrationBuilder.DropTable(
                name: "escalation_levels");

            migrationBuilder.DropTable(
                name: "followups");

            migrationBuilder.DropTable(
                name: "workflow_instances");

            migrationBuilder.DropTable(
                name: "intake_requests");

            migrationBuilder.DropTable(
                name: "business_modules");

            migrationBuilder.DropTable(
                name: "priority_levels");

            migrationBuilder.DropTable(
                name: "statuses");
        }
    }
}
