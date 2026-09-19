using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class ConvertTravelTravellersToStructuredRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ea_travel_travellers",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TravelRequestId = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TravellerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmployeePersonId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactInformation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_travel_travellers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_travel_travellers_ea_travel_requests_TravelRequestId",
                        column: x => x.TravelRequestId,
                        principalSchema: "public",
                        principalTable: "ea_travel_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_travel_travellers_TravelRequestId_SortOrder",
                schema: "public",
                table: "ea_travel_travellers",
                columns: new[] { "TravelRequestId", "SortOrder" });

            // Legacy data migration. The legacy columns ea_travel_requests.TravellerName,
            // TravellerNames, EmployeePersonId, Department and ContactInformation are NOT
            // dropped: they stay in place (nullable, unmapped, no longer written) so nothing
            // is lost and the change is reversible.
            //
            // 1) Rows with exactly ONE traveller name: one traveller row carrying the legacy
            //    EmployeePersonId / Department / ContactInformation (unambiguous).
            // 2) Rows with SEVERAL traveller names: one row per name (order preserved) with the
            //    name only. The legacy scalar values cannot be attributed to a particular
            //    person, so they are NOT copied onto any (or every) traveller.
            migrationBuilder.Sql(@"
                INSERT INTO public.ea_travel_travellers
                    (""TravelRequestId"", ""SortOrder"", ""TravellerName"", ""EmployeePersonId"", ""Department"", ""ContactInformation"")
                SELECT s.""Id"", (u.ord - 1)::int, u.n,
                       CASE WHEN cardinality(s.names) = 1 THEN NULLIF(BTRIM(s.""EmployeePersonId""), '') END,
                       CASE WHEN cardinality(s.names) = 1 THEN NULLIF(BTRIM(s.""Department""), '') END,
                       CASE WHEN cardinality(s.names) = 1 THEN NULLIF(BTRIM(s.""ContactInformation""), '') END
                FROM (
                    SELECT r.""Id"", r.""EmployeePersonId"", r.""Department"", r.""ContactInformation"",
                           COALESCE(
                               (SELECT array_agg(BTRIM(x.n) ORDER BY x.ord)
                                FROM unnest(r.""TravellerNames"") WITH ORDINALITY AS x(n, ord)
                                WHERE BTRIM(x.n) <> ''),
                               CASE WHEN BTRIM(COALESCE(r.""TravellerName"", '')) <> ''
                                    THEN ARRAY[BTRIM(r.""TravellerName"")] END) AS names
                    FROM public.ea_travel_requests r
                ) s
                CROSS JOIN LATERAL unnest(s.names) WITH ORDINALITY AS u(n, ord)
                WHERE s.names IS NOT NULL;
            ");

            // 3) Rows with NO traveller name but some legacy employee/department/contact value:
            //    exactly one set of details exists, so keep it as one traveller row (name NULL).
            migrationBuilder.Sql(@"
                INSERT INTO public.ea_travel_travellers
                    (""TravelRequestId"", ""SortOrder"", ""TravellerName"", ""EmployeePersonId"", ""Department"", ""ContactInformation"")
                SELECT r.""Id"", 0, NULL,
                       NULLIF(BTRIM(r.""EmployeePersonId""), ''),
                       NULLIF(BTRIM(r.""Department""), ''),
                       NULLIF(BTRIM(r.""ContactInformation""), '')
                FROM public.ea_travel_requests r
                WHERE NOT EXISTS (SELECT 1 FROM public.ea_travel_travellers t WHERE t.""TravelRequestId"" = r.""Id"")
                  AND (BTRIM(COALESCE(r.""EmployeePersonId"", '')) <> ''
                    OR BTRIM(COALESCE(r.""Department"", '')) <> ''
                    OR BTRIM(COALESCE(r.""ContactInformation"", '')) <> '');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ea_travel_travellers",
                schema: "public");
        }
    }
}
