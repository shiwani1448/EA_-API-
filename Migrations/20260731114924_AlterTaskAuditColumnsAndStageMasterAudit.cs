using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jarvis5.Migrations
{
    /// <inheritdoc />
    public partial class AlterTaskAuditColumnsAndStageMasterAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "SCIH_Task",
                type: "varchar(100)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "SCIH_Task",
                type: "varchar(100)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "SCIH_StageMaster",
                type: "varchar(100)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "SCIH_StageMaster",
                type: "timestamp",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SCIH_StageMaster",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "SCIH_StageMaster",
                type: "varchar(100)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                table: "SCIH_StageMaster",
                type: "timestamp",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 3L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 4L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 5L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 6L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 7L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 8L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 9L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 10L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 11L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 12L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 13L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 14L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });

            migrationBuilder.UpdateData(
                table: "SCIH_StageMaster",
                keyColumn: "Id",
                keyValue: 15L,
                columns: new[] { "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "System", new DateTime(2026, 7, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "SCIH_StageMaster");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "SCIH_StageMaster");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SCIH_StageMaster");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "SCIH_StageMaster");

            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                table: "SCIH_StageMaster");

            migrationBuilder.AlterColumn<long>(
                name: "ModifiedBy",
                table: "SCIH_Task",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CreatedBy",
                table: "SCIH_Task",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)");
        }
    }
}
