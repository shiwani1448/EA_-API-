using System;
using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <summary>Record of each reminder handoff (Email/WhatsApp) the EA opened; the backend sends nothing.</summary>
    [DbContext(typeof(EaFmsDbContext))]
    [Migration("20260924092404_AddFollowupReminderLog")]
    public partial class AddFollowupReminderLog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ea_followup_reminder_log",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FollowupId = table.Column<long>(type: "bigint", nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SentByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ea_followup_reminder_log", x => x.Id);
                    table.ForeignKey(name: "FK_ea_followup_reminder_log_ea_followups_FollowupId", column: x => x.FollowupId,
                        principalSchema: "public", principalTable: "ea_followups", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(name: "IX_ea_followup_reminder_log_FollowupId", schema: "public", table: "ea_followup_reminder_log", column: "FollowupId");
            migrationBuilder.CreateIndex(name: "IX_ea_followup_reminder_log_SentAt", schema: "public", table: "ea_followup_reminder_log", column: "SentAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "ea_followup_reminder_log", schema: "public");
    }
}
