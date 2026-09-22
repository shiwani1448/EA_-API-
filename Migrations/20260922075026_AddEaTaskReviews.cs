using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEaTaskReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ea_task_reviews",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EaTaskId = table.Column<long>(type: "bigint", nullable: false),
                    ReviewCycleNo = table.Column<int>(type: "integer", nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReviewerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReviewerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubmittedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubmittedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewRemark = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReworkRemark = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_task_reviews", x => x.Id);
                    table.CheckConstraint("CK_ea_task_reviews_ReviewStatus", "\"ReviewStatus\" IN ('PendingReview', 'Approved', 'ReworkRequested')");
                    table.ForeignKey(
                        name: "FK_ea_task_reviews_ea_tasks_EaTaskId",
                        column: x => x.EaTaskId,
                        principalSchema: "public",
                        principalTable: "ea_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_task_reviews_EaTaskId",
                schema: "public",
                table: "ea_task_reviews",
                column: "EaTaskId");

            migrationBuilder.CreateIndex(
                name: "UX_ea_task_reviews_EaTaskId_ReviewCycleNo",
                schema: "public",
                table: "ea_task_reviews",
                columns: new[] { "EaTaskId", "ReviewCycleNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ea_task_reviews",
                schema: "public");
        }
    }
}
