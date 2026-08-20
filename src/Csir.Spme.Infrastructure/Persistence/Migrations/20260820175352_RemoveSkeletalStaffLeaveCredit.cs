using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSkeletalStaffLeaveCredit : Migration
    {
        /// <inheritdoc />
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

        /// <inheritdoc />
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
}
