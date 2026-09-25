using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingActionDelegationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssigneeId",
                schema: "public",
                table: "ea_meeting_actions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssigneeName",
                schema: "public",
                table: "ea_meeting_actions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DelegationType",
                schema: "public",
                table: "ea_meeting_actions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                schema: "public",
                table: "ea_meeting_actions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssigneeId",
                schema: "public",
                table: "ea_meeting_actions");

            migrationBuilder.DropColumn(
                name: "AssigneeName",
                schema: "public",
                table: "ea_meeting_actions");

            migrationBuilder.DropColumn(
                name: "DelegationType",
                schema: "public",
                table: "ea_meeting_actions");

            migrationBuilder.DropColumn(
                name: "StartDate",
                schema: "public",
                table: "ea_meeting_actions");
        }
    }
}
