using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Studio5JarvisMasterApi.Migrations;

public partial class MeetingDelegationDecision : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "DelegationDecision", schema: "public", table: "ea_meetings", type: "text", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "DelegationDecidedAt", schema: "public", table: "ea_meetings", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "DelegationDecidedBy", schema: "public", table: "ea_meetings", type: "text", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DelegationDecision", schema: "public", table: "ea_meetings");
        migrationBuilder.DropColumn(name: "DelegationDecidedAt", schema: "public", table: "ea_meetings");
        migrationBuilder.DropColumn(name: "DelegationDecidedBy", schema: "public", table: "ea_meetings");
    }
}
