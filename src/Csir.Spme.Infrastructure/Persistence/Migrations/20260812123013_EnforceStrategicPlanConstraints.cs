using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceStrategicPlanConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlans_InstituteId_Status_StartYear_EndYear",
                schema: "plan",
                table: "StrategicPlans",
                columns: new[] { "InstituteId", "Status", "StartYear", "EndYear" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StrategicPlans_Status",
                schema: "plan",
                table: "StrategicPlans",
                sql: "[Status] IN ('draft', 'active', 'closed', 'archived')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StrategicPlans_YearRange",
                schema: "plan",
                table: "StrategicPlans",
                sql: "[EndYear] >= [StartYear]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StrategicPlans_InstituteId_Status_StartYear_EndYear",
                schema: "plan",
                table: "StrategicPlans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StrategicPlans_Status",
                schema: "plan",
                table: "StrategicPlans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StrategicPlans_YearRange",
                schema: "plan",
                table: "StrategicPlans");
        }
    }
}
