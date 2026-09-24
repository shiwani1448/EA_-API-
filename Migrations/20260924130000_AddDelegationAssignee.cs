using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <summary>
    /// Optional Delegation Assignee — a separate person from the Doer, supplied by the frontend
    /// exactly like the Doer (id + display-name snapshot). Nullable: existing rows have no assignee.
    /// </summary>
    [DbContext(typeof(EaFmsDbContext))]
    [Migration("20260924130000_AddDelegationAssignee")]
    public partial class AddDelegationAssignee : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssigneeId",
                schema: "public",
                table: "ea_delegations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssigneeNameSnapshot",
                schema: "public",
                table: "ea_delegations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AssigneeId", schema: "public", table: "ea_delegations");
            migrationBuilder.DropColumn(name: "AssigneeNameSnapshot", schema: "public", table: "ea_delegations");
        }
    }
}
