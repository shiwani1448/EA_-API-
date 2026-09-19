using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class SupportMultipleTravelTravellerNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "TravellerNames",
                schema: "public",
                table: "ea_travel_requests",
                type: "text[]",
                nullable: true);

            // Data migration: copy existing single TravellerName values into the new
            // TravellerNames array. Rows with a non-null, non-empty TravellerName get
            // ARRAY[TravellerName]; rows where TravellerName IS NULL stay NULL.
            // This is a one-time migration — the legacy TravellerName column is retained
            // in the schema but the application no longer writes to it.
            migrationBuilder.Sql(@"
                UPDATE public.ea_travel_requests
                SET ""TravellerNames"" = ARRAY[""TravellerName""]
                WHERE ""TravellerName"" IS NOT NULL
                  AND TRIM(""TravellerName"") <> '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TravellerNames",
                schema: "public",
                table: "ea_travel_requests");
        }
    }
}
