using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Knowledge;
using Csir.Spme.Domain.Leave;
using Csir.Spme.Domain.Plan;
using Csir.Spme.Domain.Projects;
using Csir.Spme.Domain.Promotions;
using Csir.Spme.Domain.Reporting;
using Csir.Spme.Application.Reporting;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;

namespace Csir.Spme.Application.Common.Interfaces;

/// <summary>Sort/paging instructions for one keyset page.</summary>
public sealed record KeysetPage(string Sort, bool Descending, CursorPosition? After, int Limit);

public interface IReportingPeriodRepository
{
    Task<ReportingPeriod?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<bool> CodeExistsAsync(string scopeType, Guid? instituteId, string code, Guid? excludeId, CancellationToken ct);
    Task<ListSlice<ReportingPeriod>> ListAsync(
        Guid? instituteScope, string? periodType, string? status, KeysetPage page, CancellationToken ct);
    void Add(ReportingPeriod period);
}

public interface IReportRepository
{
    Task<Report?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<bool> DuplicateExistsAsync(Guid instituteId, Guid reportingPeriodId, string reportType, Guid? excludeId, CancellationToken ct);
    Task<bool> HasMetricsAsync(Guid reportId, CancellationToken ct);
    Task<ListSlice<Report>> ListAsync(
        Guid? instituteScope, string? reportType, string? status, Guid? reportingPeriodId, KeysetPage page, CancellationToken ct);
    void Add(Report report);
    void Remove(Report report);
}

public sealed record StaffQuarterlyReviewer(
    User User,
    Employee Employee,
    string Role);

public sealed record StaffQuarterlyReportAggregate(
    Report Report,
    ReportingPeriod Period,
    Employee Owner,
    StaffQuarterlyReviewer Reviewer,
    IReadOnlyList<ReportProject> ReportProjects,
    IReadOnlyList<Project> Projects,
    IReadOnlyList<ReportTechnology> ReportTechnologies,
    IReadOnlyList<Technology> Technologies,
    IReadOnlyList<ReportAttachment> Attachments,
    IReadOnlyList<FileRecord> AttachmentFiles);

public interface IStaffQuarterlyReportRepository
{
    Task<IApplicationTransaction> BeginSerializableTransactionAsync(CancellationToken ct);
    Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken ct);
    Task EnsureOpenCurrentYearQuartersAsync(Guid instituteId, int year, CancellationToken ct);
    Task<IReadOnlyList<ReportingPeriod>> ListOpenQuarterlyPeriodsAsync(Guid instituteId, CancellationToken ct);
    Task<IReadOnlyList<Project>> ListProjectOptionsAsync(Guid instituteId, CancellationToken ct);
    Task<IReadOnlyList<Technology>> ListTechnologyOptionsAsync(Guid instituteId, CancellationToken ct);
    Task<IReadOnlyList<StaffQuarterlyReviewer>> ListReviewerOptionsAsync(Guid employeeId, Guid instituteId, CancellationToken ct);
    Task<IReadOnlyList<StaffQuarterlyReviewer>> SearchStaffReviewerCandidatesAsync(
        Guid instituteId, Guid excludeEmployeeId, string? query, CancellationToken ct);
    Task<StaffQuarterlyReviewer?> FindInstituteStaffReviewerAsync(
        Guid instituteId, Guid excludeEmployeeId, Guid reviewerUserId, CancellationToken ct);
    Task<StaffQuarterlyReviewer?> FindEligibleReviewerAsync(Guid employeeId, Guid instituteId, Guid reviewerUserId, CancellationToken ct);
    Task<IReadOnlyList<StaffQuarterlyReportAggregate>> ListMineAsync(Guid employeeId, CancellationToken ct);
    Task<IReadOnlyList<StaffQuarterlyReportAggregate>> ListForReviewerAsync(Guid reviewerUserId, Guid instituteId, CancellationToken ct);
    Task<StaffQuarterlyReportAggregate?> FindAggregateAsync(Guid reportId, CancellationToken ct);
    Task<bool> StaffReportExistsAsync(Guid employeeId, Guid reportingPeriodId, Guid? excludeId, CancellationToken ct);
    Task<IReadOnlyList<Project>> FindProjectsAsync(Guid instituteId, IReadOnlyCollection<Guid> projectIds, CancellationToken ct);
    Task<IReadOnlyList<Technology>> FindTechnologiesAsync(Guid instituteId, IReadOnlyCollection<Guid> technologyIds, CancellationToken ct);
    Task<Project?> FindProjectByCodeOrNameAsync(Guid instituteId, string code, string name, CancellationToken ct);
    Task<Project?> FindProjectByIdAsync(Guid instituteId, Guid projectId, CancellationToken ct);
    Task<Project?> FindProjectForUpdateAsync(Guid instituteId, Guid projectId, CancellationToken ct);
    Task<Technology?> FindTechnologyByCodeOrNameAsync(Guid instituteId, string code, string name, CancellationToken ct);
    Task<ProjectInception?> FindProjectInceptionAsync(Guid projectId, CancellationToken ct);
    Task<ProjectInception?> FindProjectInceptionForUpdateAsync(Guid projectId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, ProjectInception>> FindProjectInceptionsAsync(
        IReadOnlyCollection<Guid> projectIds, CancellationToken ct);
    Task<FileRecord?> FindFileRecordAsync(Guid fileId, CancellationToken ct);
    Task<FileRecord?> FindFileRecordForUpdateAsync(Guid fileId, CancellationToken ct);
    Task<IReadOnlyList<FileRecord>> FindFileRecordsAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken ct);
    Task<StaffQuarterlyReportUploadSession?> FindUploadSessionAsync(Guid sessionId, CancellationToken ct);
    Task<int> CountReportImagesAsync(Guid reportId, CancellationToken ct);
    Task<bool> CanReadProjectAsync(Guid projectId, Guid employeeId, Guid? reviewerUserId, CancellationToken ct);
    void Add(Report report);
    void Add(Project project);
    void Add(ProjectInception inception);
    void Add(Technology technology);
    void Add(ReportAttachment attachment);
    void Add(StaffQuarterlyReportUploadSession session);
    void Add(FileRecord file);
    void ReplaceProjects(Guid reportId, IReadOnlyCollection<SaveStaffQuarterlyProjectProgressCommand> projectProgress);
    void ReplaceTechnologies(Guid reportId, IReadOnlyCollection<Guid> technologyIds);
    void RemoveAttachment(ReportAttachment attachment);
}

