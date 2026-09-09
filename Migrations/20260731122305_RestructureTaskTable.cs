using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jarvis5.Migrations
{
    /// <inheritdoc />
    public partial class RestructureTaskTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SCIH_Task_SCIH_Request_RequestId",
                table: "SCIH_Task");

            migrationBuilder.DropForeignKey(
                name: "FK_SCIH_TaskHistory_SCIH_Task_TaskId",
                table: "SCIH_TaskHistory");

            // Renamed columns that also change type keep their existing values via
            // RENAME + explicit USING conversion below, instead of DROP+ADD (which
            // would silently discard every existing row's data, e.g. losing which
            // modules were already soft-deleted).
            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "SCIH_Task",
                newName: "CreationDate");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "SCIH_Task",
                newName: "IsDelete");

            migrationBuilder.RenameColumn(
                name: "ModifiedDate",
                table: "SCIH_Task",
                newName: "UpdationDate");

            migrationBuilder.RenameColumn(
                name: "ProjectLeadId",
                table: "SCIH_Task",
                newName: "DoerLead");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "SCIH_Task",
                newName: "CurrentStatus");

            migrationBuilder.RenameColumn(
                name: "StageDetailsJson",
                table: "SCIH_Task",
                newName: "StageDetails");

            migrationBuilder.RenameColumn(
                name: "ModuleName",
                table: "SCIH_Task",
                newName: "Module");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                table: "SCIH_Task",
                newName: "UpdatedBy");

            migrationBuilder.RenameIndex(
                name: "IX_SCIH_Task_Status",
                table: "SCIH_Task",
                newName: "IX_SCIH_Task_CurrentStatus");

            migrationBuilder.AlterColumn<string>(
                name: "RequestId",
                table: "SCIH_Task",
                type: "varchar(100)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            // Plain ALTER COLUMN ... TYPE varchar on a timestamptz would format using
            // the session's local timezone/locale (e.g. "2026-08-01 03:00:00-07"), not
            // the "...T...Z" UTC ISO-8601 format the app expects — same trap as the
            // earlier timestamp<->timestamptz migration. Convert to UTC first, then format.
            // to_char(NULL, ...) is NULL, so this is also safe for the nullable UpdationDate.
            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"OverallStartDate\" TYPE varchar(50) " +
                "USING to_char(\"OverallStartDate\" AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"');");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"OverallEndDate\" TYPE varchar(50) " +
                "USING to_char(\"OverallEndDate\" AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"');");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"CreationDate\" TYPE varchar(50) " +
                "USING to_char(\"CreationDate\" AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"');");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"UpdationDate\" TYPE varchar(50) " +
                "USING to_char(\"UpdationDate\" AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"');");

            // boolean::text yields exactly 'true'/'false', matching the app's convention.
            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"IsDelete\" TYPE varchar(10) USING \"IsDelete\"::text, " +
                "ALTER COLUMN \"IsDelete\" SET DEFAULT 'false';");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"DoerLead\" TYPE varchar(100) USING \"DoerLead\"::text;");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "SCIH_Task",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            // Genuinely new columns — no prior data to preserve.
            migrationBuilder.AddColumn<string>(
                name: "ApprovedId",
                table: "SCIH_Task",
                type: "varchar(100)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CurrentStage",
                table: "SCIH_Task",
                type: "varchar(100)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IsDeletedBy",
                table: "SCIH_Task",
                type: "varchar(100)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SolutionId",
                table: "SCIH_Task",
                type: "varchar(100)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedId",
                table: "SCIH_Task");

            migrationBuilder.DropColumn(
                name: "CurrentStage",
                table: "SCIH_Task");

            migrationBuilder.DropColumn(
                name: "IsDeletedBy",
                table: "SCIH_Task");

            migrationBuilder.DropColumn(
                name: "SolutionId",
                table: "SCIH_Task");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "SCIH_Task",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "StageDetails",
                table: "SCIH_Task",
                newName: "StageDetailsJson");

            migrationBuilder.RenameColumn(
                name: "Module",
                table: "SCIH_Task",
                newName: "ModuleName");

            migrationBuilder.RenameColumn(
                name: "CurrentStatus",
                table: "SCIH_Task",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "CreationDate",
                table: "SCIH_Task",
                newName: "CreatedDate");

            migrationBuilder.RenameColumn(
                name: "IsDelete",
                table: "SCIH_Task",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "UpdationDate",
                table: "SCIH_Task",
                newName: "ModifiedDate");

            migrationBuilder.RenameColumn(
                name: "DoerLead",
                table: "SCIH_Task",
                newName: "ProjectLeadId");

            migrationBuilder.RenameIndex(
                name: "IX_SCIH_Task_CurrentStatus",
                table: "SCIH_Task",
                newName: "IX_SCIH_Task_Status");

            // text -> bigint/timestamptz/boolean have no implicit cast; explicit casts
            // correctly parse what Up() wrote (plain numeral text, ISO-8601 "...Z", "true"/"false").
            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"RequestId\" TYPE bigint USING \"RequestId\"::bigint;");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"OverallStartDate\" TYPE timestamptz USING \"OverallStartDate\"::timestamptz;");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"OverallEndDate\" TYPE timestamptz USING \"OverallEndDate\"::timestamptz;");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"CreatedDate\" TYPE timestamptz USING \"CreatedDate\"::timestamptz;");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"ModifiedDate\" TYPE timestamptz USING \"ModifiedDate\"::timestamptz;");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"IsDeleted\" TYPE boolean USING \"IsDeleted\"::boolean, " +
                "ALTER COLUMN \"IsDeleted\" SET DEFAULT false;");

            migrationBuilder.Sql(
                "ALTER TABLE \"SCIH_Task\" ALTER COLUMN \"ProjectLeadId\" TYPE bigint USING \"ProjectLeadId\"::bigint;");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "SCIH_Task",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddForeignKey(
                name: "FK_SCIH_Task_SCIH_Request_RequestId",
                table: "SCIH_Task",
                column: "RequestId",
                principalTable: "SCIH_Request",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SCIH_TaskHistory_SCIH_Task_TaskId",
                table: "SCIH_TaskHistory",
                column: "TaskId",
                principalTable: "SCIH_Task",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
