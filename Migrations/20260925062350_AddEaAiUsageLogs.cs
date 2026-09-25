using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEaAiUsageLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ea_ai_usage_logs",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Feature = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BusinessRecordId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EaTaskId = table.Column<long>(type: "bigint", nullable: true),
                    RequestedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    AttemptNo = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StopReason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SystemPrompt = table.Column<string>(type: "text", nullable: true),
                    UserPrompt = table.Column<string>(type: "text", nullable: true),
                    ResponseText = table.Column<string>(type: "text", nullable: true),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    CacheCreationInputTokens = table.Column<long>(type: "bigint", nullable: true),
                    CacheReadInputTokens = table.Column<long>(type: "bigint", nullable: true),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_ai_usage_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_ai_usage_logs_EaTaskId",
                schema: "public",
                table: "ea_ai_usage_logs",
                column: "EaTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_ai_usage_logs_Module_BusinessRecordId",
                schema: "public",
                table: "ea_ai_usage_logs",
                columns: new[] { "Module", "BusinessRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_ea_ai_usage_logs_RequestedAt",
                schema: "public",
                table: "ea_ai_usage_logs",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ea_ai_usage_logs_RequestedById",
                schema: "public",
                table: "ea_ai_usage_logs",
                column: "RequestedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ea_ai_usage_logs",
                schema: "public");
        }
    }
}
