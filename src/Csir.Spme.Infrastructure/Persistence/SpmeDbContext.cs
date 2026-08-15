using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Org;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Leave;
using Csir.Spme.Domain.Plan;
using Csir.Spme.Domain.Projects;
using Csir.Spme.Domain.Reporting;
using Csir.Spme.Domain.Knowledge;
using Csir.Spme.Domain.Comms;
using Csir.Spme.Domain.Promotions;

namespace Csir.Spme.Infrastructure.Persistence;

public partial class SpmeDbContext : IdentityDbContext<User, Role, Guid>, IApplicationDbContext,
    IReportingPeriodRepository,
    IReportRepository,
    IStaffQuarterlyReportRepository,
    IPromotionReportRepository,
    ITechnologyRepository,
    IProjectRepository,
    IStrategicPlanRepository,
    IThrustRepository,
    IOutputRepository,
    IIndicatorRepository,
    IIndicatorMeasurementRepository,
    ILeaveRequestRepository
{
    private readonly bool _useSqlServerRowVersion;

    public SpmeDbContext(DbContextOptions<SpmeDbContext> options)
        : this(options, new RowVersionMapping())
    {
    }

    public SpmeDbContext(DbContextOptions<SpmeDbContext> options, RowVersionMapping mapping)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        _useSqlServerRowVersion = mapping.UseSqlServerRowVersion;
    }

    // IAM
    public new DbSet<User> Users => Set<User>();
    public new DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserLoginIdentifier> UserLoginIdentifiers => Set<UserLoginIdentifier>();
    public DbSet<AccountActivationChallenge> AccountActivationChallenges => Set<AccountActivationChallenge>();
    public DbSet<VerificationChallenge> VerificationChallenges => Set<VerificationChallenge>();
    public DbSet<PasswordResetRequest> PasswordResetRequests => Set<PasswordResetRequest>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    // ORG
    public DbSet<Institute> Institutes => Set<Institute>();
    public DbSet<InstituteAlias> InstituteAliases => Set<InstituteAlias>();
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<PositionType> PositionTypes => Set<PositionType>();
    public DbSet<Grade> Grades => Set<Grade>();

    // HR
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmploymentRecord> EmploymentRecords => Set<EmploymentRecord>();
    public DbSet<EmployeeContact> EmployeeContacts => Set<EmployeeContact>();
    public DbSet<EmployeeSpouse> EmployeeSpouses => Set<EmployeeSpouse>();
    public DbSet<EmployeeChild> EmployeeChildren => Set<EmployeeChild>();
    public DbSet<EducationRecord> EducationRecords => Set<EducationRecord>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeDocumentUploadSession> EmployeeDocumentUploadSessions => Set<EmployeeDocumentUploadSession>();
    public DbSet<PerformanceAppraisal> PerformanceAppraisals => Set<PerformanceAppraisal>();
    public DbSet<EmployeeImportBatch> EmployeeImportBatches => Set<EmployeeImportBatch>();
    public DbSet<EmployeeImportRow> EmployeeImportRows => Set<EmployeeImportRow>();
    public DbSet<EmployeeImportFieldMapping> EmployeeImportFieldMappings => Set<EmployeeImportFieldMapping>();

    // Leave
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeavePolicy> LeavePolicies => Set<LeavePolicy>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequestApproval> LeaveRequestApprovals => Set<LeaveRequestApproval>();
    public DbSet<LeaveHandover> LeaveHandovers => Set<LeaveHandover>();
    public DbSet<LeaveResumption> LeaveResumptions => Set<LeaveResumption>();
    public DbSet<LeaveResumptionApproval> LeaveResumptionApprovals => Set<LeaveResumptionApproval>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<HolidayPeriod> HolidayPeriods => Set<HolidayPeriod>();
    public DbSet<CompassionateLeaveType> CompassionateLeaveTypes => Set<CompassionateLeaveType>();
    public DbSet<SkeletalStaffRequest> SkeletalStaffRequests => Set<SkeletalStaffRequest>();
    public DbSet<SkeletalStaffApproval> SkeletalStaffApprovals => Set<SkeletalStaffApproval>();

    // Plan
    public DbSet<StrategicPlan> StrategicPlans => Set<StrategicPlan>();
    public DbSet<Thrust> Thrusts => Set<Thrust>();
    public DbSet<Output> Outputs => Set<Output>();
    public DbSet<Indicator> Indicators => Set<Indicator>();
    public DbSet<IndicatorMeasurement> IndicatorMeasurements => Set<IndicatorMeasurement>();

    // Projects
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectSponsor> ProjectSponsors => Set<ProjectSponsor>();
    public DbSet<ProjectMilestone> ProjectMilestones => Set<ProjectMilestone>();
    public DbSet<ProjectFunding> ProjectFundings => Set<ProjectFunding>();
    public DbSet<ProjectUpdate> ProjectUpdates => Set<ProjectUpdate>();
    public DbSet<ProjectInception> ProjectInceptions => Set<ProjectInception>();

    // Reporting
    public DbSet<ReportingPeriod> ReportingPeriods => Set<ReportingPeriod>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportMetric> ReportMetrics => Set<ReportMetric>();
    public DbSet<ReportProject> ReportProjects => Set<ReportProject>();
    public DbSet<ReportTechnology> ReportTechnologies => Set<ReportTechnology>();
    public DbSet<ReportAttachment> ReportAttachments => Set<ReportAttachment>();
    public DbSet<StaffQuarterlyReportUploadSession> StaffQuarterlyReportUploadSessions =>
        Set<StaffQuarterlyReportUploadSession>();

    // Knowledge
    public DbSet<Technology> Technologies => Set<Technology>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<SuccessStory> SuccessStories => Set<SuccessStory>();

    // Comms
    public DbSet<Memo> Memos => Set<Memo>();
    public DbSet<MemoAudience> MemoAudiences => Set<MemoAudience>();
    public DbSet<MemoAcknowledgement> MemoAcknowledgements => Set<MemoAcknowledgement>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<CommunicationOutboxMessage> CommunicationOutboxMessages => Set<CommunicationOutboxMessage>();
    public DbSet<CommunicationDeliveryAttempt> CommunicationDeliveryAttempts => Set<CommunicationDeliveryAttempt>();
    public DbSet<Faq> Faqs => Set<Faq>();

    // Promotions
    public DbSet<PromotionPolicySource> PromotionPolicySources => Set<PromotionPolicySource>();
    public DbSet<PromotionCycle> PromotionCycles => Set<PromotionCycle>();
    public DbSet<PromotionPath> PromotionPaths => Set<PromotionPath>();
    public DbSet<PromotionGradeEquivalency> PromotionGradeEquivalencies => Set<PromotionGradeEquivalency>();
    public DbSet<PromotionAssessment> PromotionAssessments => Set<PromotionAssessment>();
    public DbSet<PromotionQualificationAssessment> PromotionQualificationAssessments => Set<PromotionQualificationAssessment>();
    public DbSet<PromotionAppraisalAssessment> PromotionAppraisalAssessments => Set<PromotionAppraisalAssessment>();
    public DbSet<PromotionSubmissionRequirementTemplate> PromotionSubmissionRequirementTemplates => Set<PromotionSubmissionRequirementTemplate>();
    public DbSet<PromotionSubmissionRequirementSnapshot> PromotionSubmissionRequirementSnapshots => Set<PromotionSubmissionRequirementSnapshot>();
    public DbSet<PromotionSubmission> PromotionSubmissions => Set<PromotionSubmission>();
    public DbSet<PromotionSubmissionReport> PromotionSubmissionReports => Set<PromotionSubmissionReport>();
    public DbSet<PromotionSubmissionDeclaration> PromotionSubmissionDeclarations => Set<PromotionSubmissionDeclaration>();
    public DbSet<PromotionSubmissionDocument> PromotionSubmissionDocuments => Set<PromotionSubmissionDocument>();
    public DbSet<PromotionDecision> PromotionDecisions => Set<PromotionDecision>();
    public DbSet<PromotionStatusSnapshot> PromotionStatusSnapshots => Set<PromotionStatusSnapshot>();
    public DbSet<PromotionDocumentUploadSession> PromotionDocumentUploadSessions => Set<PromotionDocumentUploadSession>();

    // Common
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<FileRecord> FileRecords => Set<FileRecord>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
    public DbSet<LegacyImportRun> LegacyImportRuns => Set<LegacyImportRun>();
    public DbSet<LegacyIdMapping> LegacyIdMappings => Set<LegacyIdMapping>();
    public DbSet<LegacyImportIssue> LegacyImportIssues => Set<LegacyImportIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("dbo");

        modelBuilder.Entity<User>().ToTable("Users", "iam");
        modelBuilder.Entity<Role>().ToTable("Roles", "iam");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles", "iam");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims", "iam");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins", "iam");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims", "iam");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens", "iam");

        // Apply all entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SpmeDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(entityType => typeof(BaseEntity).IsAssignableFrom(entityType.ClrType)))
        {
            var property = modelBuilder.Entity(entityType.ClrType)
                .Property<byte[]>(nameof(BaseEntity.RowVersion));
            if (UsesStoreGeneratedRowVersion)
                property.IsRowVersion();
            else
                property.IsConcurrencyToken().ValueGeneratedNever();
        }

    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.GetType().GetProperty("CreatedAt")?.SetValue(entry.Entity, DateTimeOffset.UtcNow);
                    entry.Entity.GetType().GetProperty("UpdatedAt")?.SetValue(entry.Entity, DateTimeOffset.UtcNow);
                    if (!UsesStoreGeneratedRowVersion)
                        entry.Property(nameof(BaseEntity.RowVersion)).CurrentValue = Guid.NewGuid().ToByteArray();
                    break;
                case EntityState.Modified:
                    entry.Entity.GetType().GetProperty("UpdatedAt")?.SetValue(entry.Entity, DateTimeOffset.UtcNow);
                    if (!UsesStoreGeneratedRowVersion)
                        entry.Property(nameof(BaseEntity.RowVersion)).CurrentValue = Guid.NewGuid().ToByteArray();
                    break;
            }
        }

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(
                "The resource was modified by another request.",
                exception);
        }
    }

    private bool UsesStoreGeneratedRowVersion => Database.IsSqlServer() && _useSqlServerRowVersion;

    public void SetOriginalRowVersion<TEntity>(TEntity entity, byte[] rowVersion) where TEntity : BaseEntity
    {
        Entry(entity).Property(nameof(BaseEntity.RowVersion)).OriginalValue = rowVersion;
    }
}
