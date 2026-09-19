using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <summary>
    /// Syncs the model snapshot with the escalation-level table the EA database already uses:
    /// public.ea_escalation_levels. The previous migration chain left the snapshot on the
    /// convention name "EscalationLevels" (no explicit mapping), which is why
    /// GET /api/ea/escalation-levels failed with 42P01.
    ///
    /// Every statement is guarded so it is a no-op on a database that already has the
    /// ea_escalation_levels shape (the current EA database) and only renames/repairs a
    /// database built purely from the earlier migrations. No data is created or changed.
    /// </summary>
    public partial class SyncEscalationLevelTableMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF to_regclass('public.""EscalationLevels""') IS NOT NULL
                       AND to_regclass('public.ea_escalation_levels') IS NULL THEN
                        ALTER TABLE public.ea_escalations
                            DROP CONSTRAINT IF EXISTS ""FK_ea_escalations_EscalationLevels_EscalationLevelId"";
                        ALTER TABLE public.""EscalationLevels"" RENAME TO ea_escalation_levels;
                        ALTER TABLE public.ea_escalation_levels
                            RENAME CONSTRAINT ""PK_EscalationLevels"" TO ""PK_escalation_levels"";
                        ALTER TABLE public.ea_escalations
                            ADD CONSTRAINT ""FK_ea_escalations_ea_escalation_levels_EscalationLevelId""
                            FOREIGN KEY (""EscalationLevelId"") REFERENCES public.ea_escalation_levels (""Id"")
                            ON DELETE CASCADE;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_escalation_levels_Code"" ON public.ea_escalation_levels (""Code"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_escalation_levels_Level"" ON public.ea_escalation_levels (""Level"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally a no-op: the table name ea_escalation_levels is the canonical
            // name already used by the live EA database, so there is nothing safe to revert.
        }
    }
}
