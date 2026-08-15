using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompletePromotionSelfService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_InstituteId_ReportingPeriodId_ReportType",
                schema: "reporting",
                table: "Reports");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "reporting",
                table: "Reports",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerEmployeeId",
                schema: "reporting",
                table: "Reports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportScope",
                schema: "reporting",
                table: "Reports",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "institute");

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerEmployeeId",
                schema: "reporting",
                table: "Reports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerUserId",
                schema: "reporting",
                table: "Reports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScanStatus",
                schema: "ops",
                table: "FileRecords",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "pending");

            migrationBuilder.AddColumn<string>(
                name: "TextBody",
                schema: "comms",
                table: "CommunicationOutboxMessages",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentUploadSessions",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequirementSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DeclaredSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DeclaredSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentUploadSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentUploadSessions_FileRecords_FileId",
                        column: x => x.FileId,
                        principalSchema: "ops",
                        principalTable: "FileRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentUploadSessions_SubmissionRequirementSnapshots_RequirementSnapshotId",
                        column: x => x.RequirementSnapshotId,
                        principalSchema: "promotions",
                        principalTable: "SubmissionRequirementSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentUploadSessions_Submissions_PromotionSubmissionId",
                        column: x => x.PromotionSubmissionId,
                        principalSchema: "promotions",
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportProjects",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectCodeSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProjectNameSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportProjects_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "projects",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportProjects_Reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "reporting",
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportTechnologies",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnologyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnologyCodeSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TechnologyNameSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportTechnologies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportTechnologies_Reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "reporting",
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportTechnologies_Technologies_TechnologyId",
                        column: x => x.TechnologyId,
                        principalSchema: "knowledge",
                        principalTable: "Technologies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VerificationChallenges",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DestinationHash = table.Column<string>(type: "char(64)", nullable: false),
                    CodeHash = table.Column<string>(type: "char(64)", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AttemptCount = table.Column<short>(type: "smallint", nullable: false),
                    ResendCount = table.Column<short>(type: "smallint", nullable: false),
                    LastSentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificationChallenges_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetRequests",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerificationChallengeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SupersededAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetRequests_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PasswordResetRequests_VerificationChallenges_VerificationChallengeId",
                        column: x => x.VerificationChallengeId,
                        principalSchema: "iam",
                        principalTable: "VerificationChallenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_PromotionAssessmentId",
                schema: "promotions",
                table: "Submissions",
                column: "PromotionAssessmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_InstituteId_ReportingPeriodId_ReportType",
                schema: "reporting",
                table: "Reports",
                columns: new[] { "InstituteId", "ReportingPeriodId", "ReportType" },
                unique: true,
                filter: "[ReportScope] = 'institute'");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_OwnerEmployeeId_ReportingPeriodId_ReportType",
                schema: "reporting",
                table: "Reports",
                columns: new[] { "OwnerEmployeeId", "ReportingPeriodId", "ReportType" },
                unique: true,
                filter: "[ReportScope] = 'employee-quarterly'");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReviewerEmployeeId",
                schema: "reporting",
                table: "Reports",
                column: "ReviewerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReviewerUserId_Status_SubmittedAt",
                schema: "reporting",
                table: "Reports",
                columns: new[] { "ReviewerUserId", "Status", "SubmittedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reports_EmployeeQuarterlyOwnership",
                schema: "reporting",
                table: "Reports",
                sql: "([ReportScope] = 'institute' AND [OwnerEmployeeId] IS NULL AND [ReviewerEmployeeId] IS NULL AND [ReviewerUserId] IS NULL) OR ([ReportScope] = 'employee-quarterly' AND [OwnerEmployeeId] IS NOT NULL AND [ReviewerEmployeeId] IS NOT NULL AND [ReviewerUserId] IS NOT NULL AND [ReportType] = 'staff-quarterly')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reports_ReportScope",
                schema: "reporting",
                table: "Reports",
                sql: "[ReportScope] IN ('institute', 'employee-quarterly')");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentUploadSessions_FileId",
                schema: "promotions",
                table: "DocumentUploadSessions",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentUploadSessions_PromotionSubmissionId_RequirementSnapshotId_Status",
                schema: "promotions",
                table: "DocumentUploadSessions",
                columns: new[] { "PromotionSubmissionId", "RequirementSnapshotId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentUploadSessions_RequirementSnapshotId",
                schema: "promotions",
                table: "DocumentUploadSessions",
                column: "RequirementSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentUploadSessions_StorageKey",
                schema: "promotions",
                table: "DocumentUploadSessions",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_UserId",
                schema: "iam",
                table: "PasswordResetRequests",
                column: "UserId",
                unique: true,
                filter: "[CompletedAt] IS NULL AND [SupersededAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_UserId_CompletedAt_SupersededAt",
                schema: "iam",
                table: "PasswordResetRequests",
                columns: new[] { "UserId", "CompletedAt", "SupersededAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_VerificationChallengeId",
                schema: "iam",
                table: "PasswordResetRequests",
                column: "VerificationChallengeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportProjects_ProjectId",
                schema: "reporting",
                table: "ReportProjects",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportProjects_ReportId_ProjectId",
                schema: "reporting",
                table: "ReportProjects",
                columns: new[] { "ReportId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportTechnologies_ReportId_TechnologyId",
                schema: "reporting",
                table: "ReportTechnologies",
                columns: new[] { "ReportId", "TechnologyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportTechnologies_TechnologyId",
                schema: "reporting",
                table: "ReportTechnologies",
                column: "TechnologyId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationChallenges_UserId_Purpose_Channel_ConsumedAt",
                schema: "iam",
                table: "VerificationChallenges",
                columns: new[] { "UserId", "Purpose", "Channel", "ConsumedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VerificationChallenges_UserId_Purpose_ExpiresAt",
                schema: "iam",
                table: "VerificationChallenges",
                columns: new[] { "UserId", "Purpose", "ExpiresAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Employees_OwnerEmployeeId",
                schema: "reporting",
                table: "Reports",
                column: "OwnerEmployeeId",
                principalSchema: "hr",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Employees_ReviewerEmployeeId",
                schema: "reporting",
                table: "Reports",
                column: "ReviewerEmployeeId",
                principalSchema: "hr",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Users_ReviewerUserId",
                schema: "reporting",
                table: "Reports",
                column: "ReviewerUserId",
                principalSchema: "iam",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Employees_OwnerEmployeeId",
                schema: "reporting",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Employees_ReviewerEmployeeId",
                schema: "reporting",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Users_ReviewerUserId",
                schema: "reporting",
                table: "Reports");

            migrationBuilder.DropTable(
                name: "DocumentUploadSessions",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "PasswordResetRequests",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "ReportProjects",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "ReportTechnologies",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "VerificationChallenges",
                schema: "iam");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_PromotionAssessmentId",
                schema: "promotions",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Reports_InstituteId_ReportingPeriodId_ReportType",
                schema: "reporting",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_OwnerEmployeeId_ReportingPeriodId_ReportType",
                schema: "reporting",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_ReviewerEmployeeId",
                schema: "reporting",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_ReviewerUserId_Status_SubmittedAt",
                schema: "reporting",
                table: "Reports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reports_EmployeeQuarterlyOwnership",
                schema: "reporting",
                table: "Reports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reports_ReportScope",
                schema: "reporting",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "OwnerEmployeeId",
                schema: "reporting",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ReportScope",
                schema: "reporting",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ReviewerEmployeeId",
                schema: "reporting",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ReviewerUserId",
                schema: "reporting",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ScanStatus",
                schema: "ops",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "TextBody",
                schema: "comms",
                table: "CommunicationOutboxMessages");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "reporting",
                table: "Reports",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_InstituteId_ReportingPeriodId_ReportType",
                schema: "reporting",
                table: "Reports",
                columns: new[] { "InstituteId", "ReportingPeriodId", "ReportType" },
                unique: true);
        }
    }
}
