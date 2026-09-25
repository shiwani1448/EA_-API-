using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEaAiUsageUsedTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUsed",
                schema: "public",
                table: "ea_ai_usage_logs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UsedAt",
                schema: "public",
                table: "ea_ai_usage_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsedById",
                schema: "public",
                table: "ea_ai_usage_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsedByName",
                schema: "public",
                table: "ea_ai_usage_logs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsedValue",
                schema: "public",
                table: "ea_ai_usage_logs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsUsed",
                schema: "public",
                table: "ea_ai_usage_logs");

            migrationBuilder.DropColumn(
                name: "UsedAt",
                schema: "public",
                table: "ea_ai_usage_logs");

            migrationBuilder.DropColumn(
                name: "UsedById",
                schema: "public",
                table: "ea_ai_usage_logs");

            migrationBuilder.DropColumn(
                name: "UsedByName",
                schema: "public",
                table: "ea_ai_usage_logs");

            migrationBuilder.DropColumn(
                name: "UsedValue",
                schema: "public",
                table: "ea_ai_usage_logs");
        }
    }
}
