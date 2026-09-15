using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class FinalMeetingContractAndTypedTat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ea_tat_rules_ActiveModule",
                schema: "public",
                table: "ea_tat_rules");

            migrationBuilder.AddColumn<string>(
                name: "Subtype",
                schema: "public",
                table: "ea_tat_rules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "public",
                table: "ea_tat_rules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_ea_tat_rules_ActiveClassification"
                ON public.ea_tat_rules ("BusinessModuleId", lower(btrim("Type")), lower(btrim("Subtype")))
                WHERE "IsActive" AND NOT "IsDeleted"
                  AND "Type" IS NOT NULL AND "Subtype" IS NOT NULL
                  AND btrim("Type") <> '' AND btrim("Subtype") <> '';
                """);

            migrationBuilder.AddColumn<string>(
                name: "CompletionMom",
                schema: "public",
                table: "ea_meetings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CompletionPdfAttachmentId",
                schema: "public",
                table: "ea_meetings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "DoerIds",
                schema: "public",
                table: "ea_meetings",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<string[]>(
                name: "DoerNames",
                schema: "public",
                table: "ea_meetings",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meetings_CompletionPdfAttachmentId",
                schema: "public",
                table: "ea_meetings",
                column: "CompletionPdfAttachmentId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ea_meetings_DoerPairs",
                schema: "public",
                table: "ea_meetings",
                sql: "cardinality(\"DoerIds\") = cardinality(\"DoerNames\") AND array_position(\"DoerIds\", NULL) IS NULL AND array_position(\"DoerNames\", NULL) IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ea_meetings_ea_attachments_CompletionPdfAttachmentId",
                schema: "public",
                table: "ea_meetings",
                column: "CompletionPdfAttachmentId",
                principalSchema: "public",
                principalTable: "ea_attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX public.\"UX_ea_tat_rules_ActiveClassification\";");
            // Reverting to module-only identity must fail before any data is removed if typed rules conflict.
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM public.ea_tat_rules WHERE \"IsActive\" AND NOT \"IsDeleted\" GROUP BY \"BusinessModuleId\" HAVING count(*) > 1) THEN RAISE EXCEPTION 'Cannot restore module-only uniqueness while multiple active typed rules exist'; END IF; END $$;");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_meetings_ea_attachments_CompletionPdfAttachmentId",
                schema: "public",
                table: "ea_meetings");

            migrationBuilder.DropIndex(
                name: "IX_ea_meetings_CompletionPdfAttachmentId",
                schema: "public",
                table: "ea_meetings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ea_meetings_DoerPairs",
                schema: "public",
                table: "ea_meetings");

            migrationBuilder.DropColumn(
                name: "Subtype",
                schema: "public",
                table: "ea_tat_rules");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "public",
                table: "ea_tat_rules");

            migrationBuilder.DropColumn(
                name: "CompletionMom",
                schema: "public",
                table: "ea_meetings");

            migrationBuilder.DropColumn(
                name: "CompletionPdfAttachmentId",
                schema: "public",
                table: "ea_meetings");

            migrationBuilder.DropColumn(
                name: "DoerIds",
                schema: "public",
                table: "ea_meetings");

            migrationBuilder.DropColumn(
                name: "DoerNames",
                schema: "public",
                table: "ea_meetings");

            migrationBuilder.CreateIndex(
                name: "UX_ea_tat_rules_ActiveModule",
                schema: "public",
                table: "ea_tat_rules",
                column: "BusinessModuleId",
                unique: true,
                filter: "\"IsActive\" = true AND \"IsDeleted\" = false");
        }
    }
}
