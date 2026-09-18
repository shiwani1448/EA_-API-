using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEaTaskExecutionStatusAndTiming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                schema: "public",
                table: "ea_tasks",
                type: "timestamp with time zone",
                nullable: true);

            // Added nullable first, backfilled deterministically per module below, then
            // locked to NOT NULL — same two-step pattern already used by
            // 20260915103914_AddModuleNameToEaTatRules and
            // 20260917071159_AddEaTaskCentralSnapshotFields. No fabricated history: every
            // value below is derived from an existing, immutable authoritative column on
            // the owning module's own row (WorkflowInstance.TatStartedAt/CompletedAt,
            // TravelRequest.BusinessState/StartedAt/CompletedAt, ApprovalRequest.
            // WorkflowStatus/SubmittedAt + the latest decided ApprovalCycle.UpdatedAt,
            // Delegation.Status/StartedAt/CompletedAt) — nothing here is guessed.
            migrationBuilder.AddColumn<string>(
                name: "ExecutionStatus",
                schema: "public",
                table: "ea_tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                schema: "public",
                table: "ea_tasks",
                type: "timestamp with time zone",
                nullable: true);

            // Meeting: the only module with a WorkflowInstance today. TatStartedAt/
            // CompletedAt are already the authoritative, set-once execution timestamps.
            migrationBuilder.Sql("""
                UPDATE public.ea_tasks AS t
                SET "StartedAt" = w."TatStartedAt",
                    "CompletedAt" = w."CompletedAt",
                    "ExecutionStatus" = CASE
                        WHEN w."CompletedAt" IS NOT NULL THEN 'Completed'
                        WHEN w."TatStartedAt" IS NOT NULL THEN 'InProgress'
                        ELSE 'NotStarted'
                    END
                FROM public.ea_workflow_instances AS w
                WHERE w."Id" = t."WorkflowInstanceId";
                """);

            // Travel & Hospitality: TravelRequest.StartedAt/CompletedAt are already the
            // authoritative operational-trip timestamps set once by Start/Complete.
            migrationBuilder.Sql("""
                UPDATE public.ea_tasks AS t
                SET "StartedAt" = tr."StartedAt",
                    "CompletedAt" = tr."CompletedAt",
                    "ExecutionStatus" = CASE tr."BusinessState"
                        WHEN 'Completed' THEN 'Completed'
                        WHEN 'Cancelled' THEN 'Cancelled'
                        WHEN 'Active' THEN 'InProgress'
                        ELSE 'NotStarted'
                    END
                FROM public.ea_travel_requests AS tr
                JOIN public.ea_business_modules AS m ON m."Name" = 'Travel & Hospitality'
                WHERE t."WorkflowInstanceId" IS NULL
                    AND t."BusinessModuleId" = m."Id"
                    AND t."BusinessRecordId" = tr."Id"::text;
                """);

            // EA Approval: SubmittedAt already equals the request's creation instant (no
            // separate start step exists). CompletedAt is derived from the most recently
            // decided cycle's UpdatedAt (ApprovalRequest.ApprovedAt/RejectedAt are declared
            // but never populated by the current code, so they are not usable here).
            migrationBuilder.Sql("""
                UPDATE public.ea_tasks AS t
                SET "StartedAt" = ar."SubmittedAt",
                    "CompletedAt" = (
                        SELECT MAX(c."UpdatedAt")
                        FROM public.ea_approval_cycles AS c
                        WHERE c."ApprovalRequestId" = ar."Id" AND c."Status" IN ('Approved', 'Rejected')
                    ),
                    "ExecutionStatus" = CASE ar."WorkflowStatus"
                        WHEN 'Approved' THEN 'Completed'
                        WHEN 'Rejected' THEN 'Completed'
                        WHEN 'Draft' THEN 'NotStarted'
                        ELSE 'InProgress'
                    END
                FROM public.ea_approval_requests AS ar
                JOIN public.ea_business_modules AS m ON m."Name" = 'EA Approval'
                WHERE t."WorkflowInstanceId" IS NULL
                    AND t."BusinessModuleId" = m."Id"
                    AND t."BusinessRecordId" = ar."ReferenceNo";
                """);

            // Delegation: StartedAt/CompletedAt are reserved on the entity but never
            // populated yet (Start/Complete are Step 4, not implemented) — both stay NULL;
            // only ExecutionStatus is deterministic from the existing Status column.
            migrationBuilder.Sql("""
                UPDATE public.ea_tasks AS t
                SET "StartedAt" = d."StartedAt",
                    "CompletedAt" = d."CompletedAt",
                    "ExecutionStatus" = CASE d."Status"
                        WHEN 'Completed' THEN 'Completed'
                        WHEN 'InProgress' THEN 'InProgress'
                        ELSE 'NotStarted'
                    END
                FROM public.ea_delegations AS d
                JOIN public.ea_business_modules AS m ON m."Name" = 'Delegation'
                WHERE t."WorkflowInstanceId" IS NULL
                    AND t."BusinessModuleId" = m."Id"
                    AND t."BusinessRecordId" = d."Id"::text;
                """);

            // Any row not covered above (no other module creates EaTasks today) safely
            // defaults to NotStarted rather than being left NULL.
            migrationBuilder.Sql("""
                UPDATE public.ea_tasks
                SET "ExecutionStatus" = 'NotStarted'
                WHERE "ExecutionStatus" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ExecutionStatus",
                schema: "public",
                table: "ea_tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ea_tasks_ExecutionStatus",
                schema: "public",
                table: "ea_tasks",
                column: "ExecutionStatus");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ea_tasks_ExecutionStatus_Valid",
                schema: "public",
                table: "ea_tasks",
                sql: "\"ExecutionStatus\" IN ('NotStarted', 'InProgress', 'Completed', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ea_tasks_ExecutionStatus",
                schema: "public",
                table: "ea_tasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ea_tasks_ExecutionStatus_Valid",
                schema: "public",
                table: "ea_tasks");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                schema: "public",
                table: "ea_tasks");

            migrationBuilder.DropColumn(
                name: "ExecutionStatus",
                schema: "public",
                table: "ea_tasks");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                schema: "public",
                table: "ea_tasks");
        }
    }
}
