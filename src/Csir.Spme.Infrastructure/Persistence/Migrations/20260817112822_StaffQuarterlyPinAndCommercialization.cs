using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffQuarterlyPinAndCommercialization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SnapshotCommercialization",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotPin",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pin",
                schema: "projects",
                table: "Projects",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PinAssignedAt",
                schema: "projects",
                table: "Projects",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Commercialization",
                schema: "projects",
                table: "ProjectInceptions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_InstituteId_Pin",
                schema: "projects",
                table: "Projects",
                columns: new[] { "InstituteId", "Pin" },
                unique: true,
                filter: "[Pin] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_InstituteId_Pin",
                schema: "projects",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SnapshotCommercialization",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "SnapshotPin",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "Pin",
                schema: "projects",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PinAssignedAt",
                schema: "projects",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Commercialization",
                schema: "projects",
                table: "ProjectInceptions");
        }
    }
}
