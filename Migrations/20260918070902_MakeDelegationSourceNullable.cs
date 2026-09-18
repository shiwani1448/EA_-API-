using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class MakeDelegationSourceNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Widen the columns to nullable first, so the data cleanup below (step 2) can
            //    actually write NULL into them.
            migrationBuilder.AlterColumn<string>(
                name: "SourceEntityId",
                schema: "public",
                table: "ea_delegations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<long>(
                name: "SourceBusinessModuleId",
                schema: "public",
                table: "ea_delegations",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            // 2. Architectural correction: "Manual / Direct Delegation" was a fake source
            //    BusinessModule invented only to satisfy the old NOT NULL constraint on
            //    SourceBusinessModuleId. A direct/manual Delegation has no originating module
            //    or record at all — that is different from "originated from Delegation
            //    itself" — so existing rows sourced from the obsolete module are corrected to
            //    NULL/NULL, never to the canonical Delegation module's id. The obsolete module
            //    is located by its exact Name, never by a hardcoded numeric id.
            //    SourceReference is left untouched — it is a free-text business note, not an
            //    artifact of the fake-module workaround, and nothing here fabricates or
            //    removes user-entered content.
            migrationBuilder.Sql(
                "UPDATE public.ea_delegations " +
                "SET \"SourceBusinessModuleId\" = NULL, \"SourceEntityId\" = NULL " +
                "WHERE \"SourceBusinessModuleId\" = (" +
                "  SELECT \"Id\" FROM public.ea_business_modules WHERE \"Name\" = 'Manual / Direct Delegation'" +
                ");");

            // 3. Now that no Delegation references it, remove the obsolete BusinessModule row.
            //    The canonical "Delegation" BusinessModule is never touched by this statement.
            migrationBuilder.Sql(
                "DELETE FROM public.ea_business_modules WHERE \"Name\" = 'Manual / Direct Delegation';");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Schema-only reversal. The obsolete "Manual / Direct Delegation" BusinessModule row
        /// and the original SourceBusinessModuleId/SourceEntityId values it satisfied are not
        /// recreated — that data is gone once Up() runs, and fabricating a replacement row or
        /// re-linking guesses would violate the "never fabricate a source" rule this migration
        /// exists to enforce. This Down() will fail on a database that (correctly, post-Up())
        /// contains legitimate NULL SourceBusinessModuleId rows, since Postgres cannot add a
        /// NOT NULL constraint over existing NULLs — expected, not a bug.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SourceEntityId",
                schema: "public",
                table: "ea_delegations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "SourceBusinessModuleId",
                schema: "public",
                table: "ea_delegations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
