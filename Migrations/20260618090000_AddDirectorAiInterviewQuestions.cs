using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hrms_api.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectorAiInterviewQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectorAiInterviewQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidateId = table.Column<int>(type: "integer", nullable: false),
                    InterviewRoundId = table.Column<int>(type: "integer", nullable: false),
                    ScreeningScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    DiscProfile = table.Column<string>(type: "text", nullable: true),
                    DScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    IScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    SScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    CScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    HrRoundScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    TechnicalAssessmentScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    OverallGapSummary = table.Column<string>(type: "text", nullable: true),
                    QuestionNo = table.Column<int>(type: "integer", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    WhyDirectorShouldAskThis = table.Column<string>(type: "text", nullable: true),
                    GapOrRiskArea = table.Column<string>(type: "text", nullable: true),
                    StrongAnswerSignals = table.Column<string>(type: "text", nullable: true),
                    RedFlagSignals = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectorAiInterviewQuestions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectorAiInterviewQuestions_CandidateId",
                table: "DirectorAiInterviewQuestions",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectorAiInterviewQuestions_InterviewRoundId",
                table: "DirectorAiInterviewQuestions",
                column: "InterviewRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectorAiInterviewQuestions_CandidateId_InterviewRoundId_QuestionNo",
                table: "DirectorAiInterviewQuestions",
                columns: new[] { "CandidateId", "InterviewRoundId", "QuestionNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectorAiInterviewQuestions");
        }
    }
}
