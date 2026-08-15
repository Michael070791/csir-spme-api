using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeEmploymentLeadershipAndSpecialization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AreaOfSpecialization",
                schema: "hr",
                table: "EmploymentRecords",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeadershipRoles",
                schema: "hr",
                table: "EmploymentRecords",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AreaOfSpecialization",
                schema: "hr",
                table: "EmploymentRecords");

            migrationBuilder.DropColumn(
                name: "LeadershipRoles",
                schema: "hr",
                table: "EmploymentRecords");
        }
    }
}
