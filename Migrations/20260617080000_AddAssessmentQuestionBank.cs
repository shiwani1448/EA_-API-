using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hrms_api.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentQuestionBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssessmentQuestionBanks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Department = table.Column<string>(type: "text", nullable: false),
                    Designation = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<string>(type: "text", nullable: false),
                    RoundName = table.Column<string>(type: "text", nullable: false),
                    QuestionNo = table.Column<int>(type: "integer", nullable: false),
                    QuestionType = table.Column<string>(type: "text", nullable: false),
                    SkillArea = table.Column<string>(type: "text", nullable: true),
                    Difficulty = table.Column<string>(type: "text", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    ExpectedAnswer = table.Column<string>(type: "text", nullable: true),
                    MaxScore = table.Column<int>(type: "integer", nullable: false),
                    Weightage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentQuestionBanks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentQuestionBanks_Department_Designation_Level_RoundName_IsActive",
                table: "AssessmentQuestionBanks",
                columns: new[] { "Department", "Designation", "Level", "RoundName", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentQuestionBanks_Department_Designation_Level_RoundName_QuestionNo",
                table: "AssessmentQuestionBanks",
                columns: new[] { "Department", "Designation", "Level", "RoundName", "QuestionNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentQuestionBanks");
        }
    }
}
