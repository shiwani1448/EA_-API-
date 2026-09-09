using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jarvis5.Migrations
{
    /// <inheritdoc />
    public partial class AddScihApprovalAndAnalysisVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SCIH_Analysis_RequestId",
                table: "SCIH_Analysis");

            migrationBuilder.AddColumn<string>(
                name: "ComparisonJson",
                table: "SCIH_Analysis",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PreviousAnalysisId",
                table: "SCIH_Analysis",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReworkCount",
                table: "SCIH_Analysis",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "SCIH_Analysis",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "SCIH_Approval",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    ApprovalRound = table.Column<int>(type: "integer", nullable: false),
                    ReworkCount = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "varchar(30)", nullable: false),
                    Comments = table.Column<string>(type: "text", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    ImprovementAreasJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "timestamp", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCIH_Approval", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SCIH_Approval_SCIH_Request_RequestId",
                        column: x => x.RequestId,
                        principalTable: "SCIH_Request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Analysis_PreviousAnalysisId",
                table: "SCIH_Analysis",
                column: "PreviousAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Analysis_RequestId",
                table: "SCIH_Analysis",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Analysis_RequestId_Version",
                table: "SCIH_Analysis",
                columns: new[] { "RequestId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Approval_RequestId",
                table: "SCIH_Approval",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Approval_RequestId_ApprovalRound",
                table: "SCIH_Approval",
                columns: new[] { "RequestId", "ApprovalRound" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SCIH_Analysis_SCIH_Analysis_PreviousAnalysisId",
                table: "SCIH_Analysis",
                column: "PreviousAnalysisId",
                principalTable: "SCIH_Analysis",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SCIH_Analysis_SCIH_Analysis_PreviousAnalysisId",
                table: "SCIH_Analysis");

            migrationBuilder.DropTable(
                name: "SCIH_Approval");

            migrationBuilder.DropIndex(
                name: "IX_SCIH_Analysis_PreviousAnalysisId",
                table: "SCIH_Analysis");

            migrationBuilder.DropIndex(
                name: "IX_SCIH_Analysis_RequestId",
                table: "SCIH_Analysis");

            migrationBuilder.DropIndex(
                name: "IX_SCIH_Analysis_RequestId_Version",
                table: "SCIH_Analysis");

            migrationBuilder.DropColumn(
                name: "ComparisonJson",
                table: "SCIH_Analysis");

            migrationBuilder.DropColumn(
                name: "PreviousAnalysisId",
                table: "SCIH_Analysis");

            migrationBuilder.DropColumn(
                name: "ReworkCount",
                table: "SCIH_Analysis");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "SCIH_Analysis");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Analysis_RequestId",
                table: "SCIH_Analysis",
                column: "RequestId",
                unique: true);
        }
    }
}
