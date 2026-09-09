using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hrms_api.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateAIScreeningTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateAIScreenings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidateId = table.Column<int>(type: "integer", nullable: false),
                    RequisitionId = table.Column<int>(type: "integer", nullable: true),
                    ResumePath = table.Column<string>(type: "text", nullable: true),
                    JDPath = table.Column<string>(type: "text", nullable: true),
                    ResumeExtractedText = table.Column<string>(type: "text", nullable: true),
                    JDExtractedText = table.Column<string>(type: "text", nullable: true),
                    OverallScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    Recommendation = table.Column<string>(type: "text", nullable: true),
                    Decision = table.Column<string>(type: "text", nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    RiskLevel = table.Column<string>(type: "text", nullable: true),
                    RoleFitScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    SkillFitScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    ExperienceRelevanceScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    AchievementImpactScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    CareerStabilityScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    EducationCertificationScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    IndustryAlignmentScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    GrowthPotentialScore = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    RedFlagDeduction = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    MatchedSkillsJson = table.Column<string>(type: "text", nullable: true),
                    MissingSkillsJson = table.Column<string>(type: "text", nullable: true),
                    StrengthsJson = table.Column<string>(type: "text", nullable: true),
                    ConcernsJson = table.Column<string>(type: "text", nullable: true),
                    RedFlagsJson = table.Column<string>(type: "text", nullable: true),
                    InterviewFocusAreasJson = table.Column<string>(type: "text", nullable: true),
                    ShortSummary = table.Column<string>(type: "text", nullable: true),
                    RawOllamaResponse = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateAIScreenings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateAIScreenings_CandidateId",
                table: "CandidateAIScreenings",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateAIScreenings_CandidateId_Status",
                table: "CandidateAIScreenings",
                columns: new[] { "CandidateId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateAIScreenings");
        }
    }
}
