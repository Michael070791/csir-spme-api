using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SpmeDbContext))]
[Migration("20260820175352_RemoveSkeletalStaffLeaveCredit")]
public partial class RemoveSkeletalStaffLeaveCredit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LeaveCreditYear",
            schema: "leave",
            table: "SkeletalStaffRequests");

        migrationBuilder.DropColumn(
            name: "LeaveCreditedAt",
            schema: "leave",
            table: "SkeletalStaffRequests");

        migrationBuilder.DropColumn(
            name: "DeductionDays",
            schema: "leave",
            table: "HolidayPeriods");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<short>(
            name: "LeaveCreditYear",
            schema: "leave",
            table: "SkeletalStaffRequests",
            type: "smallint",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LeaveCreditedAt",
            schema: "leave",
            table: "SkeletalStaffRequests",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<short>(
            name: "DeductionDays",
            schema: "leave",
            table: "HolidayPeriods",
            type: "smallint",
            nullable: false,
            defaultValue: (short)0);
    }
}
