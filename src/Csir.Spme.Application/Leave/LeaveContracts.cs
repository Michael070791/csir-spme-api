namespace Csir.Spme.Application.Leave;

public sealed record LeaveRequestDto(
    Guid Id, Guid EmployeeId, Guid InstituteId, string LeaveType, DateTime StartDate, DateTime EndDate,
    decimal WorkingDays, string Status, string CurrentApprovalStage, string? Reason,
    string? HandoverNotes, Guid? DelegateEmployeeId, DateTimeOffset? SubmittedAt,
    DateTimeOffset? CompletedAt, DateTimeOffset? CancelledAt, string? RejectionReason,
    Guid? MedicalDocumentFileId, Guid? AdmissionLetterFileId, Guid? HandoverDocumentFileId,
    IReadOnlyList<string> AvailableActions,
    string Etag, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateLeaveRequestCommand(
    Guid? EmployeeId, string LeaveType, DateTime StartDate, DateTime EndDate, string? Reason,
    string? HandoverNotes, Guid? DelegateEmployeeId, Guid? MedicalDocumentFileId,
    Guid? AdmissionLetterFileId, Guid? HandoverDocumentFileId);

public sealed record UpdateLeaveRequestCommand(
    string LeaveType, DateTime StartDate, DateTime EndDate, string? Reason, string? HandoverNotes,
    Guid? DelegateEmployeeId, Guid? MedicalDocumentFileId, Guid? AdmissionLetterFileId,
    Guid? HandoverDocumentFileId);

public sealed record LeaveDecisionCommand(string? Comments, string? SignatureName);

public sealed record ResumeLeaveCommand(DateTime? ResumptionDate, string? EmployeeSignatureName);

public sealed record WorkingDaysCalculationDto(
    decimal WorkingDays,
    DateTime StartDate,
    DateTime EndDate,
    DateTime ExpectedReturnDate);

public sealed record LeaveBalanceDto(
    Guid EmployeeId, string LeaveType, short LeaveYear, decimal TotalDays, decimal UsedDays,
    decimal PendingDays, decimal AdjustedDays, decimal RemainingDays);

public sealed record AssignAnnualLeaveCommand(Guid EmployeeId, decimal TotalDays, short? LeaveYear);

public sealed record BulkAssignAnnualLeaveCommand(
    IReadOnlyList<Guid> EmployeeIds,
    decimal TotalDays,
    short? LeaveYear,
    string? StaffCategory);

public sealed record BulkAssignAnnualLeaveItem(
    Guid EmployeeId,
    string Outcome,
    string? Code,
    string? Message,
    LeaveBalanceDto? Balance);

public sealed record BulkAssignAnnualLeaveResult(
    int Assigned,
    int Skipped,
    int Failed,
    IReadOnlyList<BulkAssignAnnualLeaveItem> Results);

public sealed record LeaveDelegateOptionDto(
    Guid EmployeeId, string StaffId, string DisplayName, string? JobTitle);

public sealed record LeaveDelegateDivisionDto(Guid Id, string Name);

public sealed record LeaveDelegateOptionsDto(
    IReadOnlyList<LeaveDelegateOptionDto> Delegates,
    string ScopeMode,
    Guid? DivisionId,
    Guid? SectionId,
    bool PreferAlternateDivision,
    IReadOnlyList<LeaveDelegateDivisionDto> AlternateDivisions);
