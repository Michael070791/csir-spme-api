using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_EmployeeId_AppraisalPeriodStart_AppraisalPeriodEnd",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CompletedAt",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AddColumn<Guid>(
                name: "AppraisalCycleId",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "AppraiserSnapshotJson",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApproverSnapshotJson",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "BehavioralScore",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CoreScore",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DirectorAssessmentJson",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "DirectorUserId",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeSnapshotJson",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "FinalDocumentFileId",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HodAssessmentJson",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HodMidyearReviewJson",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "HodUserId",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstituteId",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "MidyearJson",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlanningJson",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RoutingExceptionReason",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaffSignatureAttemptsJson",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalScore",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YearEndJson",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AppraisalCycles",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlanningStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlanningEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MidyearStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MidyearEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    YearEndStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    YearEndEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReopenReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FormTemplateVersion = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FormTemplateChecksum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalCycles", x => x.Id);
                    table.CheckConstraint("CK_AppraisalCycles_Status", "[Status] IN ('draft','open','closed')");
                    table.CheckConstraint("CK_AppraisalCycles_Windows", "[StartDate] <= [PlanningStart] AND [PlanningStart] <= [PlanningEnd] AND [PlanningEnd] < [MidyearStart] AND [MidyearStart] <= [MidyearEnd] AND [MidyearEnd] < [YearEndStart] AND [YearEndStart] <= [YearEndEnd] AND [YearEndEnd] <= [EndDate]");
                });

            migrationBuilder.CreateTable(
                name: "AppraisalDirectorDecisions",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Version = table.Column<short>(type: "smallint", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DirectorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommentsOnWork = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ReturnReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecommendationsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalDirectorDecisions", x => x.Id);
                    table.CheckConstraint("CK_AppraisalDirectorDecisions_Decision", "[Decision] IN ('approved','returned')");
                    table.CheckConstraint("CK_AppraisalDirectorDecisions_Phase", "[Phase] IN ('midyear','year-end')");
                    table.ForeignKey(
                        name: "FK_AppraisalDirectorDecisions_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalSchema: "hr",
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalHodSubmissions",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Version = table.Column<short>(type: "smallint", nullable: false),
                    HodUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseToDecline = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SupervisorComments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalHodSubmissions", x => x.Id);
                    table.CheckConstraint("CK_AppraisalHodSubmissions_Phase", "[Phase] IN ('midyear','year-end')");
                    table.ForeignKey(
                        name: "FK_AppraisalHodSubmissions_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalSchema: "hr",
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalKeyCompetencies",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<short>(type: "smallint", nullable: false),
                    Competency = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalKeyCompetencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalKeyCompetencies_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalSchema: "hr",
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalMidyearCompetencyReviews",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Competency = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ProgressReview = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalMidyearCompetencyReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalMidyearCompetencyReviews_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalSchema: "hr",
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalReminderRecords",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OffsetCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StagedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalReminderRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalReminderRecords_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalSchema: "hr",
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalSignatureRecords",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Attempt = table.Column<short>(type: "smallint", nullable: false),
                    Accepted = table.Column<bool>(type: "bit", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DeclineReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EmployeeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalSignatureRecords", x => x.Id);
                    table.CheckConstraint("CK_AppraisalSignatureRecords_DeclineReason", "[Accepted] = 1 OR [DeclineReason] IS NOT NULL");
                    table.CheckConstraint("CK_AppraisalSignatureRecords_Phase", "[Phase] IN ('planning-employee','planning-hod','midyear-employee-submission','midyear-hod','midyear','year-end-employee-submission','year-end-hod','year-end')");
                    table.ForeignKey(
                        name: "FK_AppraisalSignatureRecords_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalSchema: "hr",
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalTargets",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<short>(type: "smallint", nullable: false),
                    CoreArea = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Target = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ResourcesRequired = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Timeline = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalTargets", x => x.Id);
                    table.CheckConstraint("CK_AppraisalTargets_DisplayOrder", "[DisplayOrder] > 0");
                    table.ForeignKey(
                        name: "FK_AppraisalTargets_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalSchema: "hr",
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalTrainingRecords",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Institution = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TrainingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Programme = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalTrainingRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalTrainingRecords_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalSchema: "hr",
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalCompetencyRatings",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HodSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Rating = table.Column<short>(type: "smallint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalCompetencyRatings", x => x.Id);
                    table.CheckConstraint("CK_AppraisalCompetencyRatings_Rating", "[Rating] IS NULL OR [Rating] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_AppraisalCompetencyRatings_AppraisalHodSubmissions_HodSubmissionId",
                        column: x => x.HodSubmissionId,
                        principalSchema: "hr",
                        principalTable: "AppraisalHodSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalMidyearCompetencyRemarks",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HodSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Competency = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalMidyearCompetencyRemarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalMidyearCompetencyRemarks_AppraisalHodSubmissions_HodSubmissionId",
                        column: x => x.HodSubmissionId,
                        principalSchema: "hr",
                        principalTable: "AppraisalHodSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalMidyearTargetRemarks",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HodSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalTargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalMidyearTargetRemarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalMidyearTargetRemarks_AppraisalHodSubmissions_HodSubmissionId",
                        column: x => x.HodSubmissionId,
                        principalSchema: "hr",
                        principalTable: "AppraisalHodSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppraisalMidyearTargetRemarks_AppraisalTargets_AppraisalTargetId",
                        column: x => x.AppraisalTargetId,
                        principalSchema: "hr",
                        principalTable: "AppraisalTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalMidyearTargetReviews",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalTargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgressReview = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalMidyearTargetReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalMidyearTargetReviews_AppraisalTargets_AppraisalTargetId",
                        column: x => x.AppraisalTargetId,
                        principalSchema: "hr",
                        principalTable: "AppraisalTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalMidyearTargetReviews_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalSchema: "hr",
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalTargetAmendments",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalTargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<short>(type: "smallint", nullable: false),
                    OriginalTarget = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OriginalResourcesRequired = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OriginalTimeline = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RevisedTarget = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RevisedResourcesRequired = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RevisedTimeline = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProposedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalTargetAmendments", x => x.Id);
                    table.CheckConstraint("CK_AppraisalTargetAmendments_Status", "[Status] IN ('proposed','accepted','superseded')");
                    table.ForeignKey(
                        name: "FK_AppraisalTargetAmendments_AppraisalTargets_AppraisalTargetId",
                        column: x => x.AppraisalTargetId,
                        principalSchema: "hr",
                        principalTable: "AppraisalTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalTargetAmendments_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalSchema: "hr",
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalTargetAssessments",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HodSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalTargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rating = table.Column<short>(type: "smallint", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalTargetAssessments", x => x.Id);
                    table.CheckConstraint("CK_AppraisalTargetAssessments_Rating", "[Rating] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_AppraisalTargetAssessments_AppraisalHodSubmissions_HodSubmissionId",
                        column: x => x.HodSubmissionId,
                        principalSchema: "hr",
                        principalTable: "AppraisalHodSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppraisalTargetAssessments_AppraisalTargets_AppraisalTargetId",
                        column: x => x.AppraisalTargetId,
                        principalSchema: "hr",
                        principalTable: "AppraisalTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalTargetVersions",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalTargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<short>(type: "smallint", nullable: false),
                    CoreArea = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Target = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ResourcesRequired = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Timeline = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalTargetVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalTargetVersions_AppraisalTargets_AppraisalTargetId",
                        column: x => x.AppraisalTargetId,
                        principalSchema: "hr",
                        principalTable: "AppraisalTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalYearEndResults",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalTargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkAccomplished = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    WorkCompletedPercentage = table.Column<short>(type: "smallint", nullable: false),
                    ExtentAndConstraints = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalYearEndResults", x => x.Id);
                    table.CheckConstraint("CK_AppraisalYearEndResults_Percentage", "[WorkCompletedPercentage] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_AppraisalYearEndResults_AppraisalTargets_AppraisalTargetId",
                        column: x => x.AppraisalTargetId,
                        principalSchema: "hr",
                        principalTable: "AppraisalTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalYearEndResults_PerformanceAppraisals_PerformanceAppraisalId",
                        column: x => x.PerformanceAppraisalId,
                        principalSchema: "hr",
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO [hr].[AppraisalCycles]
                    ([Id], [Name], [Year], [StartDate], [EndDate], [PlanningStart], [PlanningEnd],
                     [MidyearStart], [MidyearEnd], [YearEndStart], [YearEndEnd], [Status], [ReopenReason],
                     [FormTemplateVersion], [FormTemplateChecksum], [CreatedAt], [CreatedByUserId],
                     [UpdatedAt], [UpdatedByUserId], [InstituteId])
                SELECT NEWID(), CONCAT('Legacy appraisal evidence ', legacy.[Year]), legacy.[Year],
                       DATEFROMPARTS(legacy.[Year], 1, 1), DATEFROMPARTS(legacy.[Year], 12, 31),
                       DATEFROMPARTS(legacy.[Year], 1, 1), DATEFROMPARTS(legacy.[Year], 3, 31),
                       DATEFROMPARTS(legacy.[Year], 4, 1), DATEFROMPARTS(legacy.[Year], 8, 31),
                       DATEFROMPARTS(legacy.[Year], 9, 1), DATEFROMPARTS(legacy.[Year], 12, 31),
                       'closed', NULL, 'csir-performance-management-form-final-2026-08-18',
                       '4eb827081f3380d5a68fdafadea7b096f59b4e77518b01b6699c43c0819f645c', SYSUTCDATETIME(), NULL,
                       SYSUTCDATETIME(), NULL, legacy.[InstituteId]
                FROM
                (
                    SELECT DISTINCT employee.[InstituteId], CAST(YEAR(appraisal.[AppraisalPeriodEnd]) AS smallint) AS [Year]
                    FROM [hr].[PerformanceAppraisals] appraisal
                    INNER JOIN [hr].[Employees] employee ON employee.[Id] = appraisal.[EmployeeId]
                ) legacy
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM [hr].[AppraisalCycles] cycle
                    WHERE cycle.[InstituteId] = legacy.[InstituteId] AND cycle.[Year] = legacy.[Year]
                );

                UPDATE appraisal
                SET appraisal.[InstituteId] = employee.[InstituteId],
                    appraisal.[AppraisalCycleId] = cycle.[Id],
                    appraisal.[Status] = 'approved',
                    appraisal.[FinalDocumentFileId] = appraisal.[SourceFileId],
                    appraisal.[EmployeeSnapshotJson] = '{}',
                    appraisal.[AppraiserSnapshotJson] = '{}',
                    appraisal.[ApproverSnapshotJson] = '{}',
                    appraisal.[PlanningJson] = '{}',
                    appraisal.[MidyearJson] = '{}',
                    appraisal.[HodMidyearReviewJson] = '{}',
                    appraisal.[YearEndJson] = '{}',
                    appraisal.[HodAssessmentJson] = '{}',
                    appraisal.[StaffSignatureAttemptsJson] = '[]',
                    appraisal.[DirectorAssessmentJson] = '{}',
                    appraisal.[RoutingExceptionReason] = 'Legacy final-approved appraisal evidence migrated without editable workflow routing.',
                    appraisal.[CompletedAt] = COALESCE(appraisal.[CompletedAt], appraisal.[ApprovedAt], appraisal.[UpdatedAt], appraisal.[CreatedAt], SYSUTCDATETIME()),
                    appraisal.[ApprovedAt] = COALESCE(appraisal.[ApprovedAt], appraisal.[CompletedAt], appraisal.[UpdatedAt], appraisal.[CreatedAt], SYSUTCDATETIME())
                FROM [hr].[PerformanceAppraisals] appraisal
                INNER JOIN [hr].[Employees] employee ON employee.[Id] = appraisal.[EmployeeId]
                INNER JOIN [hr].[AppraisalCycles] cycle
                    ON cycle.[InstituteId] = employee.[InstituteId]
                    AND cycle.[Year] = CAST(YEAR(appraisal.[AppraisalPeriodEnd]) AS smallint);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_AppraisalCycleId",
                schema: "hr",
                table: "PerformanceAppraisals",
                column: "AppraisalCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_DirectorUserId_Status_AppraisalPeriodEnd",
                schema: "hr",
                table: "PerformanceAppraisals",
                columns: new[] { "DirectorUserId", "Status", "AppraisalPeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_EmployeeId_AppraisalCycleId_AppraisalPeriodStart_AppraisalPeriodEnd",
                schema: "hr",
                table: "PerformanceAppraisals",
                columns: new[] { "EmployeeId", "AppraisalCycleId", "AppraisalPeriodStart", "AppraisalPeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_EmployeeId_Status_UpdatedAt",
                schema: "hr",
                table: "PerformanceAppraisals",
                columns: new[] { "EmployeeId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_FinalDocumentFileId",
                schema: "hr",
                table: "PerformanceAppraisals",
                column: "FinalDocumentFileId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_HodUserId_Status_AppraisalPeriodEnd",
                schema: "hr",
                table: "PerformanceAppraisals",
                columns: new[] { "HodUserId", "Status", "AppraisalPeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_InstituteId_Status_UpdatedAt",
                schema: "hr",
                table: "PerformanceAppraisals",
                columns: new[] { "InstituteId", "Status", "UpdatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PerformanceAppraisals_DistinctReviewers",
                schema: "hr",
                table: "PerformanceAppraisals",
                sql: "[HodUserId] IS NULL OR [DirectorUserId] IS NULL OR [HodUserId] <> [DirectorUserId]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PerformanceAppraisals_Outcome",
                schema: "hr",
                table: "PerformanceAppraisals",
                sql: "[Outcome] IN ('', 'satisfactory', 'unsatisfactory')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PerformanceAppraisals_Period",
                schema: "hr",
                table: "PerformanceAppraisals",
                sql: "[AppraisalPeriodStart] <= [AppraisalPeriodEnd]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PerformanceAppraisals_Scores",
                schema: "hr",
                table: "PerformanceAppraisals",
                sql: "([BehavioralScore] IS NULL OR CAST([BehavioralScore] AS REAL) BETWEEN 0 AND 50) AND ([CoreScore] IS NULL OR CAST([CoreScore] AS REAL) BETWEEN 0 AND 50) AND ([TotalScore] IS NULL OR CAST([TotalScore] AS REAL) BETWEEN 0 AND 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PerformanceAppraisals_Status",
                schema: "hr",
                table: "PerformanceAppraisals",
                sql: "[Status] IN ('planning','planning-review','midyear','midyear-review','midyear-staff-signature','midyear-director-review','year-end','hod-assessment','staff-signature','director-review','approved')");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCompetencyRatings_HodSubmissionId_FactorCode",
                schema: "hr",
                table: "AppraisalCompetencyRatings",
                columns: new[] { "HodSubmissionId", "FactorCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_InstituteId_Status_Year",
                schema: "hr",
                table: "AppraisalCycles",
                columns: new[] { "InstituteId", "Status", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_InstituteId_Year",
                schema: "hr",
                table: "AppraisalCycles",
                columns: new[] { "InstituteId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalDirectorDecisions_PerformanceAppraisalId_Phase_Version",
                schema: "hr",
                table: "AppraisalDirectorDecisions",
                columns: new[] { "PerformanceAppraisalId", "Phase", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalHodSubmissions_PerformanceAppraisalId_Phase_Version",
                schema: "hr",
                table: "AppraisalHodSubmissions",
                columns: new[] { "PerformanceAppraisalId", "Phase", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalKeyCompetencies_PerformanceAppraisalId_DisplayOrder",
                schema: "hr",
                table: "AppraisalKeyCompetencies",
                columns: new[] { "PerformanceAppraisalId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalMidyearCompetencyRemarks_HodSubmissionId_Competency",
                schema: "hr",
                table: "AppraisalMidyearCompetencyRemarks",
                columns: new[] { "HodSubmissionId", "Competency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalMidyearCompetencyReviews_PerformanceAppraisalId_Competency",
                schema: "hr",
                table: "AppraisalMidyearCompetencyReviews",
                columns: new[] { "PerformanceAppraisalId", "Competency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalMidyearTargetRemarks_AppraisalTargetId",
                schema: "hr",
                table: "AppraisalMidyearTargetRemarks",
                column: "AppraisalTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalMidyearTargetRemarks_HodSubmissionId_AppraisalTargetId",
                schema: "hr",
                table: "AppraisalMidyearTargetRemarks",
                columns: new[] { "HodSubmissionId", "AppraisalTargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalMidyearTargetReviews_AppraisalTargetId",
                schema: "hr",
                table: "AppraisalMidyearTargetReviews",
                column: "AppraisalTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalMidyearTargetReviews_PerformanceAppraisalId_AppraisalTargetId",
                schema: "hr",
                table: "AppraisalMidyearTargetReviews",
                columns: new[] { "PerformanceAppraisalId", "AppraisalTargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalReminderRecords_PerformanceAppraisalId_Stage_OffsetCode",
                schema: "hr",
                table: "AppraisalReminderRecords",
                columns: new[] { "PerformanceAppraisalId", "Stage", "OffsetCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalSignatureRecords_PerformanceAppraisalId_Phase_Attempt",
                schema: "hr",
                table: "AppraisalSignatureRecords",
                columns: new[] { "PerformanceAppraisalId", "Phase", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTargetAmendments_AppraisalTargetId",
                schema: "hr",
                table: "AppraisalTargetAmendments",
                column: "AppraisalTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTargetAmendments_PerformanceAppraisalId_AppraisalTargetId_Version",
                schema: "hr",
                table: "AppraisalTargetAmendments",
                columns: new[] { "PerformanceAppraisalId", "AppraisalTargetId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTargetAssessments_AppraisalTargetId",
                schema: "hr",
                table: "AppraisalTargetAssessments",
                column: "AppraisalTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTargetAssessments_HodSubmissionId_AppraisalTargetId",
                schema: "hr",
                table: "AppraisalTargetAssessments",
                columns: new[] { "HodSubmissionId", "AppraisalTargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTargets_PerformanceAppraisalId_DisplayOrder",
                schema: "hr",
                table: "AppraisalTargets",
                columns: new[] { "PerformanceAppraisalId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTargetVersions_AppraisalTargetId_Version",
                schema: "hr",
                table: "AppraisalTargetVersions",
                columns: new[] { "AppraisalTargetId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalTrainingRecords_PerformanceAppraisalId",
                schema: "hr",
                table: "AppraisalTrainingRecords",
                column: "PerformanceAppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalYearEndResults_AppraisalTargetId",
                schema: "hr",
                table: "AppraisalYearEndResults",
                column: "AppraisalTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalYearEndResults_PerformanceAppraisalId_AppraisalTargetId",
                schema: "hr",
                table: "AppraisalYearEndResults",
                columns: new[] { "PerformanceAppraisalId", "AppraisalTargetId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceAppraisals_AppraisalCycles_AppraisalCycleId",
                schema: "hr",
                table: "PerformanceAppraisals",
                column: "AppraisalCycleId",
                principalSchema: "hr",
                principalTable: "AppraisalCycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceAppraisals_Employees_EmployeeId",
                schema: "hr",
                table: "PerformanceAppraisals",
                column: "EmployeeId",
                principalSchema: "hr",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceAppraisals_FileRecords_FinalDocumentFileId",
                schema: "hr",
                table: "PerformanceAppraisals",
                column: "FinalDocumentFileId",
                principalSchema: "ops",
                principalTable: "FileRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceAppraisals_AppraisalCycles_AppraisalCycleId",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceAppraisals_Employees_EmployeeId",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceAppraisals_FileRecords_FinalDocumentFileId",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropTable(
                name: "AppraisalCompetencyRatings",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalCycles",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalDirectorDecisions",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalKeyCompetencies",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalMidyearCompetencyRemarks",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalMidyearCompetencyReviews",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalMidyearTargetRemarks",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalMidyearTargetReviews",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalReminderRecords",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalSignatureRecords",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalTargetAmendments",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalTargetAssessments",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalTargetVersions",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalTrainingRecords",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalYearEndResults",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalHodSubmissions",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AppraisalTargets",
                schema: "hr");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_AppraisalCycleId",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_DirectorUserId_Status_AppraisalPeriodEnd",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_EmployeeId_AppraisalCycleId_AppraisalPeriodStart_AppraisalPeriodEnd",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_EmployeeId_Status_UpdatedAt",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_FinalDocumentFileId",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_HodUserId_Status_AppraisalPeriodEnd",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceAppraisals_InstituteId_Status_UpdatedAt",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PerformanceAppraisals_DistinctReviewers",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PerformanceAppraisals_Outcome",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PerformanceAppraisals_Period",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PerformanceAppraisals_Scores",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PerformanceAppraisals_Status",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "AppraisalCycleId",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "AppraiserSnapshotJson",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "ApproverSnapshotJson",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "BehavioralScore",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "CoreScore",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "DirectorAssessmentJson",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "DirectorUserId",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "EmployeeSnapshotJson",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "FinalDocumentFileId",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "HodAssessmentJson",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "HodMidyearReviewJson",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "HodUserId",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "InstituteId",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "MidyearJson",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "PlanningJson",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "RoutingExceptionReason",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "StaffSignatureAttemptsJson",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "TotalScore",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.DropColumn(
                name: "YearEndJson",
                schema: "hr",
                table: "PerformanceAppraisals");

            migrationBuilder.Sql("""
                UPDATE [hr].[PerformanceAppraisals]
                SET [CompletedAt] = COALESCE([CompletedAt], [ApprovedAt], [UpdatedAt], [CreatedAt], SYSUTCDATETIME())
                WHERE [CompletedAt] IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CompletedAt",
                schema: "hr",
                table: "PerformanceAppraisals",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_EmployeeId_AppraisalPeriodStart_AppraisalPeriodEnd",
                schema: "hr",
                table: "PerformanceAppraisals",
                columns: new[] { "EmployeeId", "AppraisalPeriodStart", "AppraisalPeriodEnd" },
                unique: true);
        }
    }
}
