using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SkeletalStaffWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SkeletalStaffApprovals",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkeletalStaffRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalStage = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Sequence = table.Column<short>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkeletalStaffApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkeletalStaffApprovals_SkeletalStaffRequests_SkeletalStaffRequestId",
                        column: x => x.SkeletalStaffRequestId,
                        principalSchema: "leave",
                        principalTable: "SkeletalStaffRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SkeletalStaffApprovals_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalSchema: "iam",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SkeletalStaffRequests_HolidayPeriodId",
                schema: "leave",
                table: "SkeletalStaffRequests",
                column: "HolidayPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_SkeletalStaffRequests_InstituteId_Status_CreatedAt",
                schema: "leave",
                table: "SkeletalStaffRequests",
                columns: new[] { "InstituteId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SkeletalStaffApprovals_ApproverUserId",
                schema: "leave",
                table: "SkeletalStaffApprovals",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SkeletalStaffApprovals_SkeletalStaffRequestId_ApprovalStage_Sequence",
                schema: "leave",
                table: "SkeletalStaffApprovals",
                columns: new[] { "SkeletalStaffRequestId", "ApprovalStage", "Sequence" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SkeletalStaffRequests_Employees_EmployeeId",
                schema: "leave",
                table: "SkeletalStaffRequests",
                column: "EmployeeId",
                principalSchema: "hr",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SkeletalStaffRequests_HolidayPeriods_HolidayPeriodId",
                schema: "leave",
                table: "SkeletalStaffRequests",
                column: "HolidayPeriodId",
                principalSchema: "leave",
                principalTable: "HolidayPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SkeletalStaffRequests_Employees_EmployeeId",
                schema: "leave",
                table: "SkeletalStaffRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_SkeletalStaffRequests_HolidayPeriods_HolidayPeriodId",
                schema: "leave",
                table: "SkeletalStaffRequests");

            migrationBuilder.DropTable(
                name: "SkeletalStaffApprovals",
                schema: "leave");

            migrationBuilder.DropIndex(
                name: "IX_SkeletalStaffRequests_HolidayPeriodId",
                schema: "leave",
                table: "SkeletalStaffRequests");

            migrationBuilder.DropIndex(
                name: "IX_SkeletalStaffRequests_InstituteId_Status_CreatedAt",
                schema: "leave",
                table: "SkeletalStaffRequests");
        }
    }
}
