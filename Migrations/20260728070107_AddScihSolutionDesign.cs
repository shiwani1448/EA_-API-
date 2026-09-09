using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jarvis5.Migrations
{
    /// <inheritdoc />
    public partial class AddScihSolutionDesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SCIH_SolutionDesign",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    SolutionJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    AIModel = table.Column<string>(type: "varchar(100)", nullable: true),
                    PromptVersion = table.Column<string>(type: "varchar(20)", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp", nullable: false),
                    GeneratedBy = table.Column<long>(type: "bigint", nullable: false),
                    IsEdited = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCIH_SolutionDesign", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SCIH_SolutionDesign_SCIH_Request_RequestId",
                        column: x => x.RequestId,
                        principalTable: "SCIH_Request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_SolutionDesign_RequestId",
                table: "SCIH_SolutionDesign",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SCIH_SolutionDesign");
        }
    }
}
