using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Leave;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Application.Leave;

public sealed class LeaveRequestService
{
    private static readonly string[] AllowedSorts = ["startDate", "endDate", "leaveType", "status"];

    private readonly ILeaveRequestRepository _leave;
    private readonly IInstituteDirectory _institutes;
    private readonly IApplicationDbContext _unitOfWork;
    private readonly IAuditService _audit;
    private readonly IWorkflowNotificationOutbox _notifications;
    private readonly ICurrentUserService _currentUser;
    private readonly ICursorCodec _cursorCodec;
    private readonly PaginationOptions _pagination;

    public LeaveRequestService(
        ILeaveRequestRepository leave,
        IInstituteDirectory institutes,
        IApplicationDbContext unitOfWork,
        IAuditService audit,
        IWorkflowNotificationOutbox notifications,
        ICurrentUserService currentUser,
        ICursorCodec cursorCodec,
        IOptions<PaginationOptions> pagination)
    {
        _leave = leave;
        _institutes = institutes;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _notifications = notifications;
        _currentUser = currentUser;
        _cursorCodec = cursorCodec;
        _pagination = pagination.Value;
    }

    public async Task<Result<ListSlice<LeaveRequestDto>>> ListAsync(
        Guid? employeeId, string? status, string? leaveType,
        int? limit, string? cursor, string? sort, string? direction, CancellationToken ct)
    {
        var fields = new Dictionary<string, string[]>();
        if (!string.IsNullOrWhiteSpace(status) && !DomainValues.Contains(LeaveRequestStatuses.All, status.Trim()))
        {
            fields["filter[status]"] = [$"Status must be one of: {string.Join(", ", LeaveRequestStatuses.All)}."];
        }

        if (!string.IsNullOrWhiteSpace(leaveType) && !DomainValues.Contains(LeaveTypes.All, leaveType.Trim()))
        {
            fields["filter[leaveType]"] = [$"Leave type must be one of: {string.Join(", ", LeaveTypes.All)}."];
        }

        if (fields.Count > 0)
        {
            return Result<ListSlice<LeaveRequestDto>>.Failure(Error.Validation(fields));
        }

        // Employee self-service is always restricted to the linked employee, even though the
        // canonical Employee role carries leave.read for access to its own leave resources.
        Guid? effectiveEmployeeId = employeeId;
        if (!CanReadInstituteLeave())
        {
            if (_currentUser.EmployeeId is null)
            {
                return Result<ListSlice<LeaveRequestDto>>.Failure(Error.Forbidden(
                    "You are not authorized to list leave requests."));
            }

            if (employeeId.HasValue && employeeId.Value != _currentUser.EmployeeId.Value)
            {
                return Result<ListSlice<LeaveRequestDto>>.Failure(Error.NotFound("Employee not found."));
            }

            effectiveEmployeeId = _currentUser.EmployeeId.Value;
        }
        else if (!_currentUser.InstituteId.HasValue && !CanListLeaveAcrossInstitutes())
        {
            return Result<ListSlice<LeaveRequestDto>>.Failure(Error.Forbidden(
                "An institute assignment is required."));
        }

        var page = ListQueryParser.Parse(_cursorCodec, _pagination.DefaultLimit, _pagination.MaxLimit,
            limit, cursor, sort, direction, "startDate", true, AllowedSorts);
        if (page.IsFailure)
        {
            return Result<ListSlice<LeaveRequestDto>>.Failure(page.Error!);
        }

        // PlatformAdmin / HrAdmin with no institute claim see CSIR-wide leave.
        // All other institute readers are bound to their claim.
        var instituteScope = CanListLeaveAcrossInstitutes() && !_currentUser.InstituteId.HasValue
            ? null
            : _currentUser.InstituteId;

        var slice = await _leave.ListAsync(instituteScope, effectiveEmployeeId,
            status?.Trim(), leaveType?.Trim(), page.Value!, ct);
        return Result<ListSlice<LeaveRequestDto>>.Success(await MapAsync(slice, ct));
    }

    public string? EncodeCursor(CursorPosition? cursor) => cursor is null ? null : _cursorCodec.Encode(cursor);

    public async Task<Result<LeaveRequestDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var request = await _leave.FindByIdAsync(id, ct);
        if (!IsAccessible(request))
        {
            return Result<LeaveRequestDto>.Failure(Error.NotFound("Leave request not found."));
        }

