using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class MeetingPriorityStringStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Priority", schema: "public", table: "ea_meetings",
                type: "character varying(100)", maxLength: 100, nullable: true);

            migrationBuilder.Sql("DO $migration$\nBEGIN\n    IF EXISTS (SELECT 1 FROM public.ea_meetings m LEFT JOIN public.ea_priority_levels p ON p.\"Id\" = m.\"PriorityLevelId\" WHERE m.\"PriorityLevelId\" IS NOT NULL AND p.\"Id\" IS NULL) THEN\n        RAISE EXCEPTION 'Cannot migrate ea_meetings.PriorityLevelId: orphaned priority master references exist.';\n    END IF;\nEND $migration$;");
            migrationBuilder.Sql("UPDATE public.ea_meetings m SET \"Priority\" = p.\"Name\" FROM public.ea_priority_levels p WHERE m.\"PriorityLevelId\" = p.\"Id\";");

            migrationBuilder.DropIndex(name: "IX_ea_meetings_PriorityLevelId", schema: "public", table: "ea_meetings");
            migrationBuilder.DropColumn(name: "PriorityLevelId", schema: "public", table: "ea_meetings");
            migrationBuilder.CreateIndex(name: "IX_ea_meetings_Priority", schema: "public", table: "ea_meetings", column: "Priority");
        }
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ea_meetings_Priority",
                schema: "public",
                table: "ea_meetings");

            migrationBuilder.DropColumn(
                name: "Priority",
                schema: "public",
                table: "ea_meetings");

            migrationBuilder.AddColumn<int>(
                name: "PriorityLevelId",
                schema: "public",
                table: "ea_meetings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ea_meetings_PriorityLevelId",
                schema: "public",
                table: "ea_meetings",
                column: "PriorityLevelId");
        }
    }
}
