using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddDelegationTypeAndStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DelegationType",
                schema: "public",
                table: "ea_delegations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                schema: "public",
                table: "ea_delegations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DelegationType",
                schema: "public",
                table: "ea_delegations");

            migrationBuilder.DropColumn(
                name: "StartDate",
                schema: "public",
                table: "ea_delegations");
        }
    }
}
