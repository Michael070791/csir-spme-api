using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SpmeDbContext))]
[Migration("20260813153000_StaffQuarterlyHodNotificationAttachments")]
public partial class StaffQuarterlyHodNotificationAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AttachmentsJson",
            schema: "comms",
            table: "CommunicationOutboxMessages",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AttachmentsJson",
            schema: "comms",
            table: "CommunicationOutboxMessages");
    }
}
