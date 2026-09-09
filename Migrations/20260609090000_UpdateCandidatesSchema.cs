using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hrms_api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCandidatesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Resume",
                table: "Candidates");

            migrationBuilder.AddColumn<string>(
                name: "ResumePath",
                table: "Candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Candidates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResumePath",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Candidates");

            migrationBuilder.AddColumn<byte[]>(
                name: "Resume",
                table: "Candidates",
                type: "bytea",
                nullable: true);
        }
    }
}
