using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class RedesignEaTatRulesAndCreateEaTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ea_tat_rules_OperationCode",
                schema: "public",
                table: "ea_tat_rules");

            migrationBuilder.DropIndex(
                name: "IX_ea_tat_rules_PriorityLevelId",
                schema: "public",
                table: "ea_tat_rules");

            migrationBuilder.DropColumn(
                name: "OperationCode",
                schema: "public",
                table: "ea_tat_rules");

            migrationBuilder.DropColumn(
                name: "PriorityLevelId",
                schema: "public",
                table: "ea_tat_rules");

            migrationBuilder.RenameColumn(
                name: "Minutes",
                schema: "public",
                table: "ea_tat_rules",
                newName: "TatMinutes");

            migrationBuilder.AlterColumn<long>(
                name: "BusinessModuleId",
                schema: "public",
                table: "ea_tat_rules",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "ea_tasks",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessModuleId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessRecordId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Task = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    AllottedTatMinutes = table.Column<int>(type: "integer", nullable: false),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_tasks", x => x.Id);
                    table.CheckConstraint("CK_ea_tasks_AllottedTatMinutes_Positive", "\"AllottedTatMinutes\" > 0");
                    table.ForeignKey(
                        name: "FK_ea_tasks_ea_business_modules_BusinessModuleId",
                        column: x => x.BusinessModuleId,
                        principalSchema: "public",
                        principalTable: "ea_business_modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ea_tasks_ea_workflow_instances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalSchema: "public",
                        principalTable: "ea_workflow_instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ea_tat_rules_ActiveModule",
                schema: "public",
                table: "ea_tat_rules",
                column: "BusinessModuleId",
                unique: true,
                filter: "\"IsActive\" = true AND \"IsDeleted\" = false");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ea_tat_rules_TatMinutes_Positive",
                schema: "public",
                table: "ea_tat_rules",
                sql: "\"TatMinutes\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_ea_tasks_BusinessModuleId",
                schema: "public",
                table: "ea_tasks",
                column: "BusinessModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_tasks_BusinessModuleId_BusinessRecordId",
                schema: "public",
                table: "ea_tasks",
                columns: new[] { "BusinessModuleId", "BusinessRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_ea_tasks_BusinessRecordId",
                schema: "public",
                table: "ea_tasks",
                column: "BusinessRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_tasks_WorkflowInstanceId",
                schema: "public",
                table: "ea_tasks",
                column: "WorkflowInstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ea_tat_rules_ea_business_modules_BusinessModuleId",
                schema: "public",
                table: "ea_tat_rules",
                column: "BusinessModuleId",
                principalSchema: "public",
                principalTable: "ea_business_modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ea_tat_rules_ea_business_modules_BusinessModuleId",
                schema: "public",
                table: "ea_tat_rules");

            migrationBuilder.DropTable(
                name: "ea_tasks",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "UX_ea_tat_rules_ActiveModule",
                schema: "public",
                table: "ea_tat_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ea_tat_rules_TatMinutes_Positive",
                schema: "public",
                table: "ea_tat_rules");

            migrationBuilder.RenameColumn(
                name: "TatMinutes",
                schema: "public",
                table: "ea_tat_rules",
                newName: "Minutes");

            migrationBuilder.AlterColumn<int>(
                name: "BusinessModuleId",
                schema: "public",
                table: "ea_tat_rules",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "OperationCode",
                schema: "public",
                table: "ea_tat_rules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriorityLevelId",
                schema: "public",
                table: "ea_tat_rules",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ea_tat_rules_OperationCode",
                schema: "public",
                table: "ea_tat_rules",
                column: "OperationCode");

            migrationBuilder.CreateIndex(
                name: "IX_ea_tat_rules_PriorityLevelId",
                schema: "public",
                table: "ea_tat_rules",
                column: "PriorityLevelId");
        }
    }
}
