using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEaTravelFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "ea_travel_no_seq");

            migrationBuilder.CreateTable(
                name: "ea_travel_requests",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReferenceNo = table.Column<string>(type: "varchar(40)", nullable: false),
                    EaTaskId = table.Column<long>(type: "bigint", nullable: false),
                    CurrentCycleNo = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TravellerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmployeePersonId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactInformation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Purpose = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TravelType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FromLocation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ToLocation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DepartureDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NumberOfTravellers = table.Column<int>(type: "integer", nullable: true),
                    Priority = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SpecialRequirements = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransportType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PreferredDeparture = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreferredArrival = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClassPreference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BookingRequirements = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Hotel = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CheckInDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckOutDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NumberOfRooms = table.Column<int>(type: "integer", nullable: true),
                    RoomPreference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LocationPreference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PickupRequired = table.Column<bool>(type: "boolean", nullable: true),
                    PickupLocation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DropLocation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VehiclePreference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClientGuestDetails = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HospitalityRequirement = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    MeetingEventPurpose = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NumberOfGuests = table.Column<int>(type: "integer", nullable: true),
                    SpecialArrangements = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ItineraryNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AdditionalInstructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EstimatedTravelCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    EstimatedHotelCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    EstimatedLocalTransportCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    EstimatedHospitalityCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ApprovalRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ApproverId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApproverNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BusinessState = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    ApprovalState = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "NotRequired"),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_travel_requests", x => x.Id);
                    table.CheckConstraint("CK_ea_travel_requests_ApprovalState", "\"ApprovalState\" IN ('NotRequired', 'NotSubmitted', 'Pending', 'ChangesRequested', 'Approved', 'Rejected')");
                    table.CheckConstraint("CK_ea_travel_requests_BusinessState", "\"BusinessState\" IN ('Draft', 'Upcoming', 'Active', 'Completed', 'Cancelled')");
                    table.CheckConstraint("CK_ea_travel_requests_CurrentCycleNo_NonNegative", "\"CurrentCycleNo\" >= 0");
                    table.CheckConstraint("CK_ea_travel_requests_EstimatedCosts_NonNegative", "(\"EstimatedTravelCost\" IS NULL OR \"EstimatedTravelCost\" >= 0) AND (\"EstimatedHotelCost\" IS NULL OR \"EstimatedHotelCost\" >= 0) AND (\"EstimatedLocalTransportCost\" IS NULL OR \"EstimatedLocalTransportCost\" >= 0) AND (\"EstimatedHospitalityCost\" IS NULL OR \"EstimatedHospitalityCost\" >= 0)");
                    table.CheckConstraint("CK_ea_travel_requests_HotelDates", "\"CheckInDate\" IS NULL OR \"CheckOutDate\" IS NULL OR \"CheckOutDate\" >= \"CheckInDate\"");
                    table.CheckConstraint("CK_ea_travel_requests_NumberOfGuests_NonNegative", "\"NumberOfGuests\" IS NULL OR \"NumberOfGuests\" >= 0");
                    table.CheckConstraint("CK_ea_travel_requests_NumberOfRooms_Positive", "\"NumberOfRooms\" IS NULL OR \"NumberOfRooms\" > 0");
                    table.CheckConstraint("CK_ea_travel_requests_NumberOfTravellers_Positive", "\"NumberOfTravellers\" IS NULL OR \"NumberOfTravellers\" > 0");
                    table.CheckConstraint("CK_ea_travel_requests_TravelDates", "\"DepartureDate\" IS NULL OR \"ReturnDate\" IS NULL OR \"ReturnDate\" >= \"DepartureDate\"");
                    table.ForeignKey(
                        name: "FK_ea_travel_requests_ea_tasks_EaTaskId",
                        column: x => x.EaTaskId,
                        principalSchema: "public",
                        principalTable: "ea_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ea_travel_request_cycles",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TravelRequestId = table.Column<long>(type: "bigint", nullable: false),
                    CycleNo = table.Column<int>(type: "integer", nullable: false),
                    SubmittedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApproverId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApproverNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DecisionState = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ChangeReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ChangesMade = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DecisionComment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DecisionBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DecisionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_travel_request_cycles", x => x.Id);
                    table.CheckConstraint("CK_ea_travel_request_cycles_CycleNo_Positive", "\"CycleNo\" > 0");
                    table.CheckConstraint("CK_ea_travel_request_cycles_DecisionState", "\"DecisionState\" IS NULL OR \"DecisionState\" IN ('Pending', 'ChangesRequested', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_ea_travel_request_cycles_ea_travel_requests_TravelRequestId",
                        column: x => x.TravelRequestId,
                        principalSchema: "public",
                        principalTable: "ea_travel_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_request_cycles_TravelRequestId",
                schema: "public",
                table: "ea_travel_request_cycles",
                column: "TravelRequestId");

            migrationBuilder.CreateIndex(
                name: "UX_ea_travel_request_cycles_TravelRequestId_CycleNo",
                schema: "public",
                table: "ea_travel_request_cycles",
                columns: new[] { "TravelRequestId", "CycleNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_requests_ApprovalState",
                schema: "public",
                table: "ea_travel_requests",
                column: "ApprovalState");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_requests_ApproverId",
                schema: "public",
                table: "ea_travel_requests",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_requests_BusinessState",
                schema: "public",
                table: "ea_travel_requests",
                column: "BusinessState");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_requests_CreatedBy",
                schema: "public",
                table: "ea_travel_requests",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_requests_DepartureDate",
                schema: "public",
                table: "ea_travel_requests",
                column: "DepartureDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_requests_EaTaskId",
                schema: "public",
                table: "ea_travel_requests",
                column: "EaTaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_requests_ModifiedDate",
                schema: "public",
                table: "ea_travel_requests",
                column: "ModifiedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_requests_Priority",
                schema: "public",
                table: "ea_travel_requests",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_requests_ReferenceNo",
                schema: "public",
                table: "ea_travel_requests",
                column: "ReferenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_requests_RequiredDate",
                schema: "public",
                table: "ea_travel_requests",
                column: "RequiredDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ea_travel_request_cycles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_travel_requests",
                schema: "public");

            migrationBuilder.DropSequence(
                name: "ea_travel_no_seq");
        }
    }
}