        return Result<LeaveRequestDto>.Success(await MapAsync(request!, ct));
    }

    public async Task<Result<LeaveRequestDto>> CreateAsync(CreateLeaveRequestCommand command, CancellationToken ct)
    {
        var target = await ResolveTargetEmployeeAsync(command.EmployeeId, ct);
        if (target.IsFailure)
        {
            return Result<LeaveRequestDto>.Failure(target.Error!);
        }

        var employee = target.Value!;
        var prepared = await PrepareDraftAsync(employee, command.LeaveType, command.StartDate, command.EndDate,
            command.DelegateEmployeeId, command.MedicalDocumentFileId, command.AdmissionLetterFileId,
            command.HandoverDocumentFileId, ct);
        if (prepared.IsFailure)
        {
            return Result<LeaveRequestDto>.Failure(prepared.Error!);
        }

        var request = LeaveRequest.CreateDraft(employee.EmployeeId, employee.InstituteId,
            Normalize(command.LeaveType), command.StartDate, command.EndDate, prepared.Value!,
            command.Reason, command.HandoverNotes, command.DelegateEmployeeId,
            command.MedicalDocumentFileId, command.AdmissionLetterFileId, command.HandoverDocumentFileId);
        _leave.Add(request);
        await _audit.RecordAsync("leave-request.created", "LeaveRequest", request.Id.ToString(), null,
            $"type={request.LeaveType};start={request.StartDate:yyyy-MM-dd};end={request.EndDate:yyyy-MM-dd}", ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<LeaveRequestDto>.Success(await MapAsync(request, ct));
    }

    public async Task<Result<WorkingDaysCalculationDto>> CalculateWorkingDaysAsync(
        string leaveType, DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        var target = await ResolveTargetEmployeeAsync(null, ct);
        if (target.IsFailure)
            return Result<WorkingDaysCalculationDto>.Failure(target.Error!);

        var prepared = await PrepareDraftAsync(target.Value!, leaveType, startDate, endDate,
            null, null, null, null, ct);
        if (prepared.IsFailure)
            return Result<WorkingDaysCalculationDto>.Failure(prepared.Error!);

        // Look ahead so public holidays immediately after the leave end are not treated as return days.
        var holidayWindowEnd = endDate.Date.AddDays(21);
        var holidays = await _leave.GetHolidayDatesAsync(
            target.Value!.InstituteId, startDate.Date, holidayWindowEnd, ct);
        var expectedReturn = WorkingDaysCalculator.ExpectedReturnDate(endDate, holidays.ToHashSet());
        return Result<WorkingDaysCalculationDto>.Success(new(
            prepared.Value!,
            startDate.Date,
            endDate.Date,
            expectedReturn));
    }

    public async Task<Result<LeaveRequestDto>> UpdateAsync(Guid id, UpdateLeaveRequestCommand command, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var request = await _leave.FindByIdAsync(id, ct);
        if (!IsAccessible(request))
        {
            return Result<LeaveRequestDto>.Failure(Error.NotFound("Leave request not found."));
        }

        if (!IsOwnerOrManager(request!))
        {
            return Result<LeaveRequestDto>.Failure(Error.Forbidden(
                "You are not authorized to edit this leave request."));
        }

        if (expectedRowVersion is null)
        {
            return Result<LeaveRequestDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));
        }

        var employee = (await _institutes.GetEmployeeScopeAsync(request!.EmployeeId, ct))!;
        var prepared = await PrepareDraftAsync(employee, command.LeaveType, command.StartDate, command.EndDate,
            command.DelegateEmployeeId, command.MedicalDocumentFileId, command.AdmissionLetterFileId,
            command.HandoverDocumentFileId, ct);
        if (prepared.IsFailure)
        {
            return Result<LeaveRequestDto>.Failure(prepared.Error!);
        }

        var updated = request.UpdateDraft(Normalize(command.LeaveType), command.StartDate, command.EndDate,
            prepared.Value!, command.Reason, command.HandoverNotes, command.DelegateEmployeeId,
            command.MedicalDocumentFileId, command.AdmissionLetterFileId, command.HandoverDocumentFileId);
        if (updated.IsFailure)
        {
            return Result<LeaveRequestDto>.Failure(updated.Error!);
        }

        _unitOfWork.SetOriginalRowVersion(request, expectedRowVersion);
        var auditError = await RecordAuditAsync("leave-request.updated", request, null,
            $"type={request.LeaveType};start={request.StartDate:yyyy-MM-dd};end={request.EndDate:yyyy-MM-dd}", ct);
        if (auditError is not null) return Result<LeaveRequestDto>.Failure(auditError);
        return await SaveAsync(request, ct);
    }

    public async Task<Result<LeaveRequestDto>> SubmitAsync(Guid id, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var request = await _leave.FindByIdAsync(id, ct);
        if (!IsAccessible(request))
        {
            return Result<LeaveRequestDto>.Failure(Error.NotFound("Leave request not found."));
        }

        if (!IsOwnerOrManager(request!))
        {
            return Result<LeaveRequestDto>.Failure(Error.Forbidden(
                "You are not authorized to submit this leave request."));
        }

        if (expectedRowVersion is null)
            return Result<LeaveRequestDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));

        if (await _leave.HasOverlappingActiveRequestAsync(request!.EmployeeId, request.StartDate, request.EndDate, request.Id, ct))
        {
            return Result<LeaveRequestDto>.Failure(Error.Conflict(
                "The leave request overlaps an existing active leave request."));
        }

        var submitted = request.Submit(LeaveApprovalStages.DefaultChain[0], DateTimeOffset.UtcNow);
        if (submitted.IsFailure)
        {
            return Result<LeaveRequestDto>.Failure(submitted.Error!);
        }

        // Reserve the balance in the same transaction as the submission.
        var balance = await _leave.FindBalanceAsync(request.EmployeeId, request.LeaveType, (short)request.StartDate.Year, ct);
        if (balance is null)
        {
            return Result<LeaveRequestDto>.Failure(Error.InsufficientLeaveBalance(
                "No leave balance is configured for this employee, leave type, and year."));
        }

        var reserved = balance.Reserve(request.WorkingDays);
        if (reserved.IsFailure)
        {
            return Result<LeaveRequestDto>.Failure(reserved.Error!);
        }

        _unitOfWork.SetOriginalRowVersion(request, expectedRowVersion);
        var auditError = await RecordAuditAsync("leave-request.submitted", request,
            "status=draft", $"status={request.Status};stage={request.CurrentApprovalStage}", ct);
        if (auditError is not null) return Result<LeaveRequestDto>.Failure(auditError);
        if (!string.IsNullOrWhiteSpace(request.CurrentApprovalStage))
            await _notifications.StageLeaveAwaitingApprovalAsync(
                request.Id,
                request.InstituteId,
                request.EmployeeId,
                request.CurrentApprovalStage,
                request.LeaveType,
                request.StartDate,
                request.EndDate,
                request.WorkingDays,
                ct);
        return await SaveAsync(request, ct);
    }

    public async Task<Result<LeaveRequestDto>> ApproveAsync(
        Guid id, LeaveDecisionCommand command, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var request = await _leave.FindByIdAsync(id, ct);
        if (!IsAccessible(request))
        {
            return Result<LeaveRequestDto>.Failure(Error.NotFound("Leave request not found."));
        }

        if (!_currentUser.HasPermission(SpmePermissions.LeaveApprove))
            return Result<LeaveRequestDto>.Failure(Error.Forbidden("You are not authorized to approve leave requests."));
        if (expectedRowVersion is null)
            return Result<LeaveRequestDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));

        if (request!.Status == LeaveRequestStatuses.ResumptionPending)
            return await CompleteResumptionAsync(request, expectedRowVersion, ct);

        var chain = LeaveApprovalStages.DefaultChain;
        var stageIndex = Array.IndexOf(chain, request!.CurrentApprovalStage);
        if (stageIndex < 0)
        {
            return Result<LeaveRequestDto>.Failure(Error.StateTransition(
                $"The leave request is not awaiting an approval decision."));
        }

        if (!await CanApproveStageAsync(chain[stageIndex], request.EmployeeId, ct))
            return Result<LeaveRequestDto>.Failure(Error.Forbidden(
                "You are not authorized to act at the current approval stage."));

        var nextStage = stageIndex + 1 < chain.Length ? chain[stageIndex + 1] : null;
        var approved = request.Approve(chain[stageIndex], nextStage);
        if (approved.IsFailure)
        {
            return Result<LeaveRequestDto>.Failure(approved.Error!);
        }

        var sequence = await _leave.NextApprovalSequenceAsync(request.Id, chain[stageIndex], ct);
        _leave.AddApproval(LeaveRequestApproval.Create(request.Id, _currentUser.UserId ?? Guid.Empty,
            chain[stageIndex], ApprovalDecisions.Approved, command.Comments, command.SignatureName, sequence));

        if (nextStage is null)
        {
            var balance = await _leave.FindBalanceAsync(request.EmployeeId, request.LeaveType, (short)request.StartDate.Year, ct);
            if (balance is null)
            {
                return Result<LeaveRequestDto>.Failure(Error.InsufficientLeaveBalance(
                    "The reserved leave balance could not be found."));
            }

            var consumed = balance.Consume(request.WorkingDays);
            if (consumed.IsFailure)
            {
                return Result<LeaveRequestDto>.Failure(consumed.Error!);
            }
        }

        _unitOfWork.SetOriginalRowVersion(request, expectedRowVersion);
        var auditError = await RecordAuditAsync("leave-request.approved", request,
            $"stage={chain[stageIndex]}", ApprovalAuditState(request.Status), ct);
        if (auditError is not null) return Result<LeaveRequestDto>.Failure(auditError);
        if (nextStage is null)
            await _notifications.StageLeaveDecisionAsync(
                request.Id,
                request.InstituteId,
                request.EmployeeId,
                _currentUser.UserId ?? Guid.Empty,
                "approved",
                ct);
        else
            await _notifications.StageLeaveAwaitingApprovalAsync(
                request.Id,
                request.InstituteId,
                request.EmployeeId,
                nextStage,
                request.LeaveType,
                request.StartDate,
                request.EndDate,
                request.WorkingDays,
                ct);
        return await SaveAsync(request, ct);
    }

    public async Task<Result<LeaveRequestDto>> RejectAsync(
        Guid id, LeaveDecisionCommand command, string? rejectionReason, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var request = await _leave.FindByIdAsync(id, ct);
        if (!IsAccessible(request))
        {
            return Result<LeaveRequestDto>.Failure(Error.NotFound("Leave request not found."));
        }

        if (!_currentUser.HasPermission(SpmePermissions.LeaveApprove))
            return Result<LeaveRequestDto>.Failure(Error.Forbidden("You are not authorized to reject leave requests."));
        if (expectedRowVersion is null)
            return Result<LeaveRequestDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));

        if (!await CanApproveStageAsync(request!.CurrentApprovalStage, request.EmployeeId, ct))
            return Result<LeaveRequestDto>.Failure(Error.Forbidden(
                "You are not authorized to act at the current approval stage."));

        var rejected = request.Reject(request.CurrentApprovalStage, rejectionReason ?? string.Empty);
        if (rejected.IsFailure)
        {
            return Result<LeaveRequestDto>.Failure(rejected.Error!);
        }

        var sequence = await _leave.NextApprovalSequenceAsync(request.Id, request.CurrentApprovalStage, ct);
        _leave.AddApproval(LeaveRequestApproval.Create(request.Id, _currentUser.UserId ?? Guid.Empty,
            request.CurrentApprovalStage, ApprovalDecisions.Rejected, command.Comments ?? rejectionReason,
            command.SignatureName, sequence));

        var balance = await _leave.FindBalanceAsync(request.EmployeeId, request.LeaveType, (short)request.StartDate.Year, ct);
        if (balance is not null && balance.PendingDays >= request.WorkingDays)
        {
            balance.Release(request.WorkingDays);
        }

        _unitOfWork.SetOriginalRowVersion(request, expectedRowVersion);
        var auditError = await RecordAuditAsync("leave-request.rejected", request, null,
            ApprovalAuditState(request.Status), ct);
        if (auditError is not null) return Result<LeaveRequestDto>.Failure(auditError);
        await _notifications.StageLeaveDecisionAsync(
            request.Id,
            request.InstituteId,
            request.EmployeeId,
            _currentUser.UserId ?? Guid.Empty,
            "rejected",
            ct);
        return await SaveAsync(request, ct);
    }

    public async Task<Result<LeaveRequestDto>> CancelAsync(Guid id, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var request = await _leave.FindByIdAsync(id, ct);
        if (!IsAccessible(request))
        {
            return Result<LeaveRequestDto>.Failure(Error.NotFound("Leave request not found."));
        }

        if (!IsOwnerOrManager(request!))
        {
            return Result<LeaveRequestDto>.Failure(Error.Forbidden(
                "You are not authorized to cancel this leave request."));
        }

        if (expectedRowVersion is null)
            return Result<LeaveRequestDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));

        var wasApproved = request!.Status == LeaveRequestStatuses.Approved;
        var wasPending = request.Status is LeaveRequestStatuses.Submitted or LeaveRequestStatuses.UnderReview;
        var cancelled = request.Cancel(DateTimeOffset.UtcNow);
        if (cancelled.IsFailure)
        {
            return Result<LeaveRequestDto>.Failure(cancelled.Error!);
        }

        var balance = await _leave.FindBalanceAsync(request.EmployeeId, request.LeaveType, (short)request.StartDate.Year, ct);
        if (balance is not null)
        {
            if (wasPending && balance.PendingDays >= request.WorkingDays)
            {
                balance.Release(request.WorkingDays);
            }
            else if (wasApproved && balance.UsedDays >= request.WorkingDays)
            {
                balance.Credit(request.WorkingDays);
            }
        }

        _unitOfWork.SetOriginalRowVersion(request, expectedRowVersion);
        var auditError = await RecordAuditAsync("leave-request.cancelled", request, null,
            $"status={request.Status}", ct);
        if (auditError is not null) return Result<LeaveRequestDto>.Failure(auditError);
        await _notifications.StageLeaveOwnerNoticeAsync(
            request.Id, request.InstituteId, request.EmployeeId, "cancelled",
            "Leave request cancelled",
            $"Your {request.LeaveType} leave request was cancelled.",
            ct);
        return await SaveAsync(request, ct);
    }

    public async Task<Result<LeaveRequestDto>> ResumeAsync(
        Guid id, ResumeLeaveCommand command, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var request = await _leave.FindByIdAsync(id, ct);
        if (!IsAccessible(request))
        {
            return Result<LeaveRequestDto>.Failure(Error.NotFound("Leave request not found."));
        }

        if (expectedRowVersion is null)
            return Result<LeaveRequestDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));

        if (request!.Status == LeaveRequestStatuses.ResumptionPending)
        {
            return await CompleteResumptionAsync(request, expectedRowVersion, ct);
        }

        if (!IsOwnerOrManager(request))
        {
            return Result<LeaveRequestDto>.Failure(Error.Forbidden(
                "You are not authorized to resume this leave request."));
        }

        if (DateTime.UtcNow.Date <= request.EndDate.Date)
        {
            return Result<LeaveRequestDto>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["resumptionDate"] = ["Resumption is available after the leave end date has elapsed."]
            }));
        }

        if (await _leave.FindResumptionByRequestAsync(request.Id, ct) is not null)
        {
            return Result<LeaveRequestDto>.Failure(Error.Conflict(
                "A resumption already exists for this leave request."));
        }

        var resumptionDate = command.ResumptionDate ?? DateTime.UtcNow.Date;
        var resumption = LeaveResumption.Create(request.Id, request.EmployeeId, resumptionDate,
            request.EndDate, command.EmployeeSignatureName);
        if (resumption.IsFailure)
        {
            return Result<LeaveRequestDto>.Failure(resumption.Error!);
        }

        var begun = request.BeginResumption();
        if (begun.IsFailure)
        {
            return Result<LeaveRequestDto>.Failure(begun.Error!);
        }

        _leave.AddResumption(resumption.Value!);
        _unitOfWork.SetOriginalRowVersion(request, expectedRowVersion);
        var auditError = await RecordAuditAsync("leave-request.resumption-submitted", request,
            "status=approved", $"status={request.Status}", ct);
        if (auditError is not null) return Result<LeaveRequestDto>.Failure(auditError);
        await _notifications.StageLeaveOwnerNoticeAsync(
            request.Id, request.InstituteId, request.EmployeeId, "resumption-submitted",
            "Leave resumption submitted",
            $"Your {request.LeaveType} leave resumption was submitted for review.",
            ct);
        await _notifications.StageLeaveAwaitingApprovalAsync(
            request.Id,
            request.InstituteId,
            request.EmployeeId,
            LeaveApprovalStages.SectionHead,
            request.LeaveType,
            request.StartDate,
            request.EndDate,
            request.WorkingDays,
            ct);
        return await SaveAsync(request, ct);
    }

    private async Task<Result<LeaveRequestDto>> CompleteResumptionAsync(
        LeaveRequest request, byte[] expectedRowVersion, CancellationToken ct)
    {
        if (!_currentUser.HasPermission(SpmePermissions.LeaveApprove))
        {
            return Result<LeaveRequestDto>.Failure(Error.Forbidden(
                "Only a leave approver can complete a resumption."));
        }

        if (!await CanApproveStageAsync(LeaveApprovalStages.SectionHead, request.EmployeeId, ct))
        {
            return Result<LeaveRequestDto>.Failure(Error.Forbidden(
                "You are not authorized to approve this resumption stage."));
        }

        var resumption = await _leave.FindResumptionByRequestAsync(request.Id, ct);
        if (resumption is null)
        {
            return Result<LeaveRequestDto>.Failure(Error.Conflict(
                "No resumption exists for this leave request."));
        }

        var approved = resumption.Approve(DateTimeOffset.UtcNow);
        if (approved.IsFailure)
        {
            return Result<LeaveRequestDto>.Failure(approved.Error!);
        }

        _leave.AddResumptionApproval(LeaveResumptionApproval.Create(resumption.Id,
            _currentUser.UserId ?? Guid.Empty, LeaveApprovalStages.SectionHead, ApprovalDecisions.Approved,
            null, null, 1));

        var completed = request.CompleteResumption(DateTimeOffset.UtcNow);
        if (completed.IsFailure)
        {
            return Result<LeaveRequestDto>.Failure(completed.Error!);
        }

        _unitOfWork.SetOriginalRowVersion(request, expectedRowVersion);
        var auditError = await RecordAuditAsync("leave-request.resumed", request,
            "status=resumption-pending", ApprovalAuditState(request.Status), ct);
        if (auditError is not null) return Result<LeaveRequestDto>.Failure(auditError);
        await _notifications.StageLeaveOwnerNoticeAsync(
            request.Id, request.InstituteId, request.EmployeeId, "resumed",
            "Leave resumption approved",
            $"Your {request.LeaveType} leave resumption was approved.",
            ct);
        return await SaveAsync(request, ct);
    }

    public async Task<Result<IReadOnlyList<LeaveBalanceDto>>> ListBalancesAsync(Guid employeeId, short leaveYear, CancellationToken ct)
    {
        var employee = await _institutes.GetEmployeeScopeAsync(employeeId, ct);
        if (employee is null || !InstituteScope.Resolve(_currentUser.InstituteId, null).Value!.CanAccess(employee.InstituteId))
        {
            return Result<IReadOnlyList<LeaveBalanceDto>>.Failure(Error.NotFound("Employee not found."));
        }

        if (!CanReadInstituteLeave() && _currentUser.EmployeeId != employeeId)
        {
            return Result<IReadOnlyList<LeaveBalanceDto>>.Failure(Error.NotFound("Employee not found."));
        }

        var balances = await _leave.ListBalancesAsync(employeeId, leaveYear, ct);
        return Result<IReadOnlyList<LeaveBalanceDto>>.Success(balances.Select(MapBalance).ToList());
    }

    public async Task<Result<LeaveBalanceDto>> AssignAnnualEntitlementAsync(
        AssignAnnualLeaveCommand command,
        CancellationToken ct)
    {
        if (!CanAssignAnnualLeave())
        {
            return Result<LeaveBalanceDto>.Failure(Error.Forbidden(
                "You are not authorized to assign annual leave days."));
        }

        var validation = ValidateAssignmentInput(command.TotalDays, command.LeaveYear, staffCategory: null);
        if (validation.IsFailure)
            return Result<LeaveBalanceDto>.Failure(validation.Error!);

        var leaveYear = command.LeaveYear ?? (short)DateTime.UtcNow.Year;
        var scopes = await _institutes.ListEmployeeScopesAsync([command.EmployeeId], ct);
        var balances = (await _leave.ListTrackedBalancesAsync([command.EmployeeId], LeaveTypes.Annual, leaveYear, ct))
            .ToDictionary(balance => balance.EmployeeId);
        var assigned = await AssignPreparedAsync(
            command.EmployeeId,
            command.TotalDays,
            leaveYear,
            staffCategoryFilter: null,
            scopes,
            new Dictionary<Guid, string?>(),
            balances,
            ct);
        if (assigned.Outcome != "assigned" || assigned.Balance is null)
        {
            return Result<LeaveBalanceDto>.Failure(AssignmentError(assigned));
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<LeaveBalanceDto>.Success(assigned.Balance);
    }

    public async Task<Result<BulkAssignAnnualLeaveResult>> BulkAssignAnnualEntitlementAsync(
        BulkAssignAnnualLeaveCommand command,
        CancellationToken ct)
    {
        if (!CanAssignAnnualLeave())
        {
            return Result<BulkAssignAnnualLeaveResult>.Failure(Error.Forbidden(
                "You are not authorized to assign annual leave days."));
        }

        var employeeIds = command.EmployeeIds ?? [];
        if (employeeIds.Count == 0)
        {
            return Result<BulkAssignAnnualLeaveResult>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["employeeIds"] = ["At least one employee ID is required."]
            }));
        }

        if (employeeIds.Count > 100)
        {
            return Result<BulkAssignAnnualLeaveResult>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["employeeIds"] = ["A maximum of 100 employee IDs can be assigned in one request."]
            }));
        }

        var validation = ValidateAssignmentInput(command.TotalDays, command.LeaveYear, command.StaffCategory);
        if (validation.IsFailure)
            return Result<BulkAssignAnnualLeaveResult>.Failure(validation.Error!);

        var leaveYear = command.LeaveYear ?? (short)DateTime.UtcNow.Year;
        var uniqueIds = employeeIds.Distinct().ToArray();
        var staffCategory = string.IsNullOrWhiteSpace(command.StaffCategory)
            ? null
            : command.StaffCategory.Trim().ToLowerInvariant();
        var scopes = await _institutes.ListEmployeeScopesAsync(uniqueIds, ct);
        var categories = staffCategory is null
            ? new Dictionary<Guid, string?>()
            : await _institutes.GetCurrentStaffCategoriesAsync(uniqueIds, ct);
        var balances = (await _leave.ListTrackedBalancesAsync(uniqueIds, LeaveTypes.Annual, leaveYear, ct))
            .ToDictionary(balance => balance.EmployeeId);
        var results = new List<BulkAssignAnnualLeaveItem>(uniqueIds.Length);
        var assigned = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var employeeId in uniqueIds)
        {
            var item = await AssignPreparedAsync(
                employeeId,
                command.TotalDays,
                leaveYear,
                staffCategory,
                scopes,
                categories,
                balances,
                ct);
            results.Add(item);
            switch (item.Outcome)
            {
                case "assigned":
                    assigned++;
                    break;
                case "skipped-category-mismatch":
                    skipped++;
                    break;
                default:
                    failed++;
                    break;
            }
        }

        if (assigned > 0)
            await _unitOfWork.SaveChangesAsync(ct);

        return Result<BulkAssignAnnualLeaveResult>.Success(
            new BulkAssignAnnualLeaveResult(assigned, skipped, failed, results));
    }

    private async Task<BulkAssignAnnualLeaveItem> AssignPreparedAsync(
        Guid employeeId,
        decimal totalDays,
        short leaveYear,
        string? staffCategoryFilter,
        IReadOnlyDictionary<Guid, EmployeeScope> scopes,
        IReadOnlyDictionary<Guid, string?> categories,
        IReadOnlyDictionary<Guid, LeaveBalance> balances,
        CancellationToken ct)
    {
        if (!scopes.TryGetValue(employeeId, out var employee) ||
            !InstituteScope.Resolve(_currentUser.InstituteId, null).Value!.CanAccess(employee.InstituteId))
        {
            return new BulkAssignAnnualLeaveItem(
                employeeId,
                "not-found",
                SpmeErrorCodes.NotFound,
                "Employee not found.",
                null);
        }

        if (!string.IsNullOrWhiteSpace(staffCategoryFilter))
        {
            var category = categories.GetValueOrDefault(employeeId);
            if (!string.Equals(category, staffCategoryFilter, StringComparison.OrdinalIgnoreCase))
            {
                return new BulkAssignAnnualLeaveItem(
                    employeeId,
                    "skipped-category-mismatch",
                    null,
                    "Employee staff category does not match the requested category.",
                    null);
            }
        }

        balances.TryGetValue(employeeId, out var existing);
        var before = existing is null ? "totalDays=none" : $"totalDays={existing.TotalDays}";
        if (existing is null)
        {
            var created = LeaveBalance.Create(employeeId, LeaveTypes.Annual, leaveYear, 0m);
            var createdResult = created.SetEntitlement(totalDays);
            if (createdResult.IsFailure)
            {
                return new BulkAssignAnnualLeaveItem(
                    employeeId,
                    "failed",
                    createdResult.Error!.Code,
                    createdResult.Error.Message,
                    null);
            }

            _leave.AddBalance(created);
            await _audit.RecordAsync(
                "leave-balance.assign",
                "LeaveBalance",
                created.Id.ToString(),
                before,
                $"totalDays={created.TotalDays};leaveYear={leaveYear}",
                ct);
            return new BulkAssignAnnualLeaveItem(employeeId, "assigned", null, null, MapBalance(created));
        }

        var updated = existing.SetEntitlement(totalDays);
        if (updated.IsFailure)
        {
            return new BulkAssignAnnualLeaveItem(
                employeeId,
                "failed",
                updated.Error!.Code,
                updated.Error.Message,
                null);
        }

        await _audit.RecordAsync(
            "leave-balance.assign",
            "LeaveBalance",
            existing.Id.ToString(),
            before,
            $"totalDays={existing.TotalDays};leaveYear={leaveYear}",
            ct);
        return new BulkAssignAnnualLeaveItem(employeeId, "assigned", null, null, MapBalance(existing));
    }

    private static Result<bool> ValidateAssignmentInput(decimal totalDays, short? leaveYear, string? staffCategory)
    {
        var fields = new Dictionary<string, string[]>();
        if (totalDays < 0m || totalDays > 366m || decimal.Round(totalDays, 2) != totalDays)
        {
            fields["totalDays"] = ["Annual leave days must be between 0 and 366, with at most two decimal places."];
        }

        if (leaveYear is < 2000 or > 2100)
        {
            fields["leaveYear"] = ["Leave year must be between 2000 and 2100."];
        }

        if (!string.IsNullOrWhiteSpace(staffCategory) &&
            !DomainValues.Contains(StaffCategories.All, staffCategory.Trim().ToLowerInvariant()))
        {
            fields["staffCategory"] = [$"Staff category must be one of: {string.Join(", ", StaffCategories.All)}."];
        }

        return fields.Count > 0
            ? Result.Failure(Error.Validation(fields))
            : Result.Success();
    }

    private static Error AssignmentError(BulkAssignAnnualLeaveItem item) =>
        item.Outcome switch
        {
            "not-found" => Error.NotFound("Employee not found."),
            "skipped-category-mismatch" => Error.Validation(new Dictionary<string, string[]>
            {
                ["staffCategory"] = [item.Message ?? "Employee staff category does not match the requested category."]
            }),
            _ => item.Code == SpmeErrorCodes.Conflict
                ? Error.Conflict(item.Message ?? "The leave entitlement could not be assigned.")
                : Error.Validation(item.Message ?? "The leave entitlement could not be assigned.")
        };

    private static LeaveBalanceDto MapBalance(LeaveBalance balance) =>
        new(balance.EmployeeId, balance.LeaveType, balance.LeaveYear, balance.TotalDays, balance.UsedDays,
            balance.PendingDays, balance.AdjustedDays, balance.RemainingDays);

    private bool CanAssignAnnualLeave() =>
        _currentUser.IsInRole("PlatformAdmin") ||
        _currentUser.IsInRole("HrAdmin") ||
        _currentUser.HasPermission(SpmePermissions.LeaveManage) ||
        LegacyStaffManagementRoles.WriteCompatible.Any(_currentUser.IsInRole);

    private async Task<Result<EmployeeScope>> ResolveTargetEmployeeAsync(Guid? requestedEmployeeId, CancellationToken ct)
    {
        var targetEmployeeId = requestedEmployeeId ?? _currentUser.EmployeeId;
        if (targetEmployeeId is null)
        {
            return Result<EmployeeScope>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["employeeId"] = ["An employee is required."]
            }));
        }

        if (requestedEmployeeId.HasValue && requestedEmployeeId != _currentUser.EmployeeId &&
            !CanManageInstituteLeave())
        {
            return Result<EmployeeScope>.Failure(Error.Forbidden(
                "You are not authorized to create leave requests for another employee."));
        }

        var employee = await _institutes.GetEmployeeScopeAsync(targetEmployeeId.Value, ct);
        if (employee is null || !InstituteScope.Resolve(_currentUser.InstituteId, null).Value!.CanAccess(employee.InstituteId))
        {
            return Result<EmployeeScope>.Failure(Error.NotFound("Employee not found."));
        }

        return Result<EmployeeScope>.Success(employee);
    }

    /// <summary>Validates a draft and returns the server-calculated working days.</summary>
    private async Task<Result<decimal>> PrepareDraftAsync(
        EmployeeScope employee, string leaveType, DateTime startDate, DateTime endDate,
        Guid? delegateEmployeeId, Guid? medicalDocumentFileId, Guid? admissionLetterFileId,
        Guid? handoverDocumentFileId, CancellationToken ct)
    {
        var fields = new Dictionary<string, string[]>();
        var normalizedType = leaveType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!DomainValues.Contains(LeaveTypes.All, normalizedType))
        {
            fields["leaveType"] = [$"Leave type must be one of: {string.Join(", ", LeaveTypes.All)}."];
        }

        if (endDate < startDate)
        {
            fields["endDate"] = ["The end date cannot precede the start date."];
        }

        if (fields.Count > 0)
        {
            return Result<decimal>.Failure(Error.Validation(fields));
        }

        var holidays = await _leave.GetHolidayDatesAsync(employee.InstituteId, startDate, endDate, ct);
        var workingDays = WorkingDaysCalculator.Calculate(startDate, endDate, holidays.ToHashSet());
        if (workingDays <= 0m)
        {
            return Result<decimal>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["startDate"] = ["The requested range contains no working days."]
            }));
        }

        var policy = await _leave.FindApplicablePolicyAsync(employee.InstituteId, normalizedType,
            employee.PositionTypeId, startDate, ct);
        if (policy?.MaxConsecutiveDays is { } maxDays && workingDays > maxDays)
        {
            return Result<decimal>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["workingDays"] = [$"The request exceeds the maximum of {maxDays} consecutive working days."]
            }));
        }

        if (policy?.RequiresDocument == true)
        {
            var missing = RequiresMedicalDocument(normalizedType)
                ? medicalDocumentFileId is null
                : admissionLetterFileId is null;
            if (missing)
            {
                var field = RequiresMedicalDocument(normalizedType) ? "medicalDocumentFileId" : "admissionLetterFileId";
                return Result<decimal>.Failure(Error.Validation(new Dictionary<string, string[]>
                {
                    [field] = ["A supporting document is required for this leave type."]
                }));
            }
        }

        foreach (var (fileId, field) in new[]
        {
            (medicalDocumentFileId, "medicalDocumentFileId"),
            (admissionLetterFileId, "admissionLetterFileId"),
            (handoverDocumentFileId, "handoverDocumentFileId")
        })
        {
            if (fileId.HasValue)
            {
                return Result<decimal>.Failure(Error.Validation(new Dictionary<string, string[]>
                {
                    [field] = ["This document cannot be attached until secure ownership, institute, purpose, and malware-scan verification is available."]
                }));
            }
        }

        if (delegateEmployeeId.HasValue)
        {
            if (delegateEmployeeId.Value == employee.EmployeeId)
            {
                return Result<decimal>.Failure(Error.Validation(new Dictionary<string, string[]>
                {
                    ["delegateEmployeeId"] = ["The delegate must be a different employee."]
                }));
            }

            var delegateScope = await _institutes.GetEmployeeScopeAsync(delegateEmployeeId.Value, ct);
            if (delegateScope is null || delegateScope.InstituteId != employee.InstituteId ||
                delegateScope.ProfileStatus != EmployeeProfileStatuses.Active)
            {
                return Result<decimal>.Failure(Error.Validation(new Dictionary<string, string[]>
                {
                    ["delegateEmployeeId"] = ["The delegate must be an active employee of the same institute."]
                }));
            }

            var authorOrg = await _leave.GetApprovalScopeAsync(employee.EmployeeId, ct);
            var allowed = await IsAllowedDelegateAsync(
                employee.InstituteId, employee.EmployeeId, authorOrg, delegateEmployeeId.Value, ct);
            if (!allowed)
            {
                return Result<decimal>.Failure(Error.Validation(new Dictionary<string, string[]>
                {
                    ["delegateEmployeeId"] = ["Choose a delegate from your section or division, or from another division when no local staff are available."]
                }));
            }
        }

        // Draft creation and draft edits are allowed to overlap; submission rejects overlaps.
        return Result<decimal>.Success(workingDays);
    }

    private static bool RequiresMedicalDocument(string leaveType) =>
        leaveType is LeaveTypes.Sick or LeaveTypes.LeaveOfAbsence or LeaveTypes.Compassionate;

    private bool IsAccessible(LeaveRequest? request)
    {
        if (request is null)
        {
            return false;
        }

        var scope = InstituteScope.Resolve(_currentUser.InstituteId, null).Value!;
        if (!scope.CanAccess(request.InstituteId))
        {
            return false;
        }

        if (!CanReadInstituteLeave() &&
            _currentUser.EmployeeId != request.EmployeeId)
        {
            return false;
        }

        return true;
    }

    private bool IsOwnerOrManager(LeaveRequest request) =>
        _currentUser.EmployeeId == request.EmployeeId ||
        CanManageInstituteLeave();

    /// <summary>
    /// Institute leave history readers: V2 HR/leadership roles, leave.read holders that are not
    /// employee-only self-service, and preserved StaffUser / legacy staff-management roles.
    /// </summary>
    private bool CanReadInstituteLeave()
    {
        if (IsEmployeeOnlySelfService())
            return false;

        if (_currentUser.IsInRole("PlatformAdmin") ||
            _currentUser.IsInRole("InstituteAdmin") ||
            _currentUser.IsInRole("HrAdmin") ||
            _currentUser.IsInRole("HeadOfSection") ||
            _currentUser.IsInRole("HeadOfDivision") ||
            _currentUser.IsInRole("InstituteDirector"))
            return true;

        if (HasLegacyStaffManagementReadAccess())
            return true;

        return _currentUser.HasPermission(SpmePermissions.LeaveRead);
    }

    /// <summary>
    /// PlatformAdmin and HrAdmin may list leave across all institutes when they have no institute claim.
    /// </summary>
    private bool CanListLeaveAcrossInstitutes() =>
        _currentUser.IsInRole("PlatformAdmin") || _currentUser.IsInRole("HrAdmin");

    private bool HasLegacyStaffManagementReadAccess()
    {
        if (string.Equals(_currentUser.IdentityType, "StaffUser", StringComparison.OrdinalIgnoreCase))
            return true;

        return LegacyStaffManagementRoles.All.Any(_currentUser.IsInRole);
    }

    private bool IsEmployeeOnlySelfService()
    {
        if (HasLegacyStaffManagementReadAccess())
            return false;

        if (_currentUser.IsInRole("PlatformAdmin") ||
            _currentUser.IsInRole("InstituteAdmin") ||
            _currentUser.IsInRole("HrAdmin") ||
            _currentUser.IsInRole("HeadOfSection") ||
            _currentUser.IsInRole("HeadOfDivision") ||
            _currentUser.IsInRole("InstituteDirector"))
            return false;

        return string.Equals(_currentUser.IdentityType, "Employee", StringComparison.OrdinalIgnoreCase) ||
               _currentUser.IsInRole("Employee");
    }

    private bool CanManageInstituteLeave() =>
        (_currentUser.IsInRole("PlatformAdmin") || _currentUser.IsInRole("HrAdmin")) &&
        _currentUser.HasPermission(SpmePermissions.LeaveManage);

    private string ApprovalAuditState(string status) =>
        _currentUser.IsInRole("PlatformAdmin")
            ? $"status={status};platformOverride=true"
            : $"status={status};platformOverride=false";

    private async Task<bool> CanApproveStageAsync(string stage, Guid targetEmployeeId, CancellationToken ct)
    {
        if (_currentUser.IsInRole("PlatformAdmin")) return true;
        if (!_currentUser.EmployeeId.HasValue) return false;

        var actor = await _leave.GetApprovalScopeAsync(_currentUser.EmployeeId.Value, ct);
        var target = await _leave.GetApprovalScopeAsync(targetEmployeeId, ct);
        if (actor is null || target is null || actor.InstituteId != target.InstituteId ||
            actor.InstituteId != _currentUser.InstituteId)
            return false;

        return stage switch
        {
            LeaveApprovalStages.SectionHead => _currentUser.IsInRole("HeadOfSection") &&
                actor.SectionId.HasValue && actor.SectionId == target.SectionId,
            LeaveApprovalStages.HeadOfDivision => _currentUser.IsInRole("HeadOfDivision") &&
                actor.DivisionId.HasValue && actor.DivisionId == target.DivisionId,
            LeaveApprovalStages.InstituteDirector => _currentUser.IsInRole("InstituteDirector"),
            _ => false
        };
    }

    private async Task<Result<LeaveRequestDto>> SaveAsync(LeaveRequest request, CancellationToken ct)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<LeaveRequestDto>.Failure(Error.PreconditionFailed(
                "The leave request was modified by another request. Reload it and retry."));
        }

        return Result<LeaveRequestDto>.Success(await MapAsync(request, ct));
    }

    private async Task<Error?> RecordAuditAsync(
        string action, LeaveRequest request, string? before, string? after, CancellationToken ct)
    {
        try
        {
            await _audit.RecordAsync(action, "LeaveRequest", request.Id.ToString(), before, after, ct);
            return null;
        }
        catch (ConcurrencyConflictException)
        {
            return Error.PreconditionFailed(
                "The leave request was modified by another request. Reload it and retry.");
        }
    }

    public async Task<Result<LeaveDelegateOptionsDto>> ListMyDelegateOptionsAsync(
        Guid? divisionId, CancellationToken ct)
    {
        if (!_currentUser.EmployeeId.HasValue || !_currentUser.InstituteId.HasValue)
            return Result<LeaveDelegateOptionsDto>.Failure(Error.NotFound("Staff identity link not found."));

        var employeeId = _currentUser.EmployeeId.Value;
        var instituteId = _currentUser.InstituteId.Value;
        var authorOrg = await _leave.GetApprovalScopeAsync(employeeId, ct);
        if (authorOrg is null)
            return Result<LeaveDelegateOptionsDto>.Failure(Error.NotFound("Current employment was not found."));

        var home = await ResolveHomeDelegateScopeAsync(instituteId, employeeId, authorOrg, ct);
        if (divisionId.HasValue)
        {
            if (!home.PreferAlternateDivision && divisionId != authorOrg.DivisionId)
            {
                return Result<LeaveDelegateOptionsDto>.Failure(Error.Validation(new Dictionary<string, string[]>
                {
                    ["divisionId"] = ["Another division can be selected only when no staff are available in your section or division."]
                }));
            }

            var alternateDivision = (await _leave.ListDelegateDivisionsAsync(instituteId, null, ct))
                .SingleOrDefault(item => item.Id == divisionId.Value);
            if (alternateDivision is null)
            {
                return Result<LeaveDelegateOptionsDto>.Failure(Error.Validation(new Dictionary<string, string[]>
                {
                    ["divisionId"] = ["Select a division in your institute."]
                }));
            }

            var alternateDelegates = await _leave.ListDelegateOptionsAsync(
                instituteId, employeeId, null, divisionId.Value, ct);
            return Result<LeaveDelegateOptionsDto>.Success(new LeaveDelegateOptionsDto(
                MapDelegates(alternateDelegates),
                "alternate-division",
                divisionId.Value,
                null,
                home.PreferAlternateDivision,
                home.AlternateDivisions));
        }

        return Result<LeaveDelegateOptionsDto>.Success(new LeaveDelegateOptionsDto(
            MapDelegates(home.Delegates),
            home.ScopeMode,
            home.DivisionId,
            home.SectionId,
            home.PreferAlternateDivision,
            home.AlternateDivisions));
    }

    private async Task<(
        IReadOnlyList<LeaveDelegateOption> Delegates,
        string ScopeMode,
        Guid? DivisionId,
        Guid? SectionId,
        bool PreferAlternateDivision,
        IReadOnlyList<LeaveDelegateDivisionDto> AlternateDivisions)> ResolveHomeDelegateScopeAsync(
        Guid instituteId,
        Guid employeeId,
        LeaveApprovalScope authorOrg,
        CancellationToken ct)
    {
        if (authorOrg.SectionId.HasValue)
        {
            var sectionDelegates = await _leave.ListDelegateOptionsAsync(
                instituteId, employeeId, authorOrg.SectionId, null, ct);
            if (sectionDelegates.Count > 0)
            {
                return (sectionDelegates, "section", authorOrg.DivisionId, authorOrg.SectionId, false, []);
            }
        }

        if (authorOrg.DivisionId.HasValue)
        {
            var divisionDelegates = await _leave.ListDelegateOptionsAsync(
                instituteId, employeeId, null, authorOrg.DivisionId, ct);
            if (divisionDelegates.Count > 0)
            {
                return (divisionDelegates, "division", authorOrg.DivisionId, authorOrg.SectionId, false, []);
            }
        }

        var alternateDivisions = (await _leave.ListDelegateDivisionsAsync(
                instituteId, authorOrg.DivisionId, ct))
            .Select(item => new LeaveDelegateDivisionDto(item.Id, item.Name))
            .ToList();
        return ([], "none", authorOrg.DivisionId, authorOrg.SectionId, true, alternateDivisions);
    }

    private async Task<bool> IsAllowedDelegateAsync(
        Guid instituteId,
        Guid employeeId,
        LeaveApprovalScope? authorOrg,
        Guid delegateEmployeeId,
        CancellationToken ct)
    {
        if (authorOrg is null) return false;
        var home = await ResolveHomeDelegateScopeAsync(instituteId, employeeId, authorOrg, ct);
        if (!home.PreferAlternateDivision)
            return home.Delegates.Any(option => option.EmployeeId == delegateEmployeeId);

        var delegateOrg = await _leave.GetApprovalScopeAsync(delegateEmployeeId, ct);
        return delegateOrg is not null &&
               delegateOrg.InstituteId == instituteId &&
               delegateOrg.DivisionId.HasValue;
    }

    private static IReadOnlyList<LeaveDelegateOptionDto> MapDelegates(IReadOnlyList<LeaveDelegateOption> options) =>
        options.Select(option => new LeaveDelegateOptionDto(
            option.EmployeeId, option.StaffId, option.DisplayName, option.JobTitle)).ToList();

    private static string Normalize(string leaveType) => leaveType.Trim().ToLowerInvariant();

    private async Task<ListSlice<LeaveRequestDto>> MapAsync(ListSlice<LeaveRequest> slice, CancellationToken ct)
    {
        var items = new List<LeaveRequestDto>(slice.Items.Count);
        foreach (var request in slice.Items)
            items.Add(await MapAsync(request, ct));
        return new(items, slice.Next);
    }

    private async Task<LeaveRequestDto> MapAsync(LeaveRequest request, CancellationToken ct) => new(
        request.Id, request.EmployeeId, request.InstituteId, request.LeaveType, request.StartDate,
        request.EndDate, request.WorkingDays, request.Status, request.CurrentApprovalStage,
        request.Reason, request.HandoverNotes, request.DelegateEmployeeId, request.SubmittedAt,
        request.CompletedAt, request.CancelledAt, request.RejectionReason, request.MedicalDocumentFileId,
        request.AdmissionLetterFileId, request.HandoverDocumentFileId,
        await AvailableActionsAsync(request, ct),
        ConcurrencyToken.Format(request.RowVersion), request.CreatedAt, request.UpdatedAt);

    private readonly Dictionary<(string Stage, Guid EmployeeId), bool> _stageAuthorization = new();

    private async Task<IReadOnlyList<string>> AvailableActionsAsync(LeaveRequest request, CancellationToken ct)
    {
        if (_currentUser.EmployeeId == request.EmployeeId)
        {
            if (request.Status == LeaveRequestStatuses.Draft) return ["edit", "submit", "cancel"];
            if (request.Status is LeaveRequestStatuses.Submitted or LeaveRequestStatuses.UnderReview) return ["cancel"];
            if (request.Status == LeaveRequestStatuses.Approved)
            {
                return DateTime.UtcNow.Date > request.EndDate.Date
                    ? ["cancel", "resume"]
                    : ["cancel"];
            }
            return [];
        }

        if (!_currentUser.HasPermission(SpmePermissions.LeaveApprove))
            return [];

        if (request.Status is LeaveRequestStatuses.Submitted or LeaveRequestStatuses.UnderReview &&
            !string.IsNullOrWhiteSpace(request.CurrentApprovalStage) &&
            await CachedCanApproveStageAsync(request.CurrentApprovalStage, request.EmployeeId, ct))
            return ["approve", "reject"];

        if (request.Status == LeaveRequestStatuses.ResumptionPending &&
            await CachedCanApproveStageAsync(LeaveApprovalStages.SectionHead, request.EmployeeId, ct))
            return ["approve"];

        return [];
    }

    private async Task<bool> CachedCanApproveStageAsync(string stage, Guid employeeId, CancellationToken ct)
    {
        var key = (stage, employeeId);
        if (_stageAuthorization.TryGetValue(key, out var cached))
            return cached;
        var allowed = await CanApproveStageAsync(stage, employeeId, ct);
        _stageAuthorization[key] = allowed;
        return allowed;
    }
}
