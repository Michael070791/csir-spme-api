using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Csir.Spme.Domain.Plan;

namespace Csir.Spme.Infrastructure.Persistence.Configurations.Plan;

public class StrategicPlanConfiguration : IEntityTypeConfiguration<StrategicPlan>
{
    public void Configure(EntityTypeBuilder<StrategicPlan> builder) {
        builder.ToTable("StrategicPlans", "plan", table =>
        {
            table.HasCheckConstraint(
                "CK_StrategicPlans_YearRange",
                "[EndYear] >= [StartYear]");
            table.HasCheckConstraint(
                "CK_StrategicPlans_Status",
                "[Status] IN ('draft', 'active', 'closed', 'archived')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Definition).IsRequired();
        builder.Property(x => x.Objective).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.InstituteId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.InstituteId, x.Status, x.StartYear, x.EndYear });
    }
}

public class ThrustConfiguration : IEntityTypeConfiguration<Thrust>
{
    public void Configure(EntityTypeBuilder<Thrust> builder) {
        builder.ToTable("Thrusts", "plan");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Objective).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.StrategicPlanId, x.Code }).IsUnique();
    }
}

public class OutputConfiguration : IEntityTypeConfiguration<Output>
{
    public void Configure(EntityTypeBuilder<Output> builder) {
        builder.ToTable("Outputs", "plan");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.ThrustId, x.Code }).IsUnique();
    }
}

public class IndicatorConfiguration : IEntityTypeConfiguration<Indicator>
{
    public void Configure(EntityTypeBuilder<Indicator> builder) {
        builder.ToTable("Indicators", "plan");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(128).IsRequired();
        builder.Property(x => x.VerificationMethod).HasMaxLength(2000);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.BaselineValue).HasPrecision(18, 4);
        builder.Property(x => x.TargetValue).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.OutputId, x.Code }).IsUnique();
    }
}

public class IndicatorMeasurementConfiguration : IEntityTypeConfiguration<IndicatorMeasurement>
{
    public void Configure(EntityTypeBuilder<IndicatorMeasurement> builder) {
        builder.ToTable("IndicatorMeasurements", "plan");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Remarks).HasMaxLength(2000);
        builder.Property(x => x.Value).HasColumnType("decimal(18,4)");
        builder.HasIndex(x => new { x.IndicatorId, x.ReportingPeriodId }).IsUnique();
    }
}
