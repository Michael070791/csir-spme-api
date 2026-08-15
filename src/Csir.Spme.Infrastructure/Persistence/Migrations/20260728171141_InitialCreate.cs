using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "promotions");

            migrationBuilder.EnsureSchema(
                name: "ops");

            migrationBuilder.EnsureSchema(
                name: "org");

            migrationBuilder.EnsureSchema(
                name: "hr");

            migrationBuilder.EnsureSchema(
                name: "comms");

            migrationBuilder.EnsureSchema(
                name: "leave");

            migrationBuilder.EnsureSchema(
                name: "plan");

            migrationBuilder.EnsureSchema(
                name: "iam");

            migrationBuilder.EnsureSchema(
                name: "projects");

            migrationBuilder.EnsureSchema(
                name: "knowledge");

            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.CreateTable(
                name: "AppraisalAssessments",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SatisfactoryRequirementMet = table.Column<bool>(type: "bit", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalAssessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Assessments",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionPathId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceEmploymentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssessmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectivePromotionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceGradeEffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ServiceRequirementMetOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedSourceGradeYears = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    EligibilityState = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BlockingReasonsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PendingHrChecksJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EligibilitySnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssessedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditRecords",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorScope = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ClientIp = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true),
                    BeforeSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cycles",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleYear = table.Column<short>(type: "smallint", nullable: false),
                    EffectivePromotionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Decisions",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    InternalDecisionNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EmployeeVisibleNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Decisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Divisions",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Divisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EducationRecords",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CourseStudied = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CertificateAwarded = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    QualificationLevel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Grade = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Specialization = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ProfessionalQualifications = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Affiliations = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CertificateNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DateCommenced = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateCompleted = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InstitutionRecognitionStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    InstitutionRecognitionEvidenceFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelevantFieldStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RelevanceReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelevanceReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CertificateFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeContacts",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeContacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeImportBatches",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FileChecksum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceFormat = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CommittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CommitJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ReadyRows = table.Column<int>(type: "int", nullable: false),
                    ReviewRows = table.Column<int>(type: "int", nullable: false),
                    ConflictRows = table.Column<int>(type: "int", nullable: false),
                    CreatedRows = table.Column<int>(type: "int", nullable: false),
                    UpdatedRows = table.Column<int>(type: "int", nullable: false),
                    SkippedRows = table.Column<int>(type: "int", nullable: false),
                    WarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeImportFieldMappings",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceColumn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CanonicalField = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MappingMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeImportFieldMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeImportRows",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SheetName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    SourceInstituteText = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MatchedEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchReason = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProposedAction = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FieldDiffsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AppliedResult = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AppliedMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeImportRows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NormalizedStaffId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Surname = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OtherNames = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PreferredName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: true),
                    Religion = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: true),
                    MaritalStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PrimaryEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    NormalizedPrimaryEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ProfileStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsHrApproved = table.Column<bool>(type: "bit", nullable: false),
                    IsContactVerified = table.Column<bool>(type: "bit", nullable: false),
                    ProfileImageFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmploymentRecords",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StaffCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    GradeStep = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ServiceStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AppointmentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PromotionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetirementDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Organization = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Region = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    District = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PensionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PensionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmploymentRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Faqs",
                schema: "comms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DisplayOrder = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faqs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FileRecords",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Classification = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    RetentionRule = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GradeEquivalencies",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquivalentTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEquivalentTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CanonicalGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PromotionStream = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EvidenceFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeEquivalencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Grades",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StaffCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PromotionStream = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PromotionLevel = table.Column<short>(type: "smallint", nullable: true),
                    Rank = table.Column<short>(type: "smallint", nullable: false),
                    IsPromotionGrade = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HolidayPeriods",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaveYear = table.Column<short>(type: "smallint", nullable: false),
                    ChristmasStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChristmasEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NewYearStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NewYearEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AvailabilityStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AvailabilityEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeductionDays = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinalizedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidayPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Holidays",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HolidayDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsFullDay = table.Column<bool>(type: "bit", nullable: false),
                    IsIslamic = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IndicatorMeasurements",
                schema: "plan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IndicatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportingPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvidenceFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndicatorMeasurements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Indicators",
                schema: "plan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutputId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BaselineValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    VerificationMethod = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Indicators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Institutes",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ParentInstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EmailDomain = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveBalances",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LeaveYear = table.Column<short>(type: "smallint", nullable: false),
                    TotalDays = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    UsedDays = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    PendingDays = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    AdjustedDays = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveBalances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeavePolicies",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaveType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PositionTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AnnualEntitlementDays = table.Column<short>(type: "smallint", nullable: false),
                    MaxConsecutiveDays = table.Column<short>(type: "smallint", nullable: true),
                    RequiresDocument = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RulesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeavePolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkingDays = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CurrentApprovalStage = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HandoverNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DelegateEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MedicalDocumentFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdmissionLetterFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HandoverDocumentFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegacyIdMappings",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyImportRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDatabase = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceTable = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TargetSchema = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetTable = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    MatchStrategy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowChecksum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyIdMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegacyImportIssues",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyImportRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDatabase = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceTable = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ResolutionStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyImportIssues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegacyImportRuns",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceBackupPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    SourceBackupSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SourceTableCount = table.Column<int>(type: "int", nullable: false),
                    SourceRowCount = table.Column<int>(type: "int", nullable: false),
                    TargetInsertedCount = table.Column<int>(type: "int", nullable: false),
                    TargetUpdatedCount = table.Column<int>(type: "int", nullable: false),
                    IssueCount = table.Column<int>(type: "int", nullable: false),
                    RowCountsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyImportRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Memos",
                schema: "comms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AudienceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "comms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ActionLink = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outputs",
                schema: "plan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThrustId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DisplayOrder = table.Column<short>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outputs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Paths",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PolicySourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StaffCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PromotionStream = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MinimumYearsInSourceGrade = table.Column<short>(type: "smallint", nullable: false),
                    RequiredQualificationLevel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequiresRecognisedInstitution = table.Column<bool>(type: "bit", nullable: false),
                    RequiresRelevantField = table.Column<bool>(type: "bit", nullable: false),
                    RequiresSatisfactoryAppraisal = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceAppraisals",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalPeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppraisalPeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SourceFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceAppraisals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PolicySources",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SourceFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentVersion = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SectionReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PageReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceChecksum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicySources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PositionTypes",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AnnualLeaveDays = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectFundings",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundingType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectFundings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMilestones",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DisplayOrder = table.Column<short>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMilestones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LeadEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Objective = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ExpectedResult = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ActualResult = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Nature = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    BudgetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Innovation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Impact = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ThrustId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectUpdates",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportingPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProgressPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Risks = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NextSteps = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectUpdates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Publications",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnologyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Abstract = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PublishedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicationType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LeadEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Citation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Publications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QualificationAssessments",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EducationRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QualificationRequirementMet = table.Column<bool>(type: "bit", nullable: false),
                    InstitutionRecognitionVerified = table.Column<bool>(type: "bit", nullable: false),
                    RelevantFieldVerified = table.Column<bool>(type: "bit", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualificationAssessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportingPeriods",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PeriodType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportingPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportingPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Abstract = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    KeyResults = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Conclusion = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReturnReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsSystemRole = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sections",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SkeletalStaffRequests",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HolidayPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedDatesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectedStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SelectedEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CurrentApprovalStage = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SignatureName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeaveCreditYear = table.Column<short>(type: "smallint", nullable: true),
                    LeaveCreditedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkeletalStaffRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatusSnapshots",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LatestAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LatestPromotionSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssessmentState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EligibilityState = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PromotionSubmissionStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SourceAssessmentVersion = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrategicPlans",
                schema: "plan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Definition = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Objective = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    StartYear = table.Column<short>(type: "smallint", nullable: false),
                    EndYear = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionDeclarations",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequirementSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeclarationTextSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionDeclarations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionDocuments",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequirementSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EmployeeVisibleReviewNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionReports",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequirementSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RenderedFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastSavedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionRequirementSnapshots",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequirementTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequirementType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DeclarationText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<short>(type: "smallint", nullable: false),
                    ReportTemplateCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AcceptedContentTypesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaximumFileBytes = table.Column<long>(type: "bigint", nullable: true),
                    MaximumDocumentCount = table.Column<short>(type: "smallint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionRequirementSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionRequirementTemplates",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionPathId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequirementType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DeclarationText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<short>(type: "smallint", nullable: false),
                    ReportTemplateCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AcceptedContentTypesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaximumFileBytes = table.Column<long>(type: "bigint", nullable: true),
                    MaximumDocumentCount = table.Column<short>(type: "smallint", nullable: true),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionRequirementTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Submissions",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicantUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionPathId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedTargetJobTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmployeeNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApplicantDeclarationAcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RequirementsLockedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReturnedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SuccessStories",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuccessStories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Technologies",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ApplicationArea = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LeadEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TechnologyType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    YearIntroduced = table.Column<short>(type: "smallint", nullable: true),
                    HasIntellectualProperty = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Technologies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Thrusts",
                schema: "plan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrategicPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Objective = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DisplayOrder = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Thrusts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IdentityType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeChildren",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BirthCertificateNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BirthCertificateFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeChildren", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeChildren_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeSpouses",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Occupation = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Employer = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSpouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeSpouses_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstituteAliases",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedAlias = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstituteAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstituteAliases_Institutes_InstituteId",
                        column: x => x.InstituteId,
                        principalSchema: "org",
                        principalTable: "Institutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "iam",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                schema: "iam",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "iam",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "iam",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                schema: "iam",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAssessments_PromotionAssessmentId_PerformanceAppraisalId",
                schema: "promotions",
                table: "AppraisalAssessments",
                columns: new[] { "PromotionAssessmentId", "PerformanceAppraisalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_EmployeeId_PromotionCycleId_PromotionPathId",
                schema: "promotions",
                table: "Assessments",
                columns: new[] { "EmployeeId", "PromotionCycleId", "PromotionPathId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_ActorUserId_OccurredAt",
                schema: "ops",
                table: "AuditRecords",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_CorrelationId",
                schema: "ops",
                table: "AuditRecords",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_TargetType_TargetId_OccurredAt",
                schema: "ops",
                table: "AuditRecords",
                columns: new[] { "TargetType", "TargetId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Cycles_CycleYear",
                schema: "promotions",
                table: "Cycles",
                column: "CycleYear",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Decisions_PromotionSubmissionId_DecidedAt",
                schema: "promotions",
                table: "Decisions",
                columns: new[] { "PromotionSubmissionId", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_InstituteId_IsActive",
                schema: "org",
                table: "Divisions",
                columns: new[] { "InstituteId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_InstituteId_Name",
                schema: "org",
                table: "Divisions",
                columns: new[] { "InstituteId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EducationRecords_EmployeeId_QualificationLevel_DateCompleted",
                schema: "hr",
                table: "EducationRecords",
                columns: new[] { "EmployeeId", "QualificationLevel", "DateCompleted" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeChildren_EmployeeId",
                schema: "hr",
                table: "EmployeeChildren",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeContacts_EmployeeId_ContactType_IsPrimary",
                schema: "hr",
                table: "EmployeeContacts",
                columns: new[] { "EmployeeId", "ContactType", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportBatches_InstituteId_CreatedAt",
                schema: "hr",
                table: "EmployeeImportBatches",
                columns: new[] { "InstituteId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportBatches_Status_CreatedAt",
                schema: "hr",
                table: "EmployeeImportBatches",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportFieldMappings_BatchId_SourceColumn",
                schema: "hr",
                table: "EmployeeImportFieldMappings",
                columns: new[] { "BatchId", "SourceColumn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportRows_BatchId_MatchedEmployeeId",
                schema: "hr",
                table: "EmployeeImportRows",
                columns: new[] { "BatchId", "MatchedEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportRows_BatchId_ReviewStatus",
                schema: "hr",
                table: "EmployeeImportRows",
                columns: new[] { "BatchId", "ReviewStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportRows_BatchId_SheetName_RowNumber",
                schema: "hr",
                table: "EmployeeImportRows",
                columns: new[] { "BatchId", "SheetName", "RowNumber" },
                unique: true,
                filter: "[SheetName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_InstituteId_NormalizedStaffId",
                schema: "hr",
                table: "Employees",
                columns: new[] { "InstituteId", "NormalizedStaffId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_InstituteId_ProfileStatus_Surname_OtherNames",
                schema: "hr",
                table: "Employees",
                columns: new[] { "InstituteId", "ProfileStatus", "Surname", "OtherNames" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_NormalizedPrimaryEmail",
                schema: "hr",
                table: "Employees",
                column: "NormalizedPrimaryEmail",
                unique: true,
                filter: "[NormalizedPrimaryEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSpouses_EmployeeId",
                schema: "hr",
                table: "EmployeeSpouses",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentRecords_EmployeeId_IsCurrent",
                schema: "hr",
                table: "EmploymentRecords",
                columns: new[] { "EmployeeId", "IsCurrent" },
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Faqs_InstituteId_DisplayOrder",
                schema: "comms",
                table: "Faqs",
                columns: new[] { "InstituteId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_FileRecords_ExpiresAt",
                schema: "ops",
                table: "FileRecords",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_FileRecords_StorageKey",
                schema: "ops",
                table: "FileRecords",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradeEquivalencies_NormalizedEquivalentTitle_StaffCategory_PromotionStream",
                schema: "promotions",
                table: "GradeEquivalencies",
                columns: new[] { "NormalizedEquivalentTitle", "StaffCategory", "PromotionStream" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grades_Code",
                schema: "org",
                table: "Grades",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HolidayPeriods_ScopeType_InstituteId_LeaveYear",
                schema: "leave",
                table: "HolidayPeriods",
                columns: new[] { "ScopeType", "InstituteId", "LeaveYear" },
                unique: true,
                filter: "[InstituteId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_HolidayDate",
                schema: "leave",
                table: "Holidays",
                column: "HolidayDate");

            migrationBuilder.CreateIndex(
                name: "IX_IndicatorMeasurements_IndicatorId_ReportingPeriodId",
                schema: "plan",
                table: "IndicatorMeasurements",
                columns: new[] { "IndicatorId", "ReportingPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Indicators_OutputId_Code",
                schema: "plan",
                table: "Indicators",
                columns: new[] { "OutputId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstituteAliases_InstituteId",
                schema: "org",
                table: "InstituteAliases",
                column: "InstituteId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteAliases_NormalizedAlias",
                schema: "org",
                table: "InstituteAliases",
                column: "NormalizedAlias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Institutes_Code",
                schema: "org",
                table: "Institutes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Institutes_NormalizedName",
                schema: "org",
                table: "Institutes",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_EmployeeId_LeaveYear_LeaveType",
                schema: "leave",
                table: "LeaveBalances",
                columns: new[] { "EmployeeId", "LeaveYear", "LeaveType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeavePolicies_ScopeType_InstituteId_LeaveType",
                schema: "leave",
                table: "LeavePolicies",
                columns: new[] { "ScopeType", "InstituteId", "LeaveType" },
                unique: true,
                filter: "[InstituteId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeId_Status",
                schema: "leave",
                table: "LeaveRequests",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_StartDate",
                schema: "leave",
                table: "LeaveRequests",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyIdMappings_LegacyImportRunId_SourceDatabase_SourceTable_SourceKey",
                schema: "ops",
                table: "LegacyIdMappings",
                columns: new[] { "LegacyImportRunId", "SourceDatabase", "SourceTable", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacyIdMappings_TargetSchema_TargetTable_TargetId",
                schema: "ops",
                table: "LegacyIdMappings",
                columns: new[] { "TargetSchema", "TargetTable", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyImportIssues_LegacyImportRunId_Severity_ResolutionStatus",
                schema: "ops",
                table: "LegacyImportIssues",
                columns: new[] { "LegacyImportRunId", "Severity", "ResolutionStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyImportIssues_SourceDatabase_SourceTable_SourceKey",
                schema: "ops",
                table: "LegacyImportIssues",
                columns: new[] { "SourceDatabase", "SourceTable", "SourceKey" });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyImportRuns_SourceName_StartedAt",
                schema: "ops",
                table: "LegacyImportRuns",
                columns: new[] { "SourceName", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyImportRuns_Status",
                schema: "ops",
                table: "LegacyImportRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Memos_InstituteId_Status_PublishedAt",
                schema: "comms",
                table: "Memos",
                columns: new[] { "InstituteId", "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_IsRead_CreatedAt",
                schema: "comms",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Outputs_ThrustId_Code",
                schema: "plan",
                table: "Outputs",
                columns: new[] { "ThrustId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Paths_PolicySourceId_StaffCategory_PromotionStream_SourceGradeId",
                schema: "promotions",
                table: "Paths",
                columns: new[] { "PolicySourceId", "StaffCategory", "PromotionStream", "SourceGradeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_EmployeeId_AppraisalPeriodStart_AppraisalPeriodEnd",
                schema: "hr",
                table: "PerformanceAppraisals",
                columns: new[] { "EmployeeId", "AppraisalPeriodStart", "AppraisalPeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                schema: "iam",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicySources_SourceChecksum_SectionReference",
                schema: "promotions",
                table: "PolicySources",
                columns: new[] { "SourceChecksum", "SectionReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PositionTypes_Code",
                schema: "org",
                table: "PositionTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestones_ProjectId_DisplayOrder",
                schema: "projects",
                table: "ProjectMilestones",
                columns: new[] { "ProjectId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_InstituteId_Code",
                schema: "projects",
                table: "Projects",
                columns: new[] { "InstituteId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_LeadEmployeeId",
                schema: "projects",
                table: "Projects",
                column: "LeadEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUpdates_ReportingPeriodId",
                schema: "projects",
                table: "ProjectUpdates",
                column: "ReportingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_InstituteId_Title",
                schema: "knowledge",
                table: "Publications",
                columns: new[] { "InstituteId", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_QualificationAssessments_PromotionAssessmentId_EducationRecordId",
                schema: "promotions",
                table: "QualificationAssessments",
                columns: new[] { "PromotionAssessmentId", "EducationRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                schema: "iam",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_ExpiresAt",
                schema: "iam",
                table: "RefreshTokens",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportingPeriods_ScopeType_InstituteId_Code",
                schema: "reporting",
                table: "ReportingPeriods",
                columns: new[] { "ScopeType", "InstituteId", "Code" },
                unique: true,
                filter: "[InstituteId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_InstituteId_ReportingPeriodId_ReportType",
                schema: "reporting",
                table: "Reports",
                columns: new[] { "InstituteId", "ReportingPeriodId", "ReportType" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                schema: "iam",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Code",
                schema: "iam",
                table: "Roles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "iam",
                table: "Roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_DivisionId_Name",
                schema: "org",
                table: "Sections",
                columns: new[] { "DivisionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkeletalStaffRequests_EmployeeId_HolidayPeriodId",
                schema: "leave",
                table: "SkeletalStaffRequests",
                columns: new[] { "EmployeeId", "HolidayPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatusSnapshots_EmployeeId_PromotionCycleId",
                schema: "promotions",
                table: "StatusSnapshots",
                columns: new[] { "EmployeeId", "PromotionCycleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlans_InstituteId_Code",
                schema: "plan",
                table: "StrategicPlans",
                columns: new[] { "InstituteId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionDeclarations_PromotionSubmissionId_RequirementSnapshotId",
                schema: "promotions",
                table: "SubmissionDeclarations",
                columns: new[] { "PromotionSubmissionId", "RequirementSnapshotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionDocuments_PromotionSubmissionId_RequirementSnapshotId_FileId",
                schema: "promotions",
                table: "SubmissionDocuments",
                columns: new[] { "PromotionSubmissionId", "RequirementSnapshotId", "FileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionReports_PromotionSubmissionId_RequirementSnapshotId",
                schema: "promotions",
                table: "SubmissionReports",
                columns: new[] { "PromotionSubmissionId", "RequirementSnapshotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionRequirementSnapshots_PromotionSubmissionId_Code",
                schema: "promotions",
                table: "SubmissionRequirementSnapshots",
                columns: new[] { "PromotionSubmissionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionRequirementTemplates_PromotionCycleId_PromotionPathId_Code",
                schema: "promotions",
                table: "SubmissionRequirementTemplates",
                columns: new[] { "PromotionCycleId", "PromotionPathId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_EmployeeId_Status",
                schema: "promotions",
                table: "Submissions",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_InstituteId_Status_SubmittedAt",
                schema: "promotions",
                table: "Submissions",
                columns: new[] { "InstituteId", "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SuccessStories_InstituteId_ProjectId",
                schema: "knowledge",
                table: "SuccessStories",
                columns: new[] { "InstituteId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Technologies_InstituteId_Code",
                schema: "knowledge",
                table: "Technologies",
                columns: new[] { "InstituteId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Thrusts_StrategicPlanId_Code",
                schema: "plan",
                table: "Thrusts",
                columns: new[] { "StrategicPlanId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                schema: "iam",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                schema: "iam",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "iam",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "iam",
                table: "Users",
                column: "NormalizedEmail",
                unique: true,
                filter: "[NormalizedEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_InstituteId_AccountStatus",
                schema: "iam",
                table: "Users",
                columns: new[] { "InstituteId", "AccountStatus" });

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "iam",
                table: "Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppraisalAssessments",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "Assessments",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "AuditRecords",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "Cycles",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "Decisions",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "Divisions",
                schema: "org");

            migrationBuilder.DropTable(
                name: "EducationRecords",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "EmployeeChildren",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "EmployeeContacts",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "EmployeeImportBatches",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "EmployeeImportFieldMappings",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "EmployeeImportRows",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "EmployeeSpouses",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "EmploymentRecords",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "Faqs",
                schema: "comms");

            migrationBuilder.DropTable(
                name: "FileRecords",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "GradeEquivalencies",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "Grades",
                schema: "org");

            migrationBuilder.DropTable(
                name: "HolidayPeriods",
                schema: "leave");

            migrationBuilder.DropTable(
                name: "Holidays",
                schema: "leave");

            migrationBuilder.DropTable(
                name: "IndicatorMeasurements",
                schema: "plan");

            migrationBuilder.DropTable(
                name: "Indicators",
                schema: "plan");

            migrationBuilder.DropTable(
                name: "InstituteAliases",
                schema: "org");

            migrationBuilder.DropTable(
                name: "LeaveBalances",
                schema: "leave");

            migrationBuilder.DropTable(
                name: "LeavePolicies",
                schema: "leave");

            migrationBuilder.DropTable(
                name: "LeaveRequests",
                schema: "leave");

            migrationBuilder.DropTable(
                name: "LegacyIdMappings",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "LegacyImportIssues",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "LegacyImportRuns",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "Memos",
                schema: "comms");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "comms");

            migrationBuilder.DropTable(
                name: "Outputs",
                schema: "plan");

            migrationBuilder.DropTable(
                name: "Paths",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "PerformanceAppraisals",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "PolicySources",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "PositionTypes",
                schema: "org");

            migrationBuilder.DropTable(
                name: "ProjectFundings",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "ProjectMilestones",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "ProjectUpdates",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "Publications",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "QualificationAssessments",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "RefreshTokens",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "ReportingPeriods",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "Reports",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "RoleClaims",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "Sections",
                schema: "org");

            migrationBuilder.DropTable(
                name: "SkeletalStaffRequests",
                schema: "leave");

            migrationBuilder.DropTable(
                name: "StatusSnapshots",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "StrategicPlans",
                schema: "plan");

            migrationBuilder.DropTable(
                name: "SubmissionDeclarations",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "SubmissionDocuments",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "SubmissionReports",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "SubmissionRequirementSnapshots",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "SubmissionRequirementTemplates",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "Submissions",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "SuccessStories",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "Technologies",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "Thrusts",
                schema: "plan");

            migrationBuilder.DropTable(
                name: "UserClaims",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "UserLogins",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "UserTokens",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "Institutes",
                schema: "org");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "iam");
        }
    }
}
