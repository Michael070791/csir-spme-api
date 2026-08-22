using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Csir.Spme.Domain.Hr;

namespace Csir.Spme.Infrastructure.Persistence.Configurations.Hr;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StaffId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.NormalizedStaffId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Prefix).HasMaxLength(32);
        builder.Property(x => x.Surname).HasMaxLength(128).IsRequired();
        builder.Property(x => x.OtherNames).HasMaxLength(256);
        builder.Property(x => x.PreferredName).HasMaxLength(256);
        builder.Property(x => x.Gender).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Nationality).HasMaxLength(96);
        builder.Property(x => x.Religion).HasMaxLength(96);
        builder.Property(x => x.MaritalStatus).HasMaxLength(32);
        builder.Property(x => x.PrimaryEmail).HasMaxLength(320);
        builder.Property(x => x.NormalizedPrimaryEmail).HasMaxLength(320);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.Address).HasMaxLength(512);
        builder.Property(x => x.ProfileStatus).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.InstituteId, x.NormalizedStaffId }).IsUnique();
        builder.HasIndex(x => x.NormalizedPrimaryEmail).IsUnique().HasFilter("[NormalizedPrimaryEmail] IS NOT NULL");
        builder.HasIndex(x => new { x.InstituteId, x.ProfileStatus, x.Surname, x.OtherNames });
    }
}

public class EmploymentRecordConfiguration : IEntityTypeConfiguration<EmploymentRecord>
{
    public void Configure(EntityTypeBuilder<EmploymentRecord> builder)
    {
        builder.ToTable("EmploymentRecords", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.JobTitle).HasMaxLength(256);
        builder.Property(x => x.LeadershipRoles).HasMaxLength(512);
        builder.Property(x => x.StaffCategory).HasMaxLength(64);
        builder.Property(x => x.GradeStep).HasMaxLength(32);
        builder.Property(x => x.AreaOfSpecialization).HasMaxLength(256);
        builder.Property(x => x.ServiceStatus).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Organization).HasMaxLength(256);
        builder.Property(x => x.Location).HasMaxLength(128);
        builder.Property(x => x.Region).HasMaxLength(128);
        builder.Property(x => x.ResearchInterests).HasMaxLength(2000);
        builder.Property(x => x.District).HasMaxLength(128);
        builder.Property(x => x.PensionType).HasMaxLength(32);
        builder.Property(x => x.PensionId).HasMaxLength(128);
        builder.HasIndex(x => new { x.EmployeeId, x.IsCurrent }).HasFilter("[IsCurrent] = 1");
    }
}

public class EmployeeContactConfiguration : IEntityTypeConfiguration<EmployeeContact>
{
    public void Configure(EntityTypeBuilder<EmployeeContact> builder)
    {
        builder.ToTable("EmployeeContacts", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContactType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Relationship).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.Address).HasMaxLength(512);
        builder.HasIndex(x => new { x.EmployeeId, x.ContactType, x.IsPrimary });
    }
}

public class EmployeeSpouseConfiguration : IEntityTypeConfiguration<EmployeeSpouse>
{
    public void Configure(EntityTypeBuilder<EmployeeSpouse> builder)
    {
        builder.ToTable("EmployeeSpouses", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.Occupation).HasMaxLength(256);
        builder.Property(x => x.Employer).HasMaxLength(256);
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.EmployeeId).IsUnique();
    }
}

public class EmployeeChildConfiguration : IEntityTypeConfiguration<EmployeeChild>
{
    public void Configure(EntityTypeBuilder<EmployeeChild> builder)
    {
        builder.ToTable("EmployeeChildren", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Gender).HasMaxLength(32).IsRequired();
        builder.Property(x => x.BirthCertificateNumber).HasMaxLength(128);
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.EmployeeId);
    }
}

public class EducationRecordConfiguration : IEntityTypeConfiguration<EducationRecord>
{
    public void Configure(EntityTypeBuilder<EducationRecord> builder)
    {
        builder.ToTable("EducationRecords", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InstitutionName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CourseStudied).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CertificateAwarded).HasMaxLength(256).IsRequired();
        builder.Property(x => x.QualificationLevel).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Grade).HasMaxLength(64);
        builder.Property(x => x.Specialization).HasMaxLength(256);
        builder.Property(x => x.ProfessionalQualifications).HasMaxLength(512);
        builder.Property(x => x.Affiliations).HasMaxLength(512);
        builder.Property(x => x.CertificateNumber).HasMaxLength(128);
        builder.Property(x => x.InstitutionRecognitionStatus).HasMaxLength(32).IsRequired();
        builder.Property(x => x.RelevantFieldStatus).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.EmployeeId, x.QualificationLevel, x.DateCompleted });
    }
}

