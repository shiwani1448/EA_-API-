using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEaCalendarEventsGoogleCalendarStyle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                schema: "public",
                table: "ea_calendar_events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ea_calendar_events_EventType",
                schema: "public",
                table: "ea_calendar_events",
                column: "EventType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ea_calendar_events_EventType",
                schema: "public",
                table: "ea_calendar_events");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                schema: "public",
                table: "ea_calendar_events");
        }
    }
}
