using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEaDelegationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "ea_delegation_no_seq");

            migrationBuilder.CreateTable(
                name: "ea_delegations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReferenceNo = table.Column<string>(type: "varchar(40)", nullable: false),
                    EaTaskId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    AssignedToId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AssignedToNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AssignedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AssignedByNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Priority = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    SourceBusinessModuleId = table.Column<long>(type: "bigint", nullable: false),
                    SourceEntityId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AdditionalNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompletedByNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_delegations", x => x.Id);
                    table.CheckConstraint("CK_ea_delegations_Status", "\"Status\" IN ('Pending', 'InProgress', 'Completed')");
                    table.ForeignKey(
                        name: "FK_ea_delegations_ea_business_modules_SourceBusinessModuleId",
                        column: x => x.SourceBusinessModuleId,
                        principalSchema: "public",
                        principalTable: "ea_business_modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ea_delegations_ea_tasks_EaTaskId",
                        column: x => x.EaTaskId,
                        principalSchema: "public",
                        principalTable: "ea_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegations_AssignedToId",
                schema: "public",
                table: "ea_delegations",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegations_DueDate",
                schema: "public",
                table: "ea_delegations",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegations_EaTaskId",
                schema: "public",
                table: "ea_delegations",
                column: "EaTaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegations_ReferenceNo",
                schema: "public",
                table: "ea_delegations",
                column: "ReferenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegations_SourceBusinessModuleId",
                schema: "public",
                table: "ea_delegations",
                column: "SourceBusinessModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegations_SourceEntityId",
                schema: "public",
                table: "ea_delegations",
                column: "SourceEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_delegations_Status",
                schema: "public",
                table: "ea_delegations",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ea_delegations",
                schema: "public");

            migrationBuilder.DropSequence(
                name: "ea_delegation_no_seq");
        }
    }
}
