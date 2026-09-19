using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowupReminderDeliveryParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReminderRecipientUserId",
                schema: "public",
                table: "ea_followups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderSendEmail",
                schema: "public",
                table: "ea_followups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderSendWhatsApp",
                schema: "public",
                table: "ea_followups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReminderWhatsAppNumber",
                schema: "public",
                table: "ea_followups",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderRecipientUserId",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "ReminderSendEmail",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "ReminderSendWhatsApp",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "ReminderWhatsAppNumber",
                schema: "public",
                table: "ea_followups");
        }
    }
}
