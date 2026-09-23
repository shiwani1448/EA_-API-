using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddDelegationPhaseTatSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TatPausedSeconds",
                schema: "public",
                table: "ea_delegation_phase_tat",
                type: "numeric(20,7)",
                precision: 20,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TatUsedSeconds",
                schema: "public",
                table: "ea_delegation_phase_tat",
                type: "numeric(20,7)",
                precision: 20,
                scale: 7,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TatPausedSeconds",
                schema: "public",
                table: "ea_delegation_phase_tat");

            migrationBuilder.DropColumn(
                name: "TatUsedSeconds",
                schema: "public",
                table: "ea_delegation_phase_tat");
        }
    }
}
