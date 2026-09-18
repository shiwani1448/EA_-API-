using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class RelaxTravelCountConstraintsToNonNegative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ea_travel_requests_NumberOfRooms_Positive",
                schema: "public",
                table: "ea_travel_requests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ea_travel_requests_NumberOfTravellers_Positive",
                schema: "public",
                table: "ea_travel_requests");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ea_travel_requests_NumberOfRooms_NonNegative",
                schema: "public",
                table: "ea_travel_requests",
                sql: "\"NumberOfRooms\" IS NULL OR \"NumberOfRooms\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ea_travel_requests_NumberOfTravellers_NonNegative",
                schema: "public",
                table: "ea_travel_requests",
                sql: "\"NumberOfTravellers\" IS NULL OR \"NumberOfTravellers\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ea_travel_requests_NumberOfRooms_NonNegative",
                schema: "public",
                table: "ea_travel_requests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ea_travel_requests_NumberOfTravellers_NonNegative",
                schema: "public",
                table: "ea_travel_requests");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ea_travel_requests_NumberOfRooms_Positive",
                schema: "public",
                table: "ea_travel_requests",
                sql: "\"NumberOfRooms\" IS NULL OR \"NumberOfRooms\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ea_travel_requests_NumberOfTravellers_Positive",
                schema: "public",
                table: "ea_travel_requests",
                sql: "\"NumberOfTravellers\" IS NULL OR \"NumberOfTravellers\" > 0");
        }
    }
}
