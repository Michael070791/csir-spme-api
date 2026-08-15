using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SpmeDbContext))]
[Migration("20260801120000_SettingsSelfService")]
public partial class SettingsSelfService : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DisplayName", schema: "iam", table: "Users", type: "nvarchar(256)", maxLength: 256, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "PendingEmail", schema: "iam", table: "Users", type: "nvarchar(320)", maxLength: 320, nullable: true);
        migrationBuilder.Sql("UPDATE [iam].[Users] SET [DisplayName] = [UserName] WHERE [DisplayName] IS NULL;");
        migrationBuilder.AlterColumn<string>(
            name: "DisplayName", schema: "iam", table: "Users", type: "nvarchar(256)", maxLength: 256, nullable: false,
            oldClrType: typeof(string), oldType: "nvarchar(256)", oldMaxLength: 256, oldNullable: true);
        migrationBuilder.CreateIndex(name: "IX_Users_PendingEmail", schema: "iam", table: "Users", column: "PendingEmail", unique: true, filter: "[PendingEmail] IS NOT NULL");
        migrationBuilder.CreateTable(
            name: "NotificationPreferences", schema: "iam",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmailAlerts = table.Column<bool>(type: "bit", nullable: false),
                LeaveReminders = table.Column<bool>(type: "bit", nullable: false),
                PromotionUpdates = table.Column<bool>(type: "bit", nullable: false),
                SystemAnnouncements = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotificationPreferences", x => x.UserId);
                table.ForeignKey("FK_NotificationPreferences_Users_UserId", x => x.UserId, principalSchema: "iam", principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "NotificationPreferences", schema: "iam");
        migrationBuilder.DropIndex(name: "IX_Users_PendingEmail", schema: "iam", table: "Users");
        migrationBuilder.DropColumn(name: "DisplayName", schema: "iam", table: "Users");
        migrationBuilder.DropColumn(name: "PendingEmail", schema: "iam", table: "Users");
    }
}
