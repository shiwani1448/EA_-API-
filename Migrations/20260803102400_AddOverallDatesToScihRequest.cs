using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jarvis5.Migrations
{
    /// <inheritdoc />
    public partial class AddOverallDatesToScihRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OverallEndDate",
                table: "SCIH_Request",
                type: "timestamp",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverallStartDate",
                table: "SCIH_Request",
                type: "timestamp",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverallEndDate",
                table: "SCIH_Request");

            migrationBuilder.DropColumn(
                name: "OverallStartDate",
                table: "SCIH_Request");
        }
    }
}
