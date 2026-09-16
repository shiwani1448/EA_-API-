using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleNameToEaTatRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModuleName",
                schema: "public",
                table: "ea_tat_rules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM public.ea_tat_rules AS t
                        LEFT JOIN public.ea_business_modules AS m ON m."Id" = t."BusinessModuleId"
                        WHERE m."Id" IS NULL)
                    THEN
                        RAISE EXCEPTION 'Cannot backfill ea_tat_rules.ModuleName because one or more BusinessModuleId values have no matching business module.';
                    END IF;
                END $$;

                UPDATE public.ea_tat_rules AS t
                SET "ModuleName" = m."Name"
                FROM public.ea_business_modules AS m
                WHERE m."Id" = t."BusinessModuleId";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ModuleName",
                schema: "public",
                table: "ea_tat_rules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModuleName",
                schema: "public",
                table: "ea_tat_rules");
        }
    }
}
