using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTravelAndApprovalDecisionActors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                schema: "public",
                table: "ea_travel_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedBy",
                schema: "public",
                table: "ea_travel_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                schema: "public",
                table: "ea_approval_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedBy",
                schema: "public",
                table: "ea_approval_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                schema: "public",
                table: "ea_travel_requests");

            migrationBuilder.DropColumn(
                name: "RejectedBy",
                schema: "public",
                table: "ea_travel_requests");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                schema: "public",
                table: "ea_approval_requests");

            migrationBuilder.DropColumn(
                name: "RejectedBy",
                schema: "public",
                table: "ea_approval_requests");
        }
    }
}
