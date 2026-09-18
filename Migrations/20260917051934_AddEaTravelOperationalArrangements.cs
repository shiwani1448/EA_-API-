using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEaTravelOperationalArrangements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ea_travel_hospitality_arrangements",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TravelRequestId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClientGuestDetails = table.Column<string>(type: "text", nullable: true),
                    HospitalityRequirement = table.Column<string>(type: "text", nullable: true),
                    MeetingEventPurpose = table.Column<string>(type: "text", nullable: true),
                    NumberOfGuests = table.Column<int>(type: "integer", nullable: true),
                    SpecialArrangements = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ActualCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_ea_travel_hospitality_arrangements", x => x.Id);
                    table.CheckConstraint("CK_ea_travel_hospitality_arrangements_ActualCost", "\"ActualCost\" IS NULL OR \"ActualCost\" >= 0");
                    table.CheckConstraint("CK_ea_travel_hospitality_arrangements_EstimatedCost", "\"EstimatedCost\" IS NULL OR \"EstimatedCost\" >= 0");
                    table.CheckConstraint("CK_ea_travel_hospitality_arrangements_NumberOfGuests", "\"NumberOfGuests\" IS NULL OR \"NumberOfGuests\" >= 0");
                    table.ForeignKey(
                        name: "FK_ea_travel_hospitality_arrangements_ea_travel_requests_Trave~",
                        column: x => x.TravelRequestId,
                        principalSchema: "public",
                        principalTable: "ea_travel_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ea_travel_local_transports",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TravelRequestId = table.Column<long>(type: "bigint", nullable: false),
                    TransportStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TransportType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PickupLocation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DropLocation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VehiclePreference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BookingReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ActualCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_ea_travel_local_transports", x => x.Id);
                    table.CheckConstraint("CK_ea_travel_local_transports_ActualCost", "\"ActualCost\" IS NULL OR \"ActualCost\" >= 0");
                    table.CheckConstraint("CK_ea_travel_local_transports_EstimatedCost", "\"EstimatedCost\" IS NULL OR \"EstimatedCost\" >= 0");
                    table.ForeignKey(
                        name: "FK_ea_travel_local_transports_ea_travel_requests_TravelRequest~",
                        column: x => x.TravelRequestId,
                        principalSchema: "public",
                        principalTable: "ea_travel_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_hospitality_arrangements_TravelRequestId",
                schema: "public",
                table: "ea_travel_hospitality_arrangements",
                column: "TravelRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_local_transports_TravelRequestId",
                schema: "public",
                table: "ea_travel_local_transports",
                column: "TravelRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ea_travel_hospitality_arrangements",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_travel_local_transports",
                schema: "public");
        }
    }
}
