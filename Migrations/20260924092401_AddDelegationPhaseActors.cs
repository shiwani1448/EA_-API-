using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <summary>§0 identity: who started/ended each Delegation phase, resolved from the token (never "0").</summary>
    [DbContext(typeof(EaFmsDbContext))]
    [Migration("20260924092401_AddDelegationPhaseActors")]
    public partial class AddDelegationPhaseActors : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "StartedById", schema: "public", table: "ea_delegation_phase_tat", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "StartedByName", schema: "public", table: "ea_delegation_phase_tat", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "EndedById", schema: "public", table: "ea_delegation_phase_tat", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "EndedByName", schema: "public", table: "ea_delegation_phase_tat", type: "character varying(200)", maxLength: 200, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "StartedById", schema: "public", table: "ea_delegation_phase_tat");
            migrationBuilder.DropColumn(name: "StartedByName", schema: "public", table: "ea_delegation_phase_tat");
            migrationBuilder.DropColumn(name: "EndedById", schema: "public", table: "ea_delegation_phase_tat");
            migrationBuilder.DropColumn(name: "EndedByName", schema: "public", table: "ea_delegation_phase_tat");
        }
    }
}
