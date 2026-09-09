using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <inheritdoc />
    public partial class CompleteMeetingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_escalations_escalation_levels_EscalationLevelId",
                table: "escalations");

            migrationBuilder.DropForeignKey(
                name: "FK_escalations_followups_FollowupId",
                table: "escalations");

            migrationBuilder.DropForeignKey(
                name: "FK_followups_intake_requests_IntakeRequestId",
                table: "followups");

            migrationBuilder.DropForeignKey(
                name: "FK_followups_workflow_instances_WorkflowInstanceId",
                table: "followups");

            migrationBuilder.DropForeignKey(
                name: "FK_intake_classifications_intake_requests_IntakeRequestId",
                table: "intake_classifications");

            migrationBuilder.DropForeignKey(
                name: "FK_intake_requests_business_modules_BusinessModuleId",
                table: "intake_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_intake_requests_priority_levels_PriorityLevelId",
                table: "intake_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_intake_requests_statuses_StatusId",
                table: "intake_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_workflow_history_workflow_instances_WorkflowInstanceId",
                table: "workflow_history");

            migrationBuilder.DropForeignKey(
                name: "FK_workflow_instances_intake_requests_IntakeRequestId",
                table: "workflow_instances");

            migrationBuilder.DropForeignKey(
                name: "FK_workflow_instances_statuses_StatusId",
                table: "workflow_instances");

            migrationBuilder.DropPrimaryKey(
                name: "PK_workflow_instances",
                table: "workflow_instances");

            migrationBuilder.DropPrimaryKey(
                name: "PK_workflow_history",
                table: "workflow_history");

            migrationBuilder.DropPrimaryKey(
                name: "PK_statuses",
                table: "statuses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_priority_levels",
                table: "priority_levels");

            migrationBuilder.DropPrimaryKey(
                name: "PK_notifications",
                table: "notifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_intake_requests",
                table: "intake_requests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_intake_classifications",
                table: "intake_classifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_followups",
                table: "followups");

            migrationBuilder.DropIndex(
                name: "IX_followups_CreatedDate",
                table: "followups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_escalations",
                table: "escalations");

            migrationBuilder.DropIndex(
                name: "IX_escalations_InitiatedAt",
                table: "escalations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_escalation_levels",
                table: "escalation_levels");

            migrationBuilder.DropIndex(
                name: "IX_escalation_levels_Code",
                table: "escalation_levels");

            migrationBuilder.DropIndex(
                name: "IX_escalation_levels_Level",
                table: "escalation_levels");

            migrationBuilder.DropPrimaryKey(
                name: "PK_business_modules",
                table: "business_modules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_audit_logs",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_OccurredAt",
                table: "audit_logs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_attachments",
                table: "attachments");

            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.RenameTable(
                name: "workflow_instances",
                newName: "ea_workflow_instances",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "workflow_history",
                newName: "ea_workflow_history",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "statuses",
                newName: "ea_statuses",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "priority_levels",
                newName: "ea_priority_levels",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "notifications",
                newName: "ea_notifications",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "intake_requests",
                newName: "ea_intake_requests",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "intake_classifications",
                newName: "ea_intake_classifications",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "followups",
                newName: "ea_followups",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "escalations",
                newName: "ea_escalations",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "escalation_levels",
                newName: "EscalationLevels");

            migrationBuilder.RenameTable(
                name: "business_modules",
                newName: "ea_business_modules",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "audit_logs",
                newName: "ea_audit_logs",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "attachments",
                newName: "ea_attachments",
                newSchema: "public");

            migrationBuilder.RenameIndex(
                name: "IX_workflow_instances_StatusId",
                schema: "public",
                table: "ea_workflow_instances",
                newName: "IX_ea_workflow_instances_StatusId");

            migrationBuilder.RenameIndex(
                name: "IX_workflow_instances_StartedAt",
                schema: "public",
                table: "ea_workflow_instances",
                newName: "IX_ea_workflow_instances_StartedAt");

            migrationBuilder.RenameIndex(
                name: "IX_workflow_instances_IsActive",
                schema: "public",
                table: "ea_workflow_instances",
                newName: "IX_ea_workflow_instances_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_workflow_instances_IntakeRequestId",
                schema: "public",
                table: "ea_workflow_instances",
                newName: "IX_ea_workflow_instances_IntakeRequestId");

            migrationBuilder.RenameIndex(
                name: "IX_workflow_history_WorkflowInstanceId",
                schema: "public",
                table: "ea_workflow_history",
                newName: "IX_ea_workflow_history_WorkflowInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_workflow_history_ToStatusId",
                schema: "public",
                table: "ea_workflow_history",
                newName: "IX_ea_workflow_history_ToStatusId");

            migrationBuilder.RenameIndex(
                name: "IX_workflow_history_ChangedAt",
                schema: "public",
                table: "ea_workflow_history",
                newName: "IX_ea_workflow_history_ChangedAt");

            migrationBuilder.RenameIndex(
                name: "IX_statuses_IsActive",
                schema: "public",
                table: "ea_statuses",
                newName: "IX_ea_statuses_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_statuses_DisplayOrder",
                schema: "public",
                table: "ea_statuses",
                newName: "IX_ea_statuses_DisplayOrder");

            migrationBuilder.RenameIndex(
                name: "IX_priority_levels_Level",
                schema: "public",
                table: "ea_priority_levels",
                newName: "IX_ea_priority_levels_Level");

            migrationBuilder.RenameIndex(
                name: "IX_priority_levels_IsActive",
                schema: "public",
                table: "ea_priority_levels",
                newName: "IX_ea_priority_levels_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_notifications_RecipientId",
                schema: "public",
                table: "ea_notifications",
                newName: "IX_ea_notifications_RecipientId");

            migrationBuilder.RenameIndex(
                name: "IX_notifications_IsRead",
                schema: "public",
                table: "ea_notifications",
                newName: "IX_ea_notifications_IsRead");

            migrationBuilder.RenameIndex(
                name: "IX_notifications_IsActive",
                schema: "public",
                table: "ea_notifications",
                newName: "IX_ea_notifications_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_notifications_CreatedDate",
                schema: "public",
                table: "ea_notifications",
                newName: "IX_ea_notifications_CreatedDate");

            migrationBuilder.RenameIndex(
                name: "IX_intake_requests_StatusId",
                schema: "public",
                table: "ea_intake_requests",
                newName: "IX_ea_intake_requests_StatusId");

            migrationBuilder.RenameIndex(
                name: "IX_intake_requests_PriorityLevelId",
                schema: "public",
                table: "ea_intake_requests",
                newName: "IX_ea_intake_requests_PriorityLevelId");

            migrationBuilder.RenameIndex(
                name: "IX_intake_requests_IsActive",
                schema: "public",
                table: "ea_intake_requests",
                newName: "IX_ea_intake_requests_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_intake_requests_CreatedDate",
                schema: "public",
                table: "ea_intake_requests",
                newName: "IX_ea_intake_requests_CreatedDate");

            migrationBuilder.RenameIndex(
                name: "IX_intake_requests_BusinessModuleId",
                schema: "public",
                table: "ea_intake_requests",
                newName: "IX_ea_intake_requests_BusinessModuleId");

            migrationBuilder.RenameIndex(
                name: "IX_intake_classifications_Name",
                schema: "public",
                table: "ea_intake_classifications",
                newName: "IX_ea_intake_classifications_Name");

            migrationBuilder.RenameIndex(
                name: "IX_intake_classifications_IntakeRequestId",
                schema: "public",
                table: "ea_intake_classifications",
                newName: "IX_ea_intake_classifications_IntakeRequestId");

            migrationBuilder.RenameIndex(
                name: "IX_followups_WorkflowInstanceId",
                schema: "public",
                table: "ea_followups",
                newName: "IX_ea_followups_WorkflowInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_followups_IntakeRequestId",
                schema: "public",
                table: "ea_followups",
                newName: "IX_ea_followups_IntakeRequestId");

            migrationBuilder.RenameIndex(
                name: "IX_followups_DueAt",
                schema: "public",
                table: "ea_followups",
                newName: "IX_ea_followups_DueAt");

            migrationBuilder.RenameIndex(
                name: "IX_escalations_FollowupId",
                schema: "public",
                table: "ea_escalations",
                newName: "IX_ea_escalations_FollowupId");

            migrationBuilder.RenameIndex(
                name: "IX_escalations_EscalationLevelId",
                schema: "public",
                table: "ea_escalations",
                newName: "IX_ea_escalations_EscalationLevelId");

            migrationBuilder.RenameIndex(
                name: "IX_business_modules_Name",
                schema: "public",
                table: "ea_business_modules",
                newName: "IX_ea_business_modules_Name");

            migrationBuilder.RenameIndex(
                name: "IX_business_modules_IsActive",
                schema: "public",
                table: "ea_business_modules",
                newName: "IX_ea_business_modules_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_audit_logs_Module",
                schema: "public",
                table: "ea_audit_logs",
                newName: "IX_ea_audit_logs_Module");

            migrationBuilder.RenameIndex(
                name: "IX_audit_logs_EntityId",
                schema: "public",
                table: "ea_audit_logs",
                newName: "IX_ea_audit_logs_EntityId");

            migrationBuilder.RenameIndex(
                name: "IX_audit_logs_CreatedDate",
                schema: "public",
                table: "ea_audit_logs",
                newName: "IX_ea_audit_logs_CreatedDate");

            migrationBuilder.RenameIndex(
                name: "IX_audit_logs_ActorId",
                schema: "public",
                table: "ea_audit_logs",
                newName: "IX_ea_audit_logs_ActorId");

            migrationBuilder.RenameIndex(
                name: "IX_audit_logs_ActionType",
                schema: "public",
                table: "ea_audit_logs",
                newName: "IX_ea_audit_logs_ActionType");

            migrationBuilder.RenameIndex(
                name: "IX_attachments_UploadedBy",
                schema: "public",
                table: "ea_attachments",
                newName: "IX_ea_attachments_UploadedBy");

            migrationBuilder.RenameIndex(
                name: "IX_attachments_UploadedAt",
                schema: "public",
                table: "ea_attachments",
                newName: "IX_ea_attachments_UploadedAt");

            migrationBuilder.RenameIndex(
                name: "IX_attachments_RelatedModule",
                schema: "public",
                table: "ea_attachments",
                newName: "IX_ea_attachments_RelatedModule");

            migrationBuilder.RenameIndex(
                name: "IX_attachments_RelatedEntityId",
                schema: "public",
                table: "ea_attachments",
                newName: "IX_ea_attachments_RelatedEntityId");

            migrationBuilder.RenameIndex(
                name: "IX_attachments_IsActive",
                schema: "public",
                table: "ea_attachments",
                newName: "IX_ea_attachments_IsActive");

            migrationBuilder.AlterColumn<long>(
                name: "IntakeRequestId",
                schema: "public",
                table: "ea_workflow_instances",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                schema: "public",
                table: "ea_workflow_instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedToId",
                schema: "public",
                table: "ea_workflow_instances",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedToName",
                schema: "public",
                table: "ea_workflow_instances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BusinessModuleId",
                schema: "public",
                table: "ea_workflow_instances",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessRecordId",
                schema: "public",
                table: "ea_workflow_instances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TatStartedAt",
                schema: "public",
                table: "ea_workflow_instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StageOwnerId",
                schema: "public",
                table: "ea_workflow_history",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StageOwnerName",
                schema: "public",
                table: "ea_workflow_history",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransitionType",
                schema: "public",
                table: "ea_workflow_history",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "public",
                table: "ea_notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceModule",
                schema: "public",
                table: "ea_notifications",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceId",
                schema: "public",
                table: "ea_notifications",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                schema: "public",
                table: "ea_notifications",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedToId",
                schema: "public",
                table: "ea_intake_requests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedToName",
                schema: "public",
                table: "ea_intake_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConfidential",
                schema: "public",
                table: "ea_intake_requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequiredDate",
                schema: "public",
                table: "ea_intake_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "public",
                table: "ea_intake_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceChannel",
                schema: "public",
                table: "ea_intake_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReferenceId",
                schema: "public",
                table: "ea_intake_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "IntakeRequestId",
                schema: "public",
                table: "ea_followups",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "AssignedToId",
                schema: "public",
                table: "ea_followups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedToName",
                schema: "public",
                table: "ea_followups",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BusinessModuleId",
                schema: "public",
                table: "ea_followups",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessRecordId",
                schema: "public",
                table: "ea_followups",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletedById",
                schema: "public",
                table: "ea_followups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletedByName",
                schema: "public",
                table: "ea_followups",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionNote",
                schema: "public",
                table: "ea_followups",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedResponseAt",
                schema: "public",
                table: "ea_followups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFollowupAt",
                schema: "public",
                table: "ea_followups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextFollowupAt",
                schema: "public",
                table: "ea_followups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutcomeCode",
                schema: "public",
                table: "ea_followups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriorityLevelId",
                schema: "public",
                table: "ea_followups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderAt",
                schema: "public",
                table: "ea_followups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseOwnerId",
                schema: "public",
                table: "ea_followups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseOwnerName",
                schema: "public",
                table: "ea_followups",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                schema: "public",
                table: "ea_followups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                schema: "public",
                table: "ea_followups",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "public",
                table: "ea_followups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaitingOnExternal",
                schema: "public",
                table: "ea_followups",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaitingOnId",
                schema: "public",
                table: "ea_followups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaitingOnName",
                schema: "public",
                table: "ea_followups",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "FollowupId",
                schema: "public",
                table: "ea_escalations",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<DateTime>(
                name: "AcknowledgedAt",
                schema: "public",
                table: "ea_escalations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedById",
                schema: "public",
                table: "ea_escalations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedByName",
                schema: "public",
                table: "ea_escalations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgementNote",
                schema: "public",
                table: "ea_escalations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BusinessModuleId",
                schema: "public",
                table: "ea_escalations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessRecordId",
                schema: "public",
                table: "ea_escalations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalatedToId",
                schema: "public",
                table: "ea_escalations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalatedToName",
                schema: "public",
                table: "ea_escalations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IntakeRequestId",
                schema: "public",
                table: "ea_escalations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                schema: "public",
                table: "ea_escalations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                schema: "public",
                table: "ea_escalations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextEscalationAt",
                schema: "public",
                table: "ea_escalations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NextEscalationLevelId",
                schema: "public",
                table: "ea_escalations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionNote",
                schema: "public",
                table: "ea_escalations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedById",
                schema: "public",
                table: "ea_escalations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedByName",
                schema: "public",
                table: "ea_escalations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WorkflowInstanceId",
                schema: "public",
                table: "ea_escalations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "EscalationLevels",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "EscalationLevels",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "EscalationLevels",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "EscalationLevels",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "public",
                table: "ea_audit_logs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActionType",
                schema: "public",
                table: "ea_audit_logs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "public",
                table: "ea_attachments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ea_workflow_instances",
                schema: "public",
                table: "ea_workflow_instances",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ea_workflow_history",
                schema: "public",
                table: "ea_workflow_history",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ea_statuses",
                schema: "public",
                table: "ea_statuses",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ea_priority_levels",
                schema: "public",
                table: "ea_priority_levels",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ea_notifications",
                schema: "public",
                table: "ea_notifications",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ea_intake_requests",
                schema: "public",
                table: "ea_intake_requests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ea_intake_classifications",
                schema: "public",
                table: "ea_intake_classifications",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ea_followups",
                schema: "public",
                table: "ea_followups",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ea_escalations",
                schema: "public",
                table: "ea_escalations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EscalationLevels",
                table: "EscalationLevels",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ea_business_modules",
                schema: "public",
                table: "ea_business_modules",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ea_audit_logs",
                schema: "public",
                table: "ea_audit_logs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ea_attachments",
                schema: "public",
                table: "ea_attachments",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ea_meetings",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IntakeRequestId = table.Column<long>(type: "bigint", nullable: true),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Purpose = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MeetingType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceChannel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceReferenceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MeetingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MeetingMode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MeetingLink = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OrganizerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrganizerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PriorityLevelId = table.Column<int>(type: "integer", nullable: true),
                    StatusId = table.Column<int>(type: "integer", nullable: true),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AgendaDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MinutesDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsConfidential = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_meetings_ea_intake_requests_IntakeRequestId",
                        column: x => x.IntakeRequestId,
                        principalSchema: "public",
                        principalTable: "ea_intake_requests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ea_meetings_ea_workflow_instances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalSchema: "public",
                        principalTable: "ea_workflow_instances",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ea_tat_rules",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessModuleId = table.Column<int>(type: "integer", nullable: false),
                    OperationCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PriorityLevelId = table.Column<int>(type: "integer", nullable: true),
                    Minutes = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_tat_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ea_work_assignments",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IntakeRequestId = table.Column<long>(type: "bigint", nullable: true),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    FollowupId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedToId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AssignedToName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AssignedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AssignedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnassignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnassignedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UnassignedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AssignmentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_work_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_work_assignments_ea_followups_FollowupId",
                        column: x => x.FollowupId,
                        principalSchema: "public",
                        principalTable: "ea_followups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ea_work_assignments_ea_intake_requests_IntakeRequestId",
                        column: x => x.IntakeRequestId,
                        principalSchema: "public",
                        principalTable: "ea_intake_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ea_work_assignments_ea_workflow_instances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalSchema: "public",
                        principalTable: "ea_workflow_instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ea_work_pauses",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IntakeRequestId = table.Column<long>(type: "bigint", nullable: true),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    FollowupId = table.Column<long>(type: "bigint", nullable: true),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    WaitingOnId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WaitingOnName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WaitingOnExternal = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResponseOwnerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResponseOwnerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExpectedResponseAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResumedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResumedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResumedReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_work_pauses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_work_pauses_ea_followups_FollowupId",
                        column: x => x.FollowupId,
                        principalSchema: "public",
                        principalTable: "ea_followups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ea_work_pauses_ea_intake_requests_IntakeRequestId",
                        column: x => x.IntakeRequestId,
                        principalSchema: "public",
                        principalTable: "ea_intake_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ea_work_pauses_ea_workflow_instances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalSchema: "public",
                        principalTable: "ea_workflow_instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ea_work_revisions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    BusinessModuleId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessRecordId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequestedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewerRemarks = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RespondedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RespondedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResponseRemarks = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResolvedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_work_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_work_revisions_ea_workflow_instances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalSchema: "public",
                        principalTable: "ea_workflow_instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ea_meeting_actions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<long>(type: "bigint", nullable: false),
                    ActionRecordId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OwnerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PriorityLevelId = table.Column<int>(type: "integer", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_meeting_actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_meeting_actions_ea_meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalSchema: "public",
                        principalTable: "ea_meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ea_meeting_agendas",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<long>(type: "bigint", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OwnerId = table.Column<string>(type: "text", nullable: true),
                    OwnerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsPrepared = table.Column<bool>(type: "boolean", nullable: false),
                    PreparedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreparedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PreparedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequiredBy = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_meeting_agendas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_meeting_agendas_ea_meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalSchema: "public",
                        principalTable: "ea_meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ea_meeting_attendees",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<long>(type: "bigint", nullable: false),
                    ParticipantId = table.Column<string>(type: "text", nullable: true),
                    ParticipantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Organization = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InvitationStatus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AttendanceStatus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InvitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_meeting_attendees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_meeting_attendees_ea_meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalSchema: "public",
                        principalTable: "ea_meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ea_meeting_decisions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<long>(type: "bigint", nullable: false),
                    Decision = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OwnerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DecisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DecisionRecordId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_meeting_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_meeting_decisions_ea_meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalSchema: "public",
                        principalTable: "ea_meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ea_meeting_minutes",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<long>(type: "bigint", nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DiscussionNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PreparedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PreparedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PreparedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubmittedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_meeting_minutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ea_meeting_minutes_ea_meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalSchema: "public",
                        principalTable: "ea_meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ea_workflow_instances_AssignedToId",
                schema: "public",
                table: "ea_workflow_instances",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_workflow_instances_TatStartedAt",
                schema: "public",
                table: "ea_workflow_instances",
                column: "TatStartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ea_workflow_history_StageOwnerId",
                schema: "public",
                table: "ea_workflow_history",
                column: "StageOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_intake_requests_RequiredDate",
                schema: "public",
                table: "ea_intake_requests",
                column: "RequiredDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_followups_BusinessModuleId",
                schema: "public",
                table: "ea_followups",
                column: "BusinessModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_escalations_IntakeRequestId",
                schema: "public",
                table: "ea_escalations",
                column: "IntakeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_escalations_NextEscalationAt",
                schema: "public",
                table: "ea_escalations",
                column: "NextEscalationAt");

            migrationBuilder.CreateIndex(
                name: "IX_ea_escalations_NextEscalationLevelId",
                schema: "public",
                table: "ea_escalations",
                column: "NextEscalationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_escalations_WorkflowInstanceId",
                schema: "public",
                table: "ea_escalations",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_actions_DueDate",
                schema: "public",
                table: "ea_meeting_actions",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_actions_MeetingId",
                schema: "public",
                table: "ea_meeting_actions",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_actions_PriorityLevelId",
                schema: "public",
                table: "ea_meeting_actions",
                column: "PriorityLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_actions_Status",
                schema: "public",
                table: "ea_meeting_actions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_agendas_IsPrepared",
                schema: "public",
                table: "ea_meeting_agendas",
                column: "IsPrepared");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_agendas_MeetingId",
                schema: "public",
                table: "ea_meeting_agendas",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_agendas_RequiredBy",
                schema: "public",
                table: "ea_meeting_agendas",
                column: "RequiredBy");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_attendees_ConfirmedAt",
                schema: "public",
                table: "ea_meeting_attendees",
                column: "ConfirmedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_attendees_MeetingId",
                schema: "public",
                table: "ea_meeting_attendees",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_decisions_DueDate",
                schema: "public",
                table: "ea_meeting_decisions",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_decisions_MeetingId",
                schema: "public",
                table: "ea_meeting_decisions",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_decisions_Status",
                schema: "public",
                table: "ea_meeting_decisions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_minutes_MeetingId",
                schema: "public",
                table: "ea_meeting_minutes",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meeting_minutes_Status",
                schema: "public",
                table: "ea_meeting_minutes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meetings_IntakeRequestId",
                schema: "public",
                table: "ea_meetings",
                column: "IntakeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meetings_MeetingDate",
                schema: "public",
                table: "ea_meetings",
                column: "MeetingDate");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meetings_OrganizerId",
                schema: "public",
                table: "ea_meetings",
                column: "OrganizerId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meetings_PriorityLevelId",
                schema: "public",
                table: "ea_meetings",
                column: "PriorityLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meetings_StartDateTime",
                schema: "public",
                table: "ea_meetings",
                column: "StartDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meetings_StatusId",
                schema: "public",
                table: "ea_meetings",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_meetings_WorkflowInstanceId",
                schema: "public",
                table: "ea_meetings",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_tat_rules_BusinessModuleId",
                schema: "public",
                table: "ea_tat_rules",
                column: "BusinessModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_tat_rules_OperationCode",
                schema: "public",
                table: "ea_tat_rules",
                column: "OperationCode");

            migrationBuilder.CreateIndex(
                name: "IX_ea_tat_rules_PriorityLevelId",
                schema: "public",
                table: "ea_tat_rules",
                column: "PriorityLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_work_assignments_AssignedAt",
                schema: "public",
                table: "ea_work_assignments",
                column: "AssignedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ea_work_assignments_AssignedToId",
                schema: "public",
                table: "ea_work_assignments",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_work_assignments_FollowupId",
                schema: "public",
                table: "ea_work_assignments",
                column: "FollowupId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_work_assignments_IntakeRequestId",
                schema: "public",
                table: "ea_work_assignments",
                column: "IntakeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_work_assignments_WorkflowInstanceId",
                schema: "public",
                table: "ea_work_assignments",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_work_pauses_EndAt",
                schema: "public",
                table: "ea_work_pauses",
                column: "EndAt");

            migrationBuilder.CreateIndex(
                name: "IX_ea_work_pauses_FollowupId",
                schema: "public",
                table: "ea_work_pauses",
                column: "FollowupId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_work_pauses_IntakeRequestId",
                schema: "public",
                table: "ea_work_pauses",
                column: "IntakeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_work_pauses_ResponseOwnerId",
                schema: "public",
                table: "ea_work_pauses",
                column: "ResponseOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ea_work_pauses_StartAt",
                schema: "public",
                table: "ea_work_pauses",
                column: "StartAt");

            migrationBuilder.CreateIndex(
                name: "UX_ea_work_pauses_WorkflowInstanceId_Open",
                schema: "public",
                table: "ea_work_pauses",
                column: "WorkflowInstanceId",
                unique: true,
                filter: "\"WorkflowInstanceId\" IS NOT NULL AND \"EndAt\" IS NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ea_work_revisions_RevisionNumber",
                schema: "public",
                table: "ea_work_revisions",
                column: "RevisionNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ea_work_revisions_WorkflowInstanceId",
                schema: "public",
                table: "ea_work_revisions",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_ea_work_revisions_WorkflowInstanceId_RevisionNumber",
                schema: "public",
                table: "ea_work_revisions",
                columns: new[] { "WorkflowInstanceId", "RevisionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_escalations_EscalationLevels_EscalationLevelId",
                schema: "public",
                table: "ea_escalations",
                column: "EscalationLevelId",
                principalTable: "EscalationLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_escalations_ea_followups_FollowupId",
                schema: "public",
                table: "ea_escalations",
                column: "FollowupId",
                principalSchema: "public",
                principalTable: "ea_followups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ea_escalations_ea_workflow_instances_WorkflowInstanceId",
                schema: "public",
                table: "ea_escalations",
                column: "WorkflowInstanceId",
                principalSchema: "public",
                principalTable: "ea_workflow_instances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ea_followups_ea_intake_requests_IntakeRequestId",
                schema: "public",
                table: "ea_followups",
                column: "IntakeRequestId",
                principalSchema: "public",
                principalTable: "ea_intake_requests",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ea_followups_ea_workflow_instances_WorkflowInstanceId",
                schema: "public",
                table: "ea_followups",
                column: "WorkflowInstanceId",
                principalSchema: "public",
                principalTable: "ea_workflow_instances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ea_intake_classifications_ea_intake_requests_IntakeRequestId",
                schema: "public",
                table: "ea_intake_classifications",
                column: "IntakeRequestId",
                principalSchema: "public",
                principalTable: "ea_intake_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_intake_requests_ea_business_modules_BusinessModuleId",
                schema: "public",
                table: "ea_intake_requests",
                column: "BusinessModuleId",
                principalSchema: "public",
                principalTable: "ea_business_modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_intake_requests_ea_priority_levels_PriorityLevelId",
                schema: "public",
                table: "ea_intake_requests",
                column: "PriorityLevelId",
                principalSchema: "public",
                principalTable: "ea_priority_levels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_intake_requests_ea_statuses_StatusId",
                schema: "public",
                table: "ea_intake_requests",
                column: "StatusId",
                principalSchema: "public",
                principalTable: "ea_statuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_workflow_history_ea_workflow_instances_WorkflowInstanceId",
                schema: "public",
                table: "ea_workflow_history",
                column: "WorkflowInstanceId",
                principalSchema: "public",
                principalTable: "ea_workflow_instances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_workflow_instances_ea_intake_requests_IntakeRequestId",
                schema: "public",
                table: "ea_workflow_instances",
                column: "IntakeRequestId",
                principalSchema: "public",
                principalTable: "ea_intake_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ea_workflow_instances_ea_statuses_StatusId",
                schema: "public",
                table: "ea_workflow_instances",
                column: "StatusId",
                principalSchema: "public",
                principalTable: "ea_statuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ea_escalations_EscalationLevels_EscalationLevelId",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_escalations_ea_followups_FollowupId",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_escalations_ea_workflow_instances_WorkflowInstanceId",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_followups_ea_intake_requests_IntakeRequestId",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_followups_ea_workflow_instances_WorkflowInstanceId",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_intake_classifications_ea_intake_requests_IntakeRequestId",
                schema: "public",
                table: "ea_intake_classifications");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_intake_requests_ea_business_modules_BusinessModuleId",
                schema: "public",
                table: "ea_intake_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_intake_requests_ea_priority_levels_PriorityLevelId",
                schema: "public",
                table: "ea_intake_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_intake_requests_ea_statuses_StatusId",
                schema: "public",
                table: "ea_intake_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_workflow_history_ea_workflow_instances_WorkflowInstanceId",
                schema: "public",
                table: "ea_workflow_history");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_workflow_instances_ea_intake_requests_IntakeRequestId",
                schema: "public",
                table: "ea_workflow_instances");

            migrationBuilder.DropForeignKey(
                name: "FK_ea_workflow_instances_ea_statuses_StatusId",
                schema: "public",
                table: "ea_workflow_instances");

            migrationBuilder.DropTable(
                name: "ea_meeting_actions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_meeting_agendas",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_meeting_attendees",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_meeting_decisions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_meeting_minutes",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_tat_rules",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_work_assignments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_work_pauses",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_work_revisions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ea_meetings",
                schema: "public");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EscalationLevels",
                table: "EscalationLevels");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ea_workflow_instances",
                schema: "public",
                table: "ea_workflow_instances");

            migrationBuilder.DropIndex(
                name: "IX_ea_workflow_instances_AssignedToId",
                schema: "public",
                table: "ea_workflow_instances");

            migrationBuilder.DropIndex(
                name: "IX_ea_workflow_instances_TatStartedAt",
                schema: "public",
                table: "ea_workflow_instances");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ea_workflow_history",
                schema: "public",
                table: "ea_workflow_history");

            migrationBuilder.DropIndex(
                name: "IX_ea_workflow_history_StageOwnerId",
                schema: "public",
                table: "ea_workflow_history");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ea_statuses",
                schema: "public",
                table: "ea_statuses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ea_priority_levels",
                schema: "public",
                table: "ea_priority_levels");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ea_notifications",
                schema: "public",
                table: "ea_notifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ea_intake_requests",
                schema: "public",
                table: "ea_intake_requests");

            migrationBuilder.DropIndex(
                name: "IX_ea_intake_requests_RequiredDate",
                schema: "public",
                table: "ea_intake_requests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ea_intake_classifications",
                schema: "public",
                table: "ea_intake_classifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ea_followups",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropIndex(
                name: "IX_ea_followups_BusinessModuleId",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ea_escalations",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropIndex(
                name: "IX_ea_escalations_IntakeRequestId",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropIndex(
                name: "IX_ea_escalations_NextEscalationAt",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropIndex(
                name: "IX_ea_escalations_NextEscalationLevelId",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropIndex(
                name: "IX_ea_escalations_WorkflowInstanceId",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ea_business_modules",
                schema: "public",
                table: "ea_business_modules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ea_audit_logs",
                schema: "public",
                table: "ea_audit_logs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ea_attachments",
                schema: "public",
                table: "ea_attachments");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                schema: "public",
                table: "ea_workflow_instances");

            migrationBuilder.DropColumn(
                name: "AssignedToId",
                schema: "public",
                table: "ea_workflow_instances");

            migrationBuilder.DropColumn(
                name: "AssignedToName",
                schema: "public",
                table: "ea_workflow_instances");

            migrationBuilder.DropColumn(
                name: "BusinessModuleId",
                schema: "public",
                table: "ea_workflow_instances");

            migrationBuilder.DropColumn(
                name: "BusinessRecordId",
                schema: "public",
                table: "ea_workflow_instances");

            migrationBuilder.DropColumn(
                name: "TatStartedAt",
                schema: "public",
                table: "ea_workflow_instances");

            migrationBuilder.DropColumn(
                name: "StageOwnerId",
                schema: "public",
                table: "ea_workflow_history");

            migrationBuilder.DropColumn(
                name: "StageOwnerName",
                schema: "public",
                table: "ea_workflow_history");

            migrationBuilder.DropColumn(
                name: "TransitionType",
                schema: "public",
                table: "ea_workflow_history");

            migrationBuilder.DropColumn(
                name: "AssignedToId",
                schema: "public",
                table: "ea_intake_requests");

            migrationBuilder.DropColumn(
                name: "AssignedToName",
                schema: "public",
                table: "ea_intake_requests");

            migrationBuilder.DropColumn(
                name: "IsConfidential",
                schema: "public",
                table: "ea_intake_requests");

            migrationBuilder.DropColumn(
                name: "RequiredDate",
                schema: "public",
                table: "ea_intake_requests");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "public",
                table: "ea_intake_requests");

            migrationBuilder.DropColumn(
                name: "SourceChannel",
                schema: "public",
                table: "ea_intake_requests");

            migrationBuilder.DropColumn(
                name: "SourceReferenceId",
                schema: "public",
                table: "ea_intake_requests");

            migrationBuilder.DropColumn(
                name: "AssignedToId",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "AssignedToName",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "BusinessModuleId",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "BusinessRecordId",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "CompletedById",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "CompletedByName",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "CompletionNote",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "ExpectedResponseAt",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "LastFollowupAt",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "NextFollowupAt",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "OutcomeCode",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "PriorityLevelId",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "ReminderAt",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "ResponseOwnerId",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "ResponseOwnerName",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "Subject",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "WaitingOnExternal",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "WaitingOnId",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "WaitingOnName",
                schema: "public",
                table: "ea_followups");

            migrationBuilder.DropColumn(
                name: "AcknowledgedAt",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "AcknowledgedById",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "AcknowledgedByName",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "AcknowledgementNote",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "BusinessModuleId",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "BusinessRecordId",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "EscalatedToId",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "EscalatedToName",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "IntakeRequestId",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "NextEscalationAt",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "NextEscalationLevelId",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "ResolutionNote",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "ResolvedById",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "ResolvedByName",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                schema: "public",
                table: "ea_escalations");

            migrationBuilder.RenameTable(
                name: "EscalationLevels",
                newName: "escalation_levels");

            migrationBuilder.RenameTable(
                name: "ea_workflow_instances",
                schema: "public",
                newName: "workflow_instances");

            migrationBuilder.RenameTable(
                name: "ea_workflow_history",
                schema: "public",
                newName: "workflow_history");

            migrationBuilder.RenameTable(
                name: "ea_statuses",
                schema: "public",
                newName: "statuses");

            migrationBuilder.RenameTable(
                name: "ea_priority_levels",
                schema: "public",
                newName: "priority_levels");

            migrationBuilder.RenameTable(
                name: "ea_notifications",
                schema: "public",
                newName: "notifications");

            migrationBuilder.RenameTable(
                name: "ea_intake_requests",
                schema: "public",
                newName: "intake_requests");

            migrationBuilder.RenameTable(
                name: "ea_intake_classifications",
                schema: "public",
                newName: "intake_classifications");

            migrationBuilder.RenameTable(
                name: "ea_followups",
                schema: "public",
                newName: "followups");

            migrationBuilder.RenameTable(
                name: "ea_escalations",
                schema: "public",
                newName: "escalations");

            migrationBuilder.RenameTable(
                name: "ea_business_modules",
                schema: "public",
                newName: "business_modules");

            migrationBuilder.RenameTable(
                name: "ea_audit_logs",
                schema: "public",
                newName: "audit_logs");

            migrationBuilder.RenameTable(
                name: "ea_attachments",
                schema: "public",
                newName: "attachments");

            migrationBuilder.RenameIndex(
                name: "IX_ea_workflow_instances_StatusId",
                table: "workflow_instances",
                newName: "IX_workflow_instances_StatusId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_workflow_instances_StartedAt",
                table: "workflow_instances",
                newName: "IX_workflow_instances_StartedAt");

            migrationBuilder.RenameIndex(
                name: "IX_ea_workflow_instances_IsActive",
                table: "workflow_instances",
                newName: "IX_workflow_instances_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_ea_workflow_instances_IntakeRequestId",
                table: "workflow_instances",
                newName: "IX_workflow_instances_IntakeRequestId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_workflow_history_WorkflowInstanceId",
                table: "workflow_history",
                newName: "IX_workflow_history_WorkflowInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_workflow_history_ToStatusId",
                table: "workflow_history",
                newName: "IX_workflow_history_ToStatusId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_workflow_history_ChangedAt",
                table: "workflow_history",
                newName: "IX_workflow_history_ChangedAt");

            migrationBuilder.RenameIndex(
                name: "IX_ea_statuses_IsActive",
                table: "statuses",
                newName: "IX_statuses_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_ea_statuses_DisplayOrder",
                table: "statuses",
                newName: "IX_statuses_DisplayOrder");

            migrationBuilder.RenameIndex(
                name: "IX_ea_priority_levels_Level",
                table: "priority_levels",
                newName: "IX_priority_levels_Level");

            migrationBuilder.RenameIndex(
                name: "IX_ea_priority_levels_IsActive",
                table: "priority_levels",
                newName: "IX_priority_levels_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_ea_notifications_RecipientId",
                table: "notifications",
                newName: "IX_notifications_RecipientId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_notifications_IsRead",
                table: "notifications",
                newName: "IX_notifications_IsRead");

            migrationBuilder.RenameIndex(
                name: "IX_ea_notifications_IsActive",
                table: "notifications",
                newName: "IX_notifications_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_ea_notifications_CreatedDate",
                table: "notifications",
                newName: "IX_notifications_CreatedDate");

            migrationBuilder.RenameIndex(
                name: "IX_ea_intake_requests_StatusId",
                table: "intake_requests",
                newName: "IX_intake_requests_StatusId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_intake_requests_PriorityLevelId",
                table: "intake_requests",
                newName: "IX_intake_requests_PriorityLevelId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_intake_requests_IsActive",
                table: "intake_requests",
                newName: "IX_intake_requests_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_ea_intake_requests_CreatedDate",
                table: "intake_requests",
                newName: "IX_intake_requests_CreatedDate");

            migrationBuilder.RenameIndex(
                name: "IX_ea_intake_requests_BusinessModuleId",
                table: "intake_requests",
                newName: "IX_intake_requests_BusinessModuleId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_intake_classifications_Name",
                table: "intake_classifications",
                newName: "IX_intake_classifications_Name");

            migrationBuilder.RenameIndex(
                name: "IX_ea_intake_classifications_IntakeRequestId",
                table: "intake_classifications",
                newName: "IX_intake_classifications_IntakeRequestId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_followups_WorkflowInstanceId",
                table: "followups",
                newName: "IX_followups_WorkflowInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_followups_IntakeRequestId",
                table: "followups",
                newName: "IX_followups_IntakeRequestId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_followups_DueAt",
                table: "followups",
                newName: "IX_followups_DueAt");

            migrationBuilder.RenameIndex(
                name: "IX_ea_escalations_FollowupId",
                table: "escalations",
                newName: "IX_escalations_FollowupId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_escalations_EscalationLevelId",
                table: "escalations",
                newName: "IX_escalations_EscalationLevelId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_business_modules_Name",
                table: "business_modules",
                newName: "IX_business_modules_Name");

            migrationBuilder.RenameIndex(
                name: "IX_ea_business_modules_IsActive",
                table: "business_modules",
                newName: "IX_business_modules_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_ea_audit_logs_Module",
                table: "audit_logs",
                newName: "IX_audit_logs_Module");

            migrationBuilder.RenameIndex(
                name: "IX_ea_audit_logs_EntityId",
                table: "audit_logs",
                newName: "IX_audit_logs_EntityId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_audit_logs_CreatedDate",
                table: "audit_logs",
                newName: "IX_audit_logs_CreatedDate");

            migrationBuilder.RenameIndex(
                name: "IX_ea_audit_logs_ActorId",
                table: "audit_logs",
                newName: "IX_audit_logs_ActorId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_audit_logs_ActionType",
                table: "audit_logs",
                newName: "IX_audit_logs_ActionType");

            migrationBuilder.RenameIndex(
                name: "IX_ea_attachments_UploadedBy",
                table: "attachments",
                newName: "IX_attachments_UploadedBy");

            migrationBuilder.RenameIndex(
                name: "IX_ea_attachments_UploadedAt",
                table: "attachments",
                newName: "IX_attachments_UploadedAt");

            migrationBuilder.RenameIndex(
                name: "IX_ea_attachments_RelatedModule",
                table: "attachments",
                newName: "IX_attachments_RelatedModule");

            migrationBuilder.RenameIndex(
                name: "IX_ea_attachments_RelatedEntityId",
                table: "attachments",
                newName: "IX_attachments_RelatedEntityId");

            migrationBuilder.RenameIndex(
                name: "IX_ea_attachments_IsActive",
                table: "attachments",
                newName: "IX_attachments_IsActive");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "escalation_levels",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "escalation_levels",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "escalation_levels",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "escalation_levels",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<long>(
                name: "IntakeRequestId",
                table: "workflow_instances",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "notifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceModule",
                table: "notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceId",
                table: "notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "notifications",
                type: "text",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "IntakeRequestId",
                table: "followups",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "FollowupId",
                table: "escalations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "audit_logs",
                type: "text",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActionType",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "attachments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_escalation_levels",
                table: "escalation_levels",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_workflow_instances",
                table: "workflow_instances",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_workflow_history",
                table: "workflow_history",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_statuses",
                table: "statuses",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_priority_levels",
                table: "priority_levels",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_notifications",
                table: "notifications",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_intake_requests",
                table: "intake_requests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_intake_classifications",
                table: "intake_classifications",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_followups",
                table: "followups",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_escalations",
                table: "escalations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_business_modules",
                table: "business_modules",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_audit_logs",
                table: "audit_logs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_attachments",
                table: "attachments",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_escalation_levels_Code",
                table: "escalation_levels",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_escalation_levels_Level",
                table: "escalation_levels",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_followups_CreatedDate",
                table: "followups",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_escalations_InitiatedAt",
                table: "escalations",
                column: "InitiatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_OccurredAt",
                table: "audit_logs",
                column: "OccurredAt");

            migrationBuilder.AddForeignKey(
                name: "FK_escalations_escalation_levels_EscalationLevelId",
                table: "escalations",
                column: "EscalationLevelId",
                principalTable: "escalation_levels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_escalations_followups_FollowupId",
                table: "escalations",
                column: "FollowupId",
                principalTable: "followups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_followups_intake_requests_IntakeRequestId",
                table: "followups",
                column: "IntakeRequestId",
                principalTable: "intake_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_followups_workflow_instances_WorkflowInstanceId",
                table: "followups",
                column: "WorkflowInstanceId",
                principalTable: "workflow_instances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_intake_classifications_intake_requests_IntakeRequestId",
                table: "intake_classifications",
                column: "IntakeRequestId",
                principalTable: "intake_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_intake_requests_business_modules_BusinessModuleId",
                table: "intake_requests",
                column: "BusinessModuleId",
                principalTable: "business_modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_intake_requests_priority_levels_PriorityLevelId",
                table: "intake_requests",
                column: "PriorityLevelId",
                principalTable: "priority_levels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_intake_requests_statuses_StatusId",
                table: "intake_requests",
                column: "StatusId",
                principalTable: "statuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_workflow_history_workflow_instances_WorkflowInstanceId",
                table: "workflow_history",
                column: "WorkflowInstanceId",
                principalTable: "workflow_instances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_workflow_instances_intake_requests_IntakeRequestId",
                table: "workflow_instances",
                column: "IntakeRequestId",
                principalTable: "intake_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_workflow_instances_statuses_StatusId",
                table: "workflow_instances",
                column: "StatusId",
                principalTable: "statuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
