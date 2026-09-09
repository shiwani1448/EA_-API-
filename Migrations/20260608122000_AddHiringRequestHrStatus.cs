using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hrms_api.Migrations
{
    /// <inheritdoc />
    [Migration("20260608122000_AddHiringRequestHrStatus")]
    public partial class AddHiringRequestHrStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HrStatus",
                table: "HiringRequests",
                type: "text",
                nullable: false,
                defaultValue: "Pending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HrStatus",
                table: "HiringRequests");
        }
    }
}
