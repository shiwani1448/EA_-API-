using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class ConvertMeetingActionPriorityToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the new string column first, alongside the still-present PriorityLevelId,
            //    so historical data can be backfilled before the old column is dropped.
            migrationBuilder.AddColumn<string>(
                name: "Priority",
                schema: "public",
                table: "ea_meeting_actions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // 2. Preserve historical rows: map every resolvable PriorityLevelId to its
            //    PriorityLevel.Name. Rows whose PriorityLevelId no longer resolves (deleted/
            //    unknown master data) are left null rather than fabricated.
            migrationBuilder.Sql(
                "UPDATE public.ea_meeting_actions AS a " +
                "SET \"Priority\" = pl.\"Name\" " +
                "FROM public.ea_priority_levels AS pl " +
                "WHERE a.\"PriorityLevelId\" = pl.\"Id\";");

            // 3. Drop the old FK-shaped catalog-gated column now that its data is preserved.
            migrationBuilder.DropIndex(
                name: "IX_ea_meeting_actions_PriorityLevelId",
                schema: "public",
                table: "ea_meeting_actions");

            migrationBuilder.DropColumn(
                name: "PriorityLevelId",
                schema: "public",
                table: "ea_meeting_actions");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_actions_Priority",
                schema: "public",
                table: "ea_meeting_actions",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ea_meeting_actions_Priority",
                schema: "public",
                table: "ea_meeting_actions");

            migrationBuilder.AddColumn<int>(
                name: "PriorityLevelId",
                schema: "public",
                table: "ea_meeting_actions",
                type: "integer",
                nullable: true);

            // Best-effort reverse mapping: an exact case-insensitive PriorityLevel.Name match.
            // A free-text Priority with no matching PriorityLevel is left null rather than guessed.
            migrationBuilder.Sql(
                "UPDATE public.ea_meeting_actions AS a " +
                "SET \"PriorityLevelId\" = pl.\"Id\" " +
                "FROM public.ea_priority_levels AS pl " +
                "WHERE lower(a.\"Priority\") = lower(pl.\"Name\");");

            migrationBuilder.DropColumn(
                name: "Priority",
                schema: "public",
                table: "ea_meeting_actions");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_actions_PriorityLevelId",
                schema: "public",
                table: "ea_meeting_actions",
                column: "PriorityLevelId");
        }
    }
}