public interface IApplicationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}

public sealed record PromotionReportAggregate(
    PromotionSubmission Submission,
    PromotionSubmissionRequirementSnapshot Requirement,
    PromotionSubmissionReport Report);

public interface IPromotionReportRepository
{
    Task<PromotionReportAggregate?> FindAsync(
        Guid promotionSubmissionId,
        string reportType,
        CancellationToken ct);
}

public interface ITechnologyRepository
{
    Task<Technology?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<bool> CodeExistsAsync(Guid instituteId, string code, Guid? excludeId, CancellationToken ct);
    Task<bool> HasReferencesAsync(Guid technologyId, CancellationToken ct);
    Task<ListSlice<Technology>> ListAsync(
        Guid? instituteScope, string? status, string? technologyType, KeysetPage page, CancellationToken ct);
    void Add(Technology technology);
    void Remove(Technology technology);
}

public interface IProjectRepository
{
    Task<Project?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<bool> CodeExistsAsync(Guid instituteId, string code, Guid? excludeId, CancellationToken ct);
    Task<bool> HasDependenciesAsync(Guid projectId, CancellationToken ct);
    Task<Guid?> GetThrustInstituteAsync(Guid thrustId, CancellationToken ct);
    Task<ListSlice<Project>> ListAsync(
        Guid? instituteScope, string? status, string? nature, Guid? leadEmployeeId, Guid? thrustId, KeysetPage page, CancellationToken ct);
    void Add(Project project);
    void Remove(Project project);
}

