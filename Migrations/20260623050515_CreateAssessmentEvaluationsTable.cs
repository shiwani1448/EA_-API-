using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hrms_api.Migrations
{
    /// <inheritdoc />
    public partial class CreateAssessmentEvaluationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CandidateAIScreenings_BatchId",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "AIStatus",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "AiPromptLength",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "AiResponseLength",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "CandidateName",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "CurrentStep",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "ExceptionMessage",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "ExceptionType",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "ExtractionMethodUsed",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "FailureStep",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "InnerExceptionMessage",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "JDExtractionMethod",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "JDExtractionStatus",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "JDTextAvailable",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "NormalTextLength",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "OCRTextLength",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "PdfPageCount",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "RenderedImageCreated",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "RenderedImagePath",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "ResumeExtractionMethod",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "ResumeExtractionStatus",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "ResumeTextAvailable",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "ScreeningStatus",
                table: "CandidateAIScreenings");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "CandidateAIScreenings");

            migrationBuilder.CreateTable(
                name: "OnboardingTestBanks",
                columns: table => new
                {
                    QuestionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Category = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    DesignationId = table.Column<int>(type: "integer", nullable: true),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    QuestionType = table.Column<string>(type: "text", nullable: false),
                    Options = table.Column<string>(type: "text", nullable: true),
                    CorrectAnswer = table.Column<string>(type: "text", nullable: true),
                    Marks = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    DifficultyLevel = table.Column<string>(type: "text", nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingTestBanks", x => x.QuestionId);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingTestTemplates",
                columns: table => new
                {
                    TemplateId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TemplateName = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    DesignationId = table.Column<int>(type: "integer", nullable: true),
                    PassingPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    TotalQuestions = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingTestTemplates", x => x.TemplateId);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingTestAssignments",
                columns: table => new
                {
                    AssignmentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidateId = table.Column<int>(type: "integer", nullable: false),
                    OnboardingId = table.Column<int>(type: "integer", nullable: true),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalMarks = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    ObtainedMarks = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    ResultStatus = table.Column<string>(type: "text", nullable: false),
                    AttemptNo = table.Column<int>(type: "integer", nullable: false),
                    AssignedBy = table.Column<string>(type: "text", nullable: true),
                    ReviewedBy = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewRemarks = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingTestAssignments", x => x.AssignmentId);
                    table.ForeignKey(
                        name: "FK_OnboardingTestAssignments_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "CandidateId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardingTestAssignments_OnboardingTestTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "OnboardingTestTemplates",
                        principalColumn: "TemplateId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingTestTemplateQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    Marks = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingTestTemplateQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingTestTemplateQuestions_OnboardingTestBanks_Questio~",
                        column: x => x.QuestionId,
                        principalTable: "OnboardingTestBanks",
                        principalColumn: "QuestionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardingTestTemplateQuestions_OnboardingTestTemplates_Tem~",
                        column: x => x.TemplateId,
                        principalTable: "OnboardingTestTemplates",
                        principalColumn: "TemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingTestResponses",
                columns: table => new
                {
                    ResponseId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssignmentId = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    CandidateAnswer = table.Column<string>(type: "text", nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    Score = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    EvaluatedBy = table.Column<string>(type: "text", nullable: true),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EvaluatorRemarks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingTestResponses", x => x.ResponseId);
                    table.ForeignKey(
                        name: "FK_OnboardingTestResponses_OnboardingTestAssignments_Assignmen~",
                        column: x => x.AssignmentId,
                        principalTable: "OnboardingTestAssignments",
                        principalColumn: "AssignmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OnboardingTestResponses_OnboardingTestBanks_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "OnboardingTestBanks",
                        principalColumn: "QuestionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTestAssignments_CandidateId",
                table: "OnboardingTestAssignments",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTestAssignments_CandidateId_TemplateId_AttemptNo",
                table: "OnboardingTestAssignments",
                columns: new[] { "CandidateId", "TemplateId", "AttemptNo" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTestAssignments_TemplateId",
                table: "OnboardingTestAssignments",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTestBanks_Category_DepartmentId_DesignationId_IsA~",
                table: "OnboardingTestBanks",
                columns: new[] { "Category", "DepartmentId", "DesignationId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTestResponses_AssignmentId_QuestionId",
                table: "OnboardingTestResponses",
                columns: new[] { "AssignmentId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTestResponses_QuestionId",
                table: "OnboardingTestResponses",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTestTemplateQuestions_QuestionId",
                table: "OnboardingTestTemplateQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTestTemplateQuestions_TemplateId_QuestionId",
                table: "OnboardingTestTemplateQuestions",
                columns: new[] { "TemplateId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTestTemplates_DepartmentId_DesignationId_IsActive",
                table: "OnboardingTestTemplates",
                columns: new[] { "DepartmentId", "DesignationId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingTestResponses");

            migrationBuilder.DropTable(
                name: "OnboardingTestTemplateQuestions");

            migrationBuilder.DropTable(
                name: "OnboardingTestAssignments");

            migrationBuilder.DropTable(
                name: "OnboardingTestBanks");

            migrationBuilder.DropTable(
                name: "OnboardingTestTemplates");

            migrationBuilder.AddColumn<string>(
                name: "AIStatus",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiPromptLength",
                table: "CandidateAIScreenings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AiResponseLength",
                table: "CandidateAIScreenings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BatchId",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CandidateName",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "CandidateAIScreenings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStep",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "CandidateAIScreenings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionMessage",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionType",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractionMethodUsed",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureStep",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InnerExceptionMessage",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JDExtractionMethod",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JDExtractionStatus",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "JDTextAvailable",
                table: "CandidateAIScreenings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NormalTextLength",
                table: "CandidateAIScreenings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OCRTextLength",
                table: "CandidateAIScreenings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PdfPageCount",
                table: "CandidateAIScreenings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RenderedImageCreated",
                table: "CandidateAIScreenings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RenderedImagePath",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeExtractionMethod",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeExtractionStatus",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ResumeTextAvailable",
                table: "CandidateAIScreenings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ScreeningStatus",
                table: "CandidateAIScreenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "CandidateAIScreenings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateAIScreenings_BatchId",
                table: "CandidateAIScreenings",
                column: "BatchId");
        }
    }
}
