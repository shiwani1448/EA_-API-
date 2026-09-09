using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hrms_api.Migrations
{
    /// <inheritdoc />
    [Migration("20260608114641_StoreJDDocumentPdfAsBytes")]
    public partial class StoreJDDocumentPdfAsBytes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "JDMasters"
                ALTER COLUMN "DocumentPdf" TYPE bytea
                USING NULL;
                """);

            migrationBuilder.AddColumn<string>(
                name: "DocumentPdfFileName",
                table: "JDMasters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentPdfContentType",
                table: "JDMasters",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentPdfFileName",
                table: "JDMasters");

            migrationBuilder.DropColumn(
                name: "DocumentPdfContentType",
                table: "JDMasters");

            migrationBuilder.Sql("""
                ALTER TABLE "JDMasters"
                ALTER COLUMN "DocumentPdf" TYPE text
                USING NULL;
                """);
        }
    }
}
