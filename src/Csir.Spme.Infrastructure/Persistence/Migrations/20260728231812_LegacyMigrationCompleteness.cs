using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LegacyMigrationCompleteness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReconciliationJson",
                schema: "ops",
                table: "LegacyImportRuns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CompassionateLeaveTypes",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Days = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    DoesNotDeductFromBalance = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompassionateLeaveTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveHandovers",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DelegateEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveHandovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveHandovers_LeaveRequests_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalSchema: "leave",
                        principalTable: "LeaveRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequestApprovals",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalStage = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SignatureName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Sequence = table.Column<short>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequestApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveRequestApprovals_LeaveRequests_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalSchema: "leave",
                        principalTable: "LeaveRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequestApprovals_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalSchema: "iam",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveResumptions",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResumptionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EmployeeSignatureName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveResumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveResumptions_LeaveRequests_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalSchema: "leave",
                        principalTable: "LeaveRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSponsors",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactDetails = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CommittedAmount = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSponsors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSponsors_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "projects",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportMetrics",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetricCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NumericValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    TextValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportMetrics_Reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "reporting",
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveResumptionApprovals",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResumptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalStage = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SignatureName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Sequence = table.Column<short>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveResumptionApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveResumptionApprovals_LeaveResumptions_ResumptionId",
                        column: x => x.ResumptionId,
                        principalSchema: "leave",
                        principalTable: "LeaveResumptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveResumptionApprovals_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalSchema: "iam",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompassionateLeaveTypes_ScopeType_InstituteId_Code",
                schema: "leave",
                table: "CompassionateLeaveTypes",
                columns: new[] { "ScopeType", "InstituteId", "Code" },
                unique: true,
                filter: "[InstituteId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveHandovers_LeaveRequestId",
                schema: "leave",
                table: "LeaveHandovers",
                column: "LeaveRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestApprovals_ApproverUserId",
                schema: "leave",
                table: "LeaveRequestApprovals",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestApprovals_LeaveRequestId_ApprovalStage_Sequence",
                schema: "leave",
                table: "LeaveRequestApprovals",
                columns: new[] { "LeaveRequestId", "ApprovalStage", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveResumptionApprovals_ApproverUserId",
                schema: "leave",
                table: "LeaveResumptionApprovals",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveResumptionApprovals_ResumptionId_ApprovalStage_Sequence",
                schema: "leave",
                table: "LeaveResumptionApprovals",
                columns: new[] { "ResumptionId", "ApprovalStage", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveResumptions_LeaveRequestId",
                schema: "leave",
                table: "LeaveResumptions",
                column: "LeaveRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSponsors_ProjectId_Name",
                schema: "projects",
                table: "ProjectSponsors",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportMetrics_ReportId_MetricCode",
                schema: "reporting",
                table: "ReportMetrics",
                columns: new[] { "ReportId", "MetricCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompassionateLeaveTypes",
                schema: "leave");

            migrationBuilder.DropTable(
                name: "LeaveHandovers",
                schema: "leave");

            migrationBuilder.DropTable(
                name: "LeaveRequestApprovals",
                schema: "leave");

            migrationBuilder.DropTable(
                name: "LeaveResumptionApprovals",
                schema: "leave");

            migrationBuilder.DropTable(
                name: "ProjectSponsors",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "ReportMetrics",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "LeaveResumptions",
                schema: "leave");

            migrationBuilder.DropColumn(
                name: "ReconciliationJson",
                schema: "ops",
                table: "LegacyImportRuns");
        }
    }
}
