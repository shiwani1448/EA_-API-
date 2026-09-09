using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Jarvis5.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "scih_request_no_seq");

            migrationBuilder.CreateTable(
                name: "SCIH_Request",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestNo = table.Column<string>(type: "varchar(30)", nullable: false),
                    Title = table.Column<string>(type: "varchar(300)", nullable: false),
                    DepartmentId = table.Column<string>(type: "varchar(300)", nullable: false),
                    RaisedBy = table.Column<string>(type: "varchar(300)", nullable: false),
                    RaisedAt = table.Column<DateTime>(type: "timestamp", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", nullable: false),
                    CurrentStage = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<string>(type: "varchar(20)", nullable: false),
                    OverallProgress = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    ExpectedBenefit = table.Column<string>(type: "text", nullable: false),
                    ParentRequestId = table.Column<long>(type: "bigint", nullable: true),
                    PainPointsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    AttachmentsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    MetaJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCIH_Request", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SCIH_RequestHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    StageName = table.Column<string>(type: "varchar(100)", nullable: false),
                    Action = table.Column<string>(type: "varchar(100)", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PreviousValue = table.Column<string>(type: "jsonb", nullable: true),
                    NewValue = table.Column<string>(type: "jsonb", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    ActionBy = table.Column<long>(type: "bigint", nullable: false),
                    ActionDate = table.Column<DateTime>(type: "timestamp", nullable: false),
                    IPAddress = table.Column<string>(type: "varchar(100)", nullable: true),
                    DeviceInfo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCIH_RequestHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SCIH_RequestHistory_SCIH_Request_RequestId",
                        column: x => x.RequestId,
                        principalTable: "SCIH_Request",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Request_DepartmentId",
                table: "SCIH_Request",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Request_ParentRequestId",
                table: "SCIH_Request",
                column: "ParentRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Request_Priority",
                table: "SCIH_Request",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Request_RaisedAt",
                table: "SCIH_Request",
                column: "RaisedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Request_RaisedBy",
                table: "SCIH_Request",
                column: "RaisedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Request_RequestNo",
                table: "SCIH_Request",
                column: "RequestNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_Request_Status",
                table: "SCIH_Request",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_RequestHistory_ActionDate",
                table: "SCIH_RequestHistory",
                column: "ActionDate");

            migrationBuilder.CreateIndex(
                name: "IX_SCIH_RequestHistory_RequestId",
                table: "SCIH_RequestHistory",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SCIH_RequestHistory");

            migrationBuilder.DropTable(
                name: "SCIH_Request");

            migrationBuilder.DropSequence(
                name: "scih_request_no_seq");
        }
    }
}
