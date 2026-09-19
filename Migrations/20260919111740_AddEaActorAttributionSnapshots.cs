using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEaActorAttributionSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByEmployeeId",
                schema: "public",
                table: "ea_tat_rules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByEmployeeName",
                schema: "public",
                table: "ea_tat_rules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedByEmployeeId",
                schema: "public",
                table: "ea_tat_rules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedByEmployeeName",
                schema: "public",
                table: "ea_tat_rules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByEmployeeId",
                schema: "public",
                table: "ea_followups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByEmployeeName",
                schema: "public",
                table: "ea_followups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedByEmployeeId",
                schema: "public",
                table: "ea_followups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedByEmployeeName",
                schema: "public",
                table: "ea_followups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowedUpByEmployeeId",
                schema: "public",
                table: "ea_followup_cycles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowedUpByEmployeeName",
                schema: "public",
                table: "ea_followup_cycles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByEmployeeId",
                schema: "public",
                table: "ea_business_modules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByEmployeeName",
                schema: "public",
                table: "ea_business_modules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedByEmployeeId",
                schema: "public",
                table: "ea_business_modules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedByEmployeeName",
                schema: "public",
                table: "ea_business_modules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByEmployeeId",
                schema: "public",
                table: "ea_tat_rules");

            migrationBuilder.DropColumn(
                name: "CreatedByEmployeeName",
                schema: "public",
                table: "ea_tat_rules");

            migrationBuilder.DropColumn(
                name: "ModifiedByEmployeeId",
                schema: "public",
                table: "ea_tat_rules");

            migrationBuilder.DropColumn(
                name: "ModifiedByEmployeeName",
                schema: "public",
                table: "ea_tat_rules");

            migrationBuilder.DropColumn(
                name: "CreatedByEmployeeId",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "CreatedByEmployeeName",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "ModifiedByEmployeeId",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "ModifiedByEmployeeName",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "FollowedUpByEmployeeId",
                schema: "public",
                table: "ea_followup_cycles");

            migrationBuilder.DropColumn(
                name: "FollowedUpByEmployeeName",
                schema: "public",
                table: "ea_followup_cycles");

            migrationBuilder.DropColumn(
                name: "CreatedByEmployeeId",
                schema: "public",
                table: "ea_business_modules");

            migrationBuilder.DropColumn(
                name: "CreatedByEmployeeName",
                schema: "public",
                table: "ea_business_modules");

            migrationBuilder.DropColumn(
                name: "ModifiedByEmployeeId",
                schema: "public",
                table: "ea_business_modules");

            migrationBuilder.DropColumn(
                name: "ModifiedByEmployeeName",
                schema: "public",
                table: "ea_business_modules");
        }
    }
}
