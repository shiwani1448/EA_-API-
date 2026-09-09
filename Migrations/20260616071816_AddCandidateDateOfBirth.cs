using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hrms_api.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateDateOfBirth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DateOfBirth",
                table: "Candidates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Candidates");
        }
    }
}
