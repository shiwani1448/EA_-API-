using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowupEmployeeRecipientSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReminderRecipientEmail",
                schema: "public",
                table: "ea_followups",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReminderRecipientEmployeeId",
                schema: "public",
                table: "ea_followups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReminderRecipientName",
                schema: "public",
                table: "ea_followups",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderRecipientEmail",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "ReminderRecipientEmployeeId",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "ReminderRecipientName",
                schema: "public",
                table: "ea_followups");
        }
    }
}
