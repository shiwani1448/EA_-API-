using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hrms_api.Migrations
{
    /// <inheritdoc />
    public partial class StoreJDDocumentPdfAsPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentPdf",
                table: "JDMasters");

            migrationBuilder.AddColumn<string>(
                name: "DocumentPdfPath",
                table: "JDMasters",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentPdfPath",
                table: "JDMasters");

            migrationBuilder.AddColumn<byte[]>(
                name: "DocumentPdf",
                table: "JDMasters",
                type: "bytea",
                nullable: true);
        }
    }
}