public interface ILeaveRequestRepository
{
    Task<LeaveRequest?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<ListSlice<LeaveRequest>> ListAsync(
        Guid? instituteScope, Guid? employeeId, string? status, string? leaveType, KeysetPage page, CancellationToken ct);
    Task<bool> HasOverlappingActiveRequestAsync(Guid employeeId, DateTime startDate, DateTime endDate, Guid? excludeId, CancellationToken ct);
    Task<LeaveBalance?> FindBalanceAsync(Guid employeeId, string leaveType, short leaveYear, CancellationToken ct);
    Task<LeavePolicy?> FindApplicablePolicyAsync(Guid instituteId, string leaveType, Guid? positionTypeId, DateTime onDate, CancellationToken ct);
    Task<IReadOnlyList<DateTime>> GetHolidayDatesAsync(Guid instituteId, DateTime startDate, DateTime endDate, CancellationToken ct);
    Task<short> NextApprovalSequenceAsync(Guid leaveRequestId, string approvalStage, CancellationToken ct);
    Task<LeaveResumption?> FindResumptionByRequestAsync(Guid leaveRequestId, CancellationToken ct);
    Task<IReadOnlyList<LeaveBalance>> ListBalancesAsync(Guid employeeId, short leaveYear, CancellationToken ct);
    Task<IReadOnlyList<LeaveBalance>> ListTrackedBalancesAsync(
        IReadOnlyCollection<Guid> employeeIds, string leaveType, short leaveYear, CancellationToken ct);
    Task<IReadOnlyList<LeaveDelegateOption>> ListDelegateOptionsAsync(
        Guid instituteId,
        Guid excludeEmployeeId,
        Guid? sectionId,
        Guid? divisionId,
        CancellationToken ct);
    Task<IReadOnlyList<LeaveDelegateDivisionOption>> ListDelegateDivisionsAsync(
        Guid instituteId, Guid? excludeDivisionId, CancellationToken ct);
    Task<LeaveApprovalScope?> GetApprovalScopeAsync(Guid employeeId, CancellationToken ct);
    void Add(LeaveRequest request);
    void AddApproval(LeaveRequestApproval approval);
    void AddBalance(LeaveBalance balance);
    void AddResumption(LeaveResumption resumption);
    void AddResumptionApproval(LeaveResumptionApproval approval);
}

public sealed record LeaveApprovalScope(Guid EmployeeId, Guid InstituteId, Guid? DivisionId, Guid? SectionId);
public sealed record LeaveDelegateOption(Guid EmployeeId, string StaffId, string DisplayName, string? JobTitle);
public sealed record LeaveDelegateDivisionOption(Guid Id, string Name);

public interface IStrategicPlanRepository
{
    Task<StrategicPlan?> FindByIdAsync(Guid id, Guid? instituteScope, CancellationToken ct);
    Task<bool> CodeExistsAsync(Guid instituteId, string code, Guid? excludeId, CancellationToken ct);
    Task<bool> HasOverlappingActiveAsync(
        Guid instituteId, short startYear, short endYear, Guid? excludeId, CancellationToken ct);
    Task<ListSlice<StrategicPlan>> ListAsync(Guid? instituteScope, string? status, KeysetPage page, CancellationToken ct);
    void Add(StrategicPlan plan);
}

public interface IThrustRepository
{
    Task<Thrust?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<bool> CodeExistsAsync(Guid strategicPlanId, string code, Guid? excludeId, CancellationToken ct);
    Task<ListSlice<Thrust>> ListAsync(
        Guid? instituteScope, Guid? strategicPlanId, string? status, KeysetPage page, CancellationToken ct);
    void Add(Thrust thrust);
}

public interface IOutputRepository
{
    Task<Output?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<Guid?> GetInstituteIdAsync(Guid outputId, CancellationToken ct);
    Task<bool> CodeExistsAsync(Guid thrustId, string code, Guid? excludeId, CancellationToken ct);
    Task<ListSlice<Output>> ListAsync(Guid? instituteScope, Guid? thrustId, string? status, KeysetPage page, CancellationToken ct);
    void Add(Output output);
}

public interface IIndicatorRepository
{
    Task<Indicator?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<Guid?> GetInstituteIdAsync(Guid indicatorId, CancellationToken ct);
    Task<Guid?> GetThrustIdAsync(Guid indicatorId, CancellationToken ct);
    Task<bool> CodeExistsAsync(Guid outputId, string code, Guid? excludeId, CancellationToken ct);
    Task<ListSlice<Indicator>> ListByOutputAsync(Guid? instituteScope, Guid outputId, string? status, KeysetPage page, CancellationToken ct);
    Task<ListSlice<Indicator>> ListByThrustAsync(Guid thrustId, string? status, KeysetPage page, CancellationToken ct);
    void Add(Indicator indicator);
}

public interface IIndicatorMeasurementRepository
{
    Task<IndicatorMeasurement?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<Guid?> GetInstituteIdAsync(Guid measurementId, CancellationToken ct);
    Task<Guid?> GetIndicatorInstituteIdAsync(Guid indicatorId, CancellationToken ct);
    Task<ReportingPeriod?> GetReportingPeriodAsync(Guid reportingPeriodId, CancellationToken ct);
    Task<Indicator?> GetIndicatorAsync(Guid indicatorId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid indicatorId, Guid reportingPeriodId, Guid? excludeId, CancellationToken ct);
    Task<ListSlice<IndicatorMeasurement>> ListByIndicatorAsync(Guid indicatorId, KeysetPage page, CancellationToken ct);
    void Add(IndicatorMeasurement measurement);
    void Remove(IndicatorMeasurement measurement);
}
