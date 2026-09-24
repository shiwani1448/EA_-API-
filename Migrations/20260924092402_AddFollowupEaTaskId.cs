using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations
{
    /// <summary>The Followup's own central execution task (nullable until the backfill runs).</summary>
    [DbContext(typeof(EaFmsDbContext))]
    [Migration("20260924092402_AddFollowupEaTaskId")]
    public partial class AddFollowupEaTaskId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(name: "EaTaskId", schema: "public", table: "ea_followups", type: "bigint", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_ea_followups_EaTaskId", schema: "public", table: "ea_followups", column: "EaTaskId", unique: true);
            migrationBuilder.AddForeignKey(name: "FK_ea_followups_ea_tasks_EaTaskId", schema: "public", table: "ea_followups", column: "EaTaskId",
                principalSchema: "public", principalTable: "ea_tasks", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_ea_followups_ea_tasks_EaTaskId", schema: "public", table: "ea_followups");
            migrationBuilder.DropIndex(name: "IX_ea_followups_EaTaskId", schema: "public", table: "ea_followups");
            migrationBuilder.DropColumn(name: "EaTaskId", schema: "public", table: "ea_followups");
        }
    }
}
