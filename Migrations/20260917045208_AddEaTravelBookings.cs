using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEaTravelBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ea_travel_bookings",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TravelRequestId = table.Column<long>(type: "bigint", nullable: false),
                    BookingType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BookingStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BookingReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BookingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DepartureDetails = table.Column<string>(type: "text", nullable: true),
                    ArrivalDetails = table.Column<string>(type: "text", nullable: true),
                    HotelDetails = table.Column<string>(type: "text", nullable: true),
                    VehicleDetails = table.Column<string>(type: "text", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_travel_bookings", x => x.Id);
                    table.CheckConstraint("CK_ea_travel_bookings_Cost", "\"Cost\" IS NULL OR \"Cost\" >= 0");
                    table.ForeignKey(
                        name: "FK_ea_travel_bookings_ea_travel_requests_TravelRequestId",
                        column: x => x.TravelRequestId,
                        principalSchema: "public",
                        principalTable: "ea_travel_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_bookings_BookingStatus",
                schema: "public",
                table: "ea_travel_bookings",
                column: "BookingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_bookings_TravelRequestId",
                schema: "public",
                table: "ea_travel_bookings",
                column: "TravelRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ea_travel_bookings",
                schema: "public");
        }
    }
}
