using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffQuarterlyFormOneTwo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Challenges",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConferencePapersProduced",
                schema: "reporting",
                table: "ReportProjects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IpTechnologiesProtected",
                schema: "reporting",
                table: "ReportProjects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NextQuarterActivities",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgressKeyResults",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgressSummary",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotCollaboratingInstitute",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotContributionToKnowledge",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotEstimatedDuration",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotExpectedBeneficiaries",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReportProjects_ConferencePapersProduced",
                schema: "reporting",
                table: "ReportProjects",
                sql: "[ConferencePapersProduced] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReportProjects_IpTechnologiesProtected",
                schema: "reporting",
                table: "ReportProjects",
                sql: "[IpTechnologiesProtected] >= 0");

            migrationBuilder.AddColumn<string>(
                name: "SnapshotJustification",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotLeadName",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotLocation",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotMethod",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotObjective",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotParticipatingScientists",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotPotentialTechnology",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotSponsorName",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WayForward",
                schema: "reporting",
                table: "ReportProjects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Method",
                schema: "projects",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectInceptions",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstimatedDuration = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SponsorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CollaboratingInstitute = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ParticipatingScientists = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpectedBeneficiaries = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PotentialTechnology = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContributionToKnowledge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConceptNoteFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InceptionCompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectInceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectInceptions_FileRecords_ConceptNoteFileId",
                        column: x => x.ConceptNoteFileId,
                        principalSchema: "ops",
                        principalTable: "FileRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectInceptions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "projects",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportAttachments",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachmentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportAttachments_FileRecords_FileId",
                        column: x => x.FileId,
                        principalSchema: "ops",
                        principalTable: "FileRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportAttachments_Reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "reporting",
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffQuarterlyReportUploadSessions",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadKind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_StaffQuarterlyReportUploadSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInceptions_ConceptNoteFileId",
                schema: "projects",
                table: "ProjectInceptions",
                column: "ConceptNoteFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInceptions_ProjectId",
                schema: "projects",
                table: "ProjectInceptions",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportAttachments_FileId",
                schema: "reporting",
                table: "ReportAttachments",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportAttachments_ReportId_FileId",
                schema: "reporting",
                table: "ReportAttachments",
                columns: new[] { "ReportId", "FileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffQuarterlyReportUploadSessions_ProjectId_UploadKind_Status",
                schema: "reporting",
                table: "StaffQuarterlyReportUploadSessions",
                columns: new[] { "ProjectId", "UploadKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffQuarterlyReportUploadSessions_ReportId_Status",
                schema: "reporting",
                table: "StaffQuarterlyReportUploadSessions",
                columns: new[] { "ReportId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffQuarterlyReportUploadSessions_StorageKey",
                schema: "reporting",
                table: "StaffQuarterlyReportUploadSessions",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ReportProjects_ConferencePapersProduced",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReportProjects_IpTechnologiesProtected",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropTable(
                name: "ProjectInceptions",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "ReportAttachments",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "StaffQuarterlyReportUploadSessions",
                schema: "reporting");

            migrationBuilder.DropColumn(
                name: "Challenges",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "ConferencePapersProduced",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "IpTechnologiesProtected",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "NextQuarterActivities",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "ProgressKeyResults",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "ProgressSummary",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "SnapshotCollaboratingInstitute",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "SnapshotContributionToKnowledge",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "SnapshotEstimatedDuration",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "SnapshotExpectedBeneficiaries",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "SnapshotJustification",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "SnapshotLeadName",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "SnapshotLocation",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "SnapshotMethod",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "SnapshotObjective",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "SnapshotParticipatingScientists",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "SnapshotPotentialTechnology",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "SnapshotSponsorName",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "WayForward",
                schema: "reporting",
                table: "ReportProjects");

            migrationBuilder.DropColumn(
                name: "Method",
                schema: "projects",
                table: "Projects");
        }
    }
}
