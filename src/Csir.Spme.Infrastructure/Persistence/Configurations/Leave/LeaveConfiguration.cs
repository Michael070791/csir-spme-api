using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Csir.Spme.Domain.Leave;

namespace Csir.Spme.Infrastructure.Persistence.Configurations.Leave;

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder) {
        builder.ToTable("LeaveRequests", "leave");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LeaveType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.CurrentApprovalStage).HasMaxLength(64);
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.HandoverNotes).HasMaxLength(2000);
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);
        builder.Property(x => x.WorkingDays).HasPrecision(9, 2);
        builder.HasIndex(x => new { x.EmployeeId, x.Status });
        builder.HasIndex(x => x.StartDate);
    }
}

public class LeavePolicyConfiguration : IEntityTypeConfiguration<LeavePolicy>
{
    public void Configure(EntityTypeBuilder<LeavePolicy> builder) {
        builder.ToTable("LeavePolicies", "leave");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ScopeType).HasMaxLength(16).IsRequired();
        builder.Property(x => x.LeaveType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.RulesJson);
        builder.HasIndex(x => new { x.ScopeType, x.InstituteId, x.LeaveType }).IsUnique().HasFilter("[InstituteId] IS NOT NULL");
    }
}

public class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder) {
        builder.ToTable("LeaveBalances", "leave");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LeaveType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.TotalDays).HasPrecision(9, 2);
        builder.Property(x => x.UsedDays).HasPrecision(9, 2);
        builder.Property(x => x.PendingDays).HasPrecision(9, 2);
        builder.Property(x => x.AdjustedDays).HasPrecision(9, 2);
        builder.HasIndex(x => new { x.EmployeeId, x.LeaveYear, x.LeaveType }).IsUnique();
    }
}

public class LeaveRequestApprovalConfiguration : IEntityTypeConfiguration<LeaveRequestApproval>
{
    public void Configure(EntityTypeBuilder<LeaveRequestApproval> builder)
    {
        builder.ToTable("LeaveRequestApprovals", "leave");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ApprovalStage).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Decision).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Comments).HasMaxLength(2000);
        builder.Property(x => x.SignatureName).HasMaxLength(256);
        builder.HasOne<LeaveRequest>().WithMany().HasForeignKey(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Csir.Spme.Domain.Iam.User>().WithMany().HasForeignKey(x => x.ApproverUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LeaveRequestId, x.ApprovalStage, x.Sequence }).IsUnique();
    }
}

public class LeaveHandoverConfiguration : IEntityTypeConfiguration<LeaveHandover>
{
    public void Configure(EntityTypeBuilder<LeaveHandover> builder)
    {
        builder.ToTable("LeaveHandovers", "leave");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.HasOne<LeaveRequest>().WithOne().HasForeignKey<LeaveHandover>(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.LeaveRequestId).IsUnique();
    }
}

public class LeaveResumptionConfiguration : IEntityTypeConfiguration<LeaveResumption>
{
    public void Configure(EntityTypeBuilder<LeaveResumption> builder)
    {
        builder.ToTable("LeaveResumptions", "leave");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.EmployeeSignatureName).HasMaxLength(256);
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);
        builder.HasOne<LeaveRequest>().WithOne().HasForeignKey<LeaveResumption>(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.LeaveRequestId).IsUnique();
    }
}

public class LeaveResumptionApprovalConfiguration : IEntityTypeConfiguration<LeaveResumptionApproval>
{
    public void Configure(EntityTypeBuilder<LeaveResumptionApproval> builder)
    {
        builder.ToTable("LeaveResumptionApprovals", "leave");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ApprovalStage).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Decision).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Comments).HasMaxLength(2000);
        builder.Property(x => x.SignatureName).HasMaxLength(256);
        builder.HasOne<LeaveResumption>().WithMany().HasForeignKey(x => x.ResumptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Csir.Spme.Domain.Iam.User>().WithMany().HasForeignKey(x => x.ApproverUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ResumptionId, x.ApprovalStage, x.Sequence }).IsUnique();
    }
}

public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder) {
        builder.ToTable("Holidays", "leave");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ScopeType).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => x.HolidayDate);
        builder.HasIndex(x => new { x.ScopeType, x.InstituteId, x.HolidayDate, x.Name }).IsUnique();
    }
}

public class HolidayPeriodConfiguration : IEntityTypeConfiguration<HolidayPeriod>
{
    public void Configure(EntityTypeBuilder<HolidayPeriod> builder) {
        builder.ToTable("HolidayPeriods", "leave");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ScopeType).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.ScopeType, x.InstituteId, x.LeaveYear }).IsUnique();
    }
}

public class CompassionateLeaveTypeConfiguration : IEntityTypeConfiguration<CompassionateLeaveType>
{
    public void Configure(EntityTypeBuilder<CompassionateLeaveType> builder)
    {
        builder.ToTable("CompassionateLeaveTypes", "leave");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ScopeType).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Days).HasPrecision(7, 2);
        builder.HasIndex(x => new { x.ScopeType, x.InstituteId, x.Code }).IsUnique().HasFilter(null);
    }
}

public class SkeletalStaffRequestConfiguration : IEntityTypeConfiguration<SkeletalStaffRequest>
{
    public void Configure(EntityTypeBuilder<SkeletalStaffRequest> builder) {
        builder.ToTable("SkeletalStaffRequests", "leave");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SelectedDatesJson);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.CurrentApprovalStage).HasMaxLength(64);
        builder.Property(x => x.SignatureName).HasMaxLength(256);
        builder.Property(x => x.Comment).HasMaxLength(2000);
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);
        builder.HasIndex(x => new { x.EmployeeId, x.HolidayPeriodId }).IsUnique();
        builder.HasIndex(x => new { x.InstituteId, x.Status, x.CreatedAt });
        builder.HasOne<Csir.Spme.Domain.Hr.Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HolidayPeriod>().WithMany().HasForeignKey(x => x.HolidayPeriodId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SkeletalStaffApprovalConfiguration : IEntityTypeConfiguration<SkeletalStaffApproval>
{
    public void Configure(EntityTypeBuilder<SkeletalStaffApproval> builder)
    {
        builder.ToTable("SkeletalStaffApprovals", "leave");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ApprovalStage).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Decision).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Comments).HasMaxLength(2000);
        builder.HasOne<SkeletalStaffRequest>().WithMany().HasForeignKey(x => x.SkeletalStaffRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Csir.Spme.Domain.Iam.User>().WithMany().HasForeignKey(x => x.ApproverUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SkeletalStaffRequestId, x.ApprovalStage, x.Sequence }).IsUnique();
    }
}
