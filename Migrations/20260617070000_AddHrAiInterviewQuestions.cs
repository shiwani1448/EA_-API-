using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hrms_api.Migrations
{
    /// <inheritdoc />
    public partial class AddHrAiInterviewQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HrAiInterviewQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidateId = table.Column<int>(type: "integer", nullable: false),
                    InterviewRoundId = table.Column<int>(type: "integer", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: true),
                    Designation = table.Column<string>(type: "text", nullable: true),
                    DiscProfile = table.Column<string>(type: "text", nullable: true),
                    DScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    IScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    SScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    CScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    QuestionNo = table.Column<int>(type: "integer", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    WhyAskThis = table.Column<string>(type: "text", nullable: true),
                    ScoringGuideline = table.Column<string>(type: "text", nullable: true),
                    StrongAnswerSignals = table.Column<string>(type: "text", nullable: true),
                    RedFlagSignals = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrAiInterviewQuestions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HrAiInterviewQuestions_CandidateId",
                table: "HrAiInterviewQuestions",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_HrAiInterviewQuestions_InterviewRoundId",
                table: "HrAiInterviewQuestions",
                column: "InterviewRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_HrAiInterviewQuestions_CandidateId_InterviewRoundId_QuestionNo",
                table: "HrAiInterviewQuestions",
                columns: new[] { "CandidateId", "InterviewRoundId", "QuestionNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HrAiInterviewQuestions");
        }
    }
}
