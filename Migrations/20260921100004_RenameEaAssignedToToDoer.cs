using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class RenameEaAssignedToToDoer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AssignedToName",
                schema: "public",
                table: "ea_workflow_instances",
                newName: "DoerName");

            migrationBuilder.RenameColumn(
                name: "AssignedToId",
                schema: "public",
                table: "ea_workflow_instances",
                newName: "DoerId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_workflow_instances_AssignedToId",
                schema: "public",
                table: "ea_workflow_instances",
                newName: "IX_ea_workflow_instances_DoerId");

            migrationBuilder.RenameColumn(
                name: "AssignedToName",
                schema: "public",
                table: "ea_work_assignments",
                newName: "DoerName");

            migrationBuilder.RenameColumn(
                name: "AssignedToId",
                schema: "public",
                table: "ea_work_assignments",
                newName: "DoerId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_work_assignments_AssignedToId",
                schema: "public",
                table: "ea_work_assignments",
                newName: "IX_ea_work_assignments_DoerId");

            migrationBuilder.RenameColumn(
                name: "OwnerName",
                schema: "public",
                table: "ea_meeting_actions",
                newName: "DoerName");

            migrationBuilder.RenameColumn(
                name: "AssignedToId",
                schema: "public",
                table: "ea_meeting_actions",
                newName: "DoerId");

            migrationBuilder.RenameColumn(
                name: "AssignedToName",
                schema: "public",
                table: "ea_intake_requests",
                newName: "DoerName");

            migrationBuilder.RenameColumn(
                name: "AssignedToId",
                schema: "public",
                table: "ea_intake_requests",
                newName: "DoerId");

            migrationBuilder.RenameColumn(
                name: "AssignedToName",
                schema: "public",
                table: "ea_followups",
                newName: "DoerName");

            migrationBuilder.RenameColumn(
                name: "AssignedToId",
                schema: "public",
                table: "ea_followups",
                newName: "DoerId");

            migrationBuilder.RenameColumn(
                name: "AssignedToNameSnapshot",
                schema: "public",
                table: "ea_delegations",
                newName: "DoerNameSnapshot");

            migrationBuilder.RenameColumn(
                name: "AssignedToId",
                schema: "public",
                table: "ea_delegations",
                newName: "DoerId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_delegations_AssignedToId",
                schema: "public",
                table: "ea_delegations",
                newName: "IX_ea_delegations_DoerId");
            // PostgreSQL 18+ keeps NOT NULL as a named constraint that RENAME COLUMN does not rename.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'public.ea_delegations'::regclass AND conname = 'ea_delegations_AssignedToId_not_null') THEN
                        ALTER TABLE public.ea_delegations RENAME CONSTRAINT "ea_delegations_AssignedToId_not_null" TO "ea_delegations_DoerId_not_null";
                    END IF;
                END $$;
                """);
            // PostgreSQL 18+ keeps NOT NULL as a named constraint that RENAME COLUMN does not rename.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'public.ea_work_assignments'::regclass AND conname = 'ea_work_assignments_AssignedToId_not_null') THEN
                        ALTER TABLE public.ea_work_assignments RENAME CONSTRAINT "ea_work_assignments_AssignedToId_not_null" TO "ea_work_assignments_DoerId_not_null";
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL 18+ keeps NOT NULL as a named constraint that RENAME COLUMN does not rename.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'public.ea_delegations'::regclass AND conname = 'ea_delegations_DoerId_not_null') THEN
                        ALTER TABLE public.ea_delegations RENAME CONSTRAINT "ea_delegations_DoerId_not_null" TO "ea_delegations_AssignedToId_not_null";
                    END IF;
                END $$;
                """);
            // PostgreSQL 18+ keeps NOT NULL as a named constraint that RENAME COLUMN does not rename.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'public.ea_work_assignments'::regclass AND conname = 'ea_work_assignments_DoerId_not_null') THEN
                        ALTER TABLE public.ea_work_assignments RENAME CONSTRAINT "ea_work_assignments_DoerId_not_null" TO "ea_work_assignments_AssignedToId_not_null";
                    END IF;
                END $$;
                """);

            migrationBuilder.RenameColumn(
                name: "DoerName",
                schema: "public",
                table: "ea_workflow_instances",
                newName: "AssignedToName");

            migrationBuilder.RenameColumn(
                name: "DoerId",
                schema: "public",
                table: "ea_workflow_instances",
                newName: "AssignedToId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_workflow_instances_DoerId",
                schema: "public",
                table: "ea_workflow_instances",
                newName: "IX_ea_workflow_instances_AssignedToId");

            migrationBuilder.RenameColumn(
                name: "DoerName",
                schema: "public",
                table: "ea_work_assignments",
                newName: "AssignedToName");

            migrationBuilder.RenameColumn(
                name: "DoerId",
                schema: "public",
                table: "ea_work_assignments",
                newName: "AssignedToId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_work_assignments_DoerId",
                schema: "public",
                table: "ea_work_assignments",
                newName: "IX_ea_work_assignments_AssignedToId");

            migrationBuilder.RenameColumn(
                name: "DoerName",
                schema: "public",
                table: "ea_meeting_actions",
                newName: "OwnerName");

            migrationBuilder.RenameColumn(
                name: "DoerId",
                schema: "public",
                table: "ea_meeting_actions",
                newName: "AssignedToId");

            migrationBuilder.RenameColumn(
                name: "DoerName",
                schema: "public",
                table: "ea_intake_requests",
                newName: "AssignedToName");

            migrationBuilder.RenameColumn(
                name: "DoerId",
                schema: "public",
                table: "ea_intake_requests",
                newName: "AssignedToId");

            migrationBuilder.RenameColumn(
                name: "DoerName",
                schema: "public",
                table: "ea_followups",
                newName: "AssignedToName");

            migrationBuilder.RenameColumn(
                name: "DoerId",
                schema: "public",
                table: "ea_followups",
                newName: "AssignedToId");

            migrationBuilder.RenameColumn(
                name: "DoerNameSnapshot",
                schema: "public",
                table: "ea_delegations",
                newName: "AssignedToNameSnapshot");

            migrationBuilder.RenameColumn(
                name: "DoerId",
                schema: "public",
                table: "ea_delegations",
                newName: "AssignedToId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_delegations_DoerId",
                schema: "public",
                table: "ea_delegations",
                newName: "IX_ea_delegations_AssignedToId");
        }
    }
}
