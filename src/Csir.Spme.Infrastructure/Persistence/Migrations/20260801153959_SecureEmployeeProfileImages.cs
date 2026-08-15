using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SpmeDbContext))]
[Migration("20260801153959_SecureEmployeeProfileImages")]
public partial class SecureEmployeeProfileImages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAt",
            schema: "ops",
            table: "FileRecords",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "StorageDeletedAt",
            schema: "ops",
            table: "FileRecords",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_FileRecords_IsDeleted_StorageDeletedAt",
            schema: "ops",
            table: "FileRecords",
            columns: new[] { "IsDeleted", "StorageDeletedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FileRecords_IsDeleted_StorageDeletedAt",
            schema: "ops",
            table: "FileRecords");

        migrationBuilder.DropColumn(
            name: "DeletedAt",
            schema: "ops",
            table: "FileRecords");

        migrationBuilder.DropColumn(
            name: "StorageDeletedAt",
            schema: "ops",
            table: "FileRecords");
    }
}