public class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        builder.ToTable("EmployeeDocuments", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocumentType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.EmployeeId, x.DocumentType, x.Status });
        builder.HasIndex(x => new { x.EmployeeId, x.DocumentType, x.LinkedChildId, x.Status });
    }
}

public class EmployeeDocumentUploadSessionConfiguration : IEntityTypeConfiguration<EmployeeDocumentUploadSession>
{
    public void Configure(EntityTypeBuilder<EmployeeDocumentUploadSession> builder)
    {
        builder.ToTable("EmployeeDocumentUploadSessions", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocumentType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DeclaredSha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => new { x.EmployeeId, x.Status });
    }
}

public class PerformanceAppraisalConfiguration : IEntityTypeConfiguration<PerformanceAppraisal>
{
    public void Configure(EntityTypeBuilder<PerformanceAppraisal> builder)
    {
        builder.ToTable("PerformanceAppraisals", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Comments).HasMaxLength(4000);
        builder.Property(x => x.RoutingExceptionReason).HasMaxLength(2000);
        builder.Property(x => x.EmployeeSnapshotJson).IsRequired();
        builder.Property(x => x.AppraiserSnapshotJson).IsRequired();
        builder.Property(x => x.ApproverSnapshotJson).IsRequired();
        builder.Property(x => x.PlanningJson).IsRequired();
        builder.Property(x => x.MidyearJson).IsRequired();
        builder.Property(x => x.HodMidyearReviewJson).IsRequired();
        builder.Property(x => x.YearEndJson).IsRequired();
        builder.Property(x => x.HodAssessmentJson).IsRequired();
        builder.Property(x => x.StaffSignatureAttemptsJson).IsRequired();
        builder.Property(x => x.DirectorAssessmentJson).IsRequired();
        builder.HasIndex(x => new { x.EmployeeId, x.AppraisalCycleId, x.AppraisalPeriodStart, x.AppraisalPeriodEnd }).IsUnique();
        builder.HasIndex(x => new { x.EmployeeId, x.Status, x.UpdatedAt });
        builder.HasIndex(x => new { x.HodUserId, x.Status, x.AppraisalPeriodEnd });
        builder.HasIndex(x => new { x.DirectorUserId, x.Status, x.AppraisalPeriodEnd });
        builder.HasIndex(x => new { x.InstituteId, x.Status, x.UpdatedAt });
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppraisalCycle>().WithMany().HasForeignKey(x => x.AppraisalCycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Csir.Spme.Domain.Common.FileRecord>().WithMany().HasForeignKey(x => x.FinalDocumentFileId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_PerformanceAppraisals_Status", $"[Status] IN ('{string.Join("','", AppraisalStatuses.All)}')");
            table.HasCheckConstraint("CK_PerformanceAppraisals_Outcome", "[Outcome] IN ('', 'satisfactory', 'unsatisfactory')");
            table.HasCheckConstraint("CK_PerformanceAppraisals_Period", "[AppraisalPeriodStart] <= [AppraisalPeriodEnd]");
            table.HasCheckConstraint("CK_PerformanceAppraisals_DistinctReviewers", "[HodUserId] IS NULL OR [DirectorUserId] IS NULL OR [HodUserId] <> [DirectorUserId]");
            table.HasCheckConstraint("CK_PerformanceAppraisals_Scores", "([BehavioralScore] IS NULL OR CAST([BehavioralScore] AS REAL) BETWEEN 0 AND 50) AND ([CoreScore] IS NULL OR CAST([CoreScore] AS REAL) BETWEEN 0 AND 50) AND ([TotalScore] IS NULL OR CAST([TotalScore] AS REAL) BETWEEN 0 AND 100)");
        });
    }
}

