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
        builder.Property(x => x.Outcome).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Comments).HasMaxLength(4000);
        builder.HasIndex(x => new { x.EmployeeId, x.AppraisalPeriodStart, x.AppraisalPeriodEnd }).IsUnique();
    }
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
