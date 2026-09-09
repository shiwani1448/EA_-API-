using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hrms_api.Migrations
{
    public partial class AddHiringRequestDirectorStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "HiringRequests"
                ADD COLUMN IF NOT EXISTS "DirectorStatus" text NOT NULL DEFAULT 'Pending';

                ALTER TABLE "HiringRequests"
                ADD COLUMN IF NOT EXISTS "DirectorActionDate" timestamp with time zone NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DirectorActionDate",
                table: "HiringRequests");

            migrationBuilder.DropColumn(
                name: "DirectorStatus",
                table: "HiringRequests");
        }
    }
}