public sealed class AppraisalCycleConfiguration : IEntityTypeConfiguration<AppraisalCycle>
{
    public void Configure(EntityTypeBuilder<AppraisalCycle> builder)
    {
        builder.ToTable("AppraisalCycles", "hr"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ReopenReason).HasMaxLength(2000);
        builder.Property(x => x.FormTemplateVersion).HasMaxLength(128).IsRequired();
        builder.Property(x => x.FormTemplateChecksum).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.InstituteId, x.Year }).IsUnique();
        builder.HasIndex(x => new { x.InstituteId, x.Status, x.Year });
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_AppraisalCycles_Status", "[Status] IN ('draft','open','closed')");
            table.HasCheckConstraint("CK_AppraisalCycles_Windows", "[StartDate] <= [PlanningStart] AND [PlanningStart] <= [PlanningEnd] AND [PlanningEnd] < [MidyearStart] AND [MidyearStart] <= [MidyearEnd] AND [MidyearEnd] < [YearEndStart] AND [YearEndStart] <= [YearEndEnd] AND [YearEndEnd] <= [EndDate]");
        });
    }
}

public sealed class AppraisalTrainingRecordConfiguration : IEntityTypeConfiguration<AppraisalTrainingRecord>
{
    public void Configure(EntityTypeBuilder<AppraisalTrainingRecord> b) { b.ToTable("AppraisalTrainingRecords", "hr"); b.HasKey(x => x.Id); b.Property(x => x.Institution).HasMaxLength(256).IsRequired(); b.Property(x => x.Programme).HasMaxLength(512).IsRequired(); b.HasIndex(x => x.PerformanceAppraisalId); b.HasOne<PerformanceAppraisal>().WithMany().HasForeignKey(x => x.PerformanceAppraisalId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class AppraisalTargetConfiguration : IEntityTypeConfiguration<AppraisalTarget>
{
    public void Configure(EntityTypeBuilder<AppraisalTarget> b) { b.ToTable("AppraisalTargets", "hr", table => table.HasCheckConstraint("CK_AppraisalTargets_DisplayOrder", "[DisplayOrder] > 0")); b.HasKey(x => x.Id); b.Property(x => x.CoreArea).HasMaxLength(512).IsRequired(); b.Property(x => x.Target).HasMaxLength(4000).IsRequired(); b.Property(x => x.ResourcesRequired).HasMaxLength(4000).IsRequired(); b.Property(x => x.Timeline).HasMaxLength(512); b.HasIndex(x => new { x.PerformanceAppraisalId, x.DisplayOrder }); b.HasOne<PerformanceAppraisal>().WithMany().HasForeignKey(x => x.PerformanceAppraisalId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class AppraisalTargetVersionConfiguration : IEntityTypeConfiguration<AppraisalTargetVersion>
{
    public void Configure(EntityTypeBuilder<AppraisalTargetVersion> b) { b.ToTable("AppraisalTargetVersions", "hr"); b.HasKey(x => x.Id); b.Property(x => x.CoreArea).HasMaxLength(512).IsRequired(); b.Property(x => x.Target).HasMaxLength(4000).IsRequired(); b.Property(x => x.ResourcesRequired).HasMaxLength(4000).IsRequired(); b.Property(x => x.Timeline).HasMaxLength(512); b.HasIndex(x => new { x.AppraisalTargetId, x.Version }).IsUnique(); b.HasOne<AppraisalTarget>().WithMany().HasForeignKey(x => x.AppraisalTargetId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class AppraisalKeyCompetencyConfiguration : IEntityTypeConfiguration<AppraisalKeyCompetency>
{
    public void Configure(EntityTypeBuilder<AppraisalKeyCompetency> b) { b.ToTable("AppraisalKeyCompetencies", "hr"); b.HasKey(x => x.Id); b.Property(x => x.Competency).HasMaxLength(1000).IsRequired(); b.HasIndex(x => new { x.PerformanceAppraisalId, x.DisplayOrder }).IsUnique(); b.HasOne<PerformanceAppraisal>().WithMany().HasForeignKey(x => x.PerformanceAppraisalId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class AppraisalMidyearTargetReviewConfiguration : IEntityTypeConfiguration<AppraisalMidyearTargetReview>
{
    public void Configure(EntityTypeBuilder<AppraisalMidyearTargetReview> b) { b.ToTable("AppraisalMidyearTargetReviews", "hr"); b.HasKey(x => x.Id); b.Property(x => x.ProgressReview).HasMaxLength(4000).IsRequired(); b.Property(x => x.Remarks).HasMaxLength(2000); b.HasIndex(x => new { x.PerformanceAppraisalId, x.AppraisalTargetId }).IsUnique(); b.HasOne<PerformanceAppraisal>().WithMany().HasForeignKey(x => x.PerformanceAppraisalId).OnDelete(DeleteBehavior.Cascade); b.HasOne<AppraisalTarget>().WithMany().HasForeignKey(x => x.AppraisalTargetId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class AppraisalMidyearCompetencyReviewConfiguration : IEntityTypeConfiguration<AppraisalMidyearCompetencyReview>
{
    public void Configure(EntityTypeBuilder<AppraisalMidyearCompetencyReview> b) { b.ToTable("AppraisalMidyearCompetencyReviews", "hr"); b.HasKey(x => x.Id); b.Property(x => x.Competency).HasMaxLength(1000).IsRequired(); b.Property(x => x.ProgressReview).HasMaxLength(4000).IsRequired(); b.Property(x => x.Remarks).HasMaxLength(2000); b.HasIndex(x => new { x.PerformanceAppraisalId, x.Competency }).IsUnique(); b.HasOne<PerformanceAppraisal>().WithMany().HasForeignKey(x => x.PerformanceAppraisalId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class AppraisalYearEndResultConfiguration : IEntityTypeConfiguration<AppraisalYearEndResult>
{
    public void Configure(EntityTypeBuilder<AppraisalYearEndResult> b) { b.ToTable("AppraisalYearEndResults", "hr", table => table.HasCheckConstraint("CK_AppraisalYearEndResults_Percentage", "[WorkCompletedPercentage] BETWEEN 0 AND 100")); b.HasKey(x => x.Id); b.Property(x => x.WorkAccomplished).HasMaxLength(4000).IsRequired(); b.Property(x => x.ExtentAndConstraints).HasMaxLength(4000).IsRequired(); b.HasIndex(x => new { x.PerformanceAppraisalId, x.AppraisalTargetId }).IsUnique(); b.HasOne<PerformanceAppraisal>().WithMany().HasForeignKey(x => x.PerformanceAppraisalId).OnDelete(DeleteBehavior.Cascade); b.HasOne<AppraisalTarget>().WithMany().HasForeignKey(x => x.AppraisalTargetId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class AppraisalHodSubmissionConfiguration : IEntityTypeConfiguration<AppraisalHodSubmission>
{
    public void Configure(EntityTypeBuilder<AppraisalHodSubmission> b) { b.ToTable("AppraisalHodSubmissions", "hr", table => table.HasCheckConstraint("CK_AppraisalHodSubmissions_Phase", "[Phase] IN ('midyear','year-end')")); b.HasKey(x => x.Id); b.Property(x => x.Phase).HasMaxLength(32).IsRequired(); b.Property(x => x.ResponseToDecline).HasMaxLength(2000); b.Property(x => x.SupervisorComments).HasMaxLength(4000); b.HasIndex(x => new { x.PerformanceAppraisalId, x.Phase, x.Version }).IsUnique(); b.HasOne<PerformanceAppraisal>().WithMany().HasForeignKey(x => x.PerformanceAppraisalId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class AppraisalMidyearTargetRemarkConfiguration : IEntityTypeConfiguration<AppraisalMidyearTargetRemark>
{
    public void Configure(EntityTypeBuilder<AppraisalMidyearTargetRemark> b) { b.ToTable("AppraisalMidyearTargetRemarks", "hr"); b.HasKey(x => x.Id); b.Property(x => x.Remarks).HasMaxLength(2000); b.HasIndex(x => new { x.HodSubmissionId, x.AppraisalTargetId }).IsUnique(); b.HasOne<AppraisalHodSubmission>().WithMany().HasForeignKey(x => x.HodSubmissionId).OnDelete(DeleteBehavior.Cascade); b.HasOne<AppraisalTarget>().WithMany().HasForeignKey(x => x.AppraisalTargetId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class AppraisalMidyearCompetencyRemarkConfiguration : IEntityTypeConfiguration<AppraisalMidyearCompetencyRemark>
{
    public void Configure(EntityTypeBuilder<AppraisalMidyearCompetencyRemark> b) { b.ToTable("AppraisalMidyearCompetencyRemarks", "hr"); b.HasKey(x => x.Id); b.Property(x => x.Competency).HasMaxLength(1000).IsRequired(); b.Property(x => x.Remarks).HasMaxLength(2000); b.HasIndex(x => new { x.HodSubmissionId, x.Competency }).IsUnique(); b.HasOne<AppraisalHodSubmission>().WithMany().HasForeignKey(x => x.HodSubmissionId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class AppraisalTargetAmendmentConfiguration : IEntityTypeConfiguration<AppraisalTargetAmendment>
{
    public void Configure(EntityTypeBuilder<AppraisalTargetAmendment> b) { b.ToTable("AppraisalTargetAmendments", "hr", table => table.HasCheckConstraint("CK_AppraisalTargetAmendments_Status", "[Status] IN ('proposed','accepted','superseded')")); b.HasKey(x => x.Id); b.Property(x => x.OriginalTarget).HasMaxLength(4000).IsRequired(); b.Property(x => x.OriginalResourcesRequired).HasMaxLength(4000).IsRequired(); b.Property(x => x.OriginalTimeline).HasMaxLength(512); b.Property(x => x.RevisedTarget).HasMaxLength(4000).IsRequired(); b.Property(x => x.RevisedResourcesRequired).HasMaxLength(4000).IsRequired(); b.Property(x => x.RevisedTimeline).HasMaxLength(512); b.Property(x => x.Reason).HasMaxLength(2000).IsRequired(); b.Property(x => x.Status).HasMaxLength(32).IsRequired(); b.HasIndex(x => new { x.PerformanceAppraisalId, x.AppraisalTargetId, x.Version }).IsUnique(); b.HasOne<PerformanceAppraisal>().WithMany().HasForeignKey(x => x.PerformanceAppraisalId).OnDelete(DeleteBehavior.Restrict); b.HasOne<AppraisalTarget>().WithMany().HasForeignKey(x => x.AppraisalTargetId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class AppraisalTargetAssessmentRecordConfiguration : IEntityTypeConfiguration<AppraisalTargetAssessmentRecord>
{
    public void Configure(EntityTypeBuilder<AppraisalTargetAssessmentRecord> b) { b.ToTable("AppraisalTargetAssessments", "hr", table => table.HasCheckConstraint("CK_AppraisalTargetAssessments_Rating", "[Rating] BETWEEN 1 AND 5")); b.HasKey(x => x.Id); b.Property(x => x.Comments).HasMaxLength(2000); b.HasIndex(x => new { x.HodSubmissionId, x.AppraisalTargetId }).IsUnique(); b.HasOne<AppraisalHodSubmission>().WithMany().HasForeignKey(x => x.HodSubmissionId).OnDelete(DeleteBehavior.Cascade); b.HasOne<AppraisalTarget>().WithMany().HasForeignKey(x => x.AppraisalTargetId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class AppraisalCompetencyRatingRecordConfiguration : IEntityTypeConfiguration<AppraisalCompetencyRatingRecord>
{
    public void Configure(EntityTypeBuilder<AppraisalCompetencyRatingRecord> b) { b.ToTable("AppraisalCompetencyRatings", "hr", table => table.HasCheckConstraint("CK_AppraisalCompetencyRatings_Rating", "[Rating] IS NULL OR [Rating] BETWEEN 1 AND 5")); b.HasKey(x => x.Id); b.Property(x => x.FactorCode).HasMaxLength(64).IsRequired(); b.HasIndex(x => new { x.HodSubmissionId, x.FactorCode }).IsUnique(); b.HasOne<AppraisalHodSubmission>().WithMany().HasForeignKey(x => x.HodSubmissionId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class AppraisalSignatureRecordConfiguration : IEntityTypeConfiguration<AppraisalSignatureRecord>
{
    public void Configure(EntityTypeBuilder<AppraisalSignatureRecord> b) { b.ToTable("AppraisalSignatureRecords", "hr", table => { table.HasCheckConstraint("CK_AppraisalSignatureRecords_Phase", "[Phase] IN ('planning-employee','planning-hod','midyear-employee-submission','midyear-hod','midyear','year-end-employee-submission','year-end-hod','year-end')"); table.HasCheckConstraint("CK_AppraisalSignatureRecords_DeclineReason", "[Accepted] = 1 OR [DeclineReason] IS NOT NULL"); }); b.HasKey(x => x.Id); b.Property(x => x.Phase).HasMaxLength(32).IsRequired(); b.Property(x => x.Comments).HasMaxLength(4000); b.Property(x => x.DeclineReason).HasMaxLength(2000); b.HasIndex(x => new { x.PerformanceAppraisalId, x.Phase, x.Attempt }).IsUnique(); b.HasOne<PerformanceAppraisal>().WithMany().HasForeignKey(x => x.PerformanceAppraisalId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class AppraisalDirectorDecisionConfiguration : IEntityTypeConfiguration<AppraisalDirectorDecision>
{
    public void Configure(EntityTypeBuilder<AppraisalDirectorDecision> b) { b.ToTable("AppraisalDirectorDecisions", "hr", table => { table.HasCheckConstraint("CK_AppraisalDirectorDecisions_Phase", "[Phase] IN ('midyear','year-end')"); table.HasCheckConstraint("CK_AppraisalDirectorDecisions_Decision", "[Decision] IN ('approved','returned')"); }); b.HasKey(x => x.Id); b.Property(x => x.Phase).HasMaxLength(32).IsRequired(); b.Property(x => x.Decision).HasMaxLength(32).IsRequired(); b.Property(x => x.CommentsOnWork).HasMaxLength(4000).IsRequired(); b.Property(x => x.ReturnReason).HasMaxLength(2000); b.HasIndex(x => new { x.PerformanceAppraisalId, x.Phase, x.Version }).IsUnique(); b.HasOne<PerformanceAppraisal>().WithMany().HasForeignKey(x => x.PerformanceAppraisalId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class AppraisalReminderRecordConfiguration : IEntityTypeConfiguration<AppraisalReminderRecord>
{
    public void Configure(EntityTypeBuilder<AppraisalReminderRecord> b) { b.ToTable("AppraisalReminderRecords", "hr"); b.HasKey(x => x.Id); b.Property(x => x.Stage).HasMaxLength(32).IsRequired(); b.Property(x => x.OffsetCode).HasMaxLength(16).IsRequired(); b.HasIndex(x => new { x.PerformanceAppraisalId, x.Stage, x.OffsetCode }).IsUnique(); b.HasOne<PerformanceAppraisal>().WithMany().HasForeignKey(x => x.PerformanceAppraisalId).OnDelete(DeleteBehavior.Cascade); }
}

public class EmployeeImportBatchConfiguration : IEntityTypeConfiguration<EmployeeImportBatch>
{
    public void Configure(EntityTypeBuilder<EmployeeImportBatch> builder)
    {
        builder.ToTable("EmployeeImportBatches", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.FileChecksum).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.SourceFormat).HasMaxLength(16).IsRequired();
        builder.Property(x => x.WarningsJson).IsRequired();
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.InstituteId, x.CreatedAt });
    }
}

public class EmployeeImportRowConfiguration : IEntityTypeConfiguration<EmployeeImportRow>
{
    public void Configure(EntityTypeBuilder<EmployeeImportRow> builder)
    {
        builder.ToTable("EmployeeImportRows", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SheetName).HasMaxLength(128);
        builder.Property(x => x.SourceInstituteText).HasMaxLength(256);
        builder.Property(x => x.MatchReason).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ReviewStatus).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProposedAction).HasMaxLength(32).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.FieldDiffsJson).IsRequired();
        builder.Property(x => x.WarningsJson).IsRequired();
        builder.Property(x => x.AppliedResult).HasMaxLength(32).IsRequired();
        builder.Property(x => x.AppliedMessage).HasMaxLength(2000);
        builder.HasIndex(x => new { x.BatchId, x.SheetName, x.RowNumber }).IsUnique();
        builder.HasIndex(x => new { x.BatchId, x.ReviewStatus });
        builder.HasIndex(x => new { x.BatchId, x.MatchedEmployeeId });
    }
}

public class EmployeeImportFieldMappingConfiguration : IEntityTypeConfiguration<EmployeeImportFieldMapping>
{
    public void Configure(EntityTypeBuilder<EmployeeImportFieldMapping> builder)
    {
        builder.ToTable("EmployeeImportFieldMappings", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceColumn).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CanonicalField).HasMaxLength(64).IsRequired();
        builder.Property(x => x.MappingMode).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.BatchId, x.SourceColumn }).IsUnique();
    }
}

public class EmployeeGradePromotionDateConfiguration : IEntityTypeConfiguration<EmployeeGradePromotionDate>
{
    public void Configure(EntityTypeBuilder<EmployeeGradePromotionDate> builder)
    {
        builder.ToTable("EmployeeGradePromotionDates", "hr");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.EmployeeId, x.GradeId }).IsUnique();
    }
}
