using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations;

/// <summary>
/// Allows approval history created from an authenticated external identity to omit the local
/// Identity user foreign key. Locally managed users continue to be linked when they exist.
/// </summary>
[DbContext(typeof(SpmeDbContext))]
[Migration("20260810000000_AllowExternalSkeletalStaffApprovers")]
public partial class AllowExternalSkeletalStaffApprovers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "ApproverUserId",
            schema: "leave",
            table: "SkeletalStaffApprovals",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS
            (
                SELECT 1
                FROM [leave].[SkeletalStaffApprovals]
                WHERE [ApproverUserId] IS NULL
            )
                THROW 51000, 'Cannot make skeletal-staff approver identity required while external approval rows exist.', 1;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "ApproverUserId",
            schema: "leave",
            table: "SkeletalStaffApprovals",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);
    }
}
