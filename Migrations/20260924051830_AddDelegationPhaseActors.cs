using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddDelegationPhaseActors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndedById",
                schema: "public",
                table: "ea_delegation_phase_tat",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndedByName",
                schema: "public",
                table: "ea_delegation_phase_tat",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartedById",
                schema: "public",
                table: "ea_delegation_phase_tat",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartedByName",
                schema: "public",
                table: "ea_delegation_phase_tat",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndedById",
                schema: "public",
                table: "ea_delegation_phase_tat");

            migrationBuilder.DropColumn(
                name: "EndedByName",
                schema: "public",
                table: "ea_delegation_phase_tat");

            migrationBuilder.DropColumn(
                name: "StartedById",
                schema: "public",
                table: "ea_delegation_phase_tat");

            migrationBuilder.DropColumn(
                name: "StartedByName",
                schema: "public",
                table: "ea_delegation_phase_tat");
        }
    }
}
