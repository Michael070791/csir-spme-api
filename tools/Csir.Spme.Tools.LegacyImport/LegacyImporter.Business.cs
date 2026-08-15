using System.Globalization;
using System.Text.Json;
using Csir.Spme.Domain.Comms;
using Csir.Spme.Domain.Knowledge;
using Csir.Spme.Domain.Leave;
using Csir.Spme.Domain.Plan;
using Csir.Spme.Domain.Projects;
using Csir.Spme.Domain.Reporting;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Tools.LegacyImport;

internal sealed partial class LegacyImporter
{
    private readonly Dictionary<Guid, Guid> _positionTypesByLegacyId = [];
    private readonly Dictionary<int, Guid> _thrustsByLegacyId = [];
    private readonly Dictionary<int, Guid> _outputsByLegacyId = [];
    private readonly Dictionary<Guid, Guid> _indicatorsByLegacyId = [];
    private readonly Dictionary<Guid, Guid> _projectsByLegacyId = [];
    private readonly Dictionary<Guid, Guid> _reportsByLegacyId = [];
    private readonly Dictionary<Guid, Guid> _technologiesByLegacyId = [];
    private readonly Dictionary<Guid, Guid> _leaveRequestsByLegacyId = [];
    private readonly Dictionary<Guid, Guid> _holidayPeriodsByLegacyId = [];
    private readonly Dictionary<string, Guid> _reportingPeriods = new(StringComparer.OrdinalIgnoreCase);
    private Guid _migrationActorUserId;

    private async Task ImportRemainingBusinessDataAsync(
        SqlConnection legacyAuth,
        SqlConnection legacySpme,
        CancellationToken cancellationToken)
    {
        await ImportLeaveAsync(legacyAuth, cancellationToken);
        await ImportPlanningAsync(legacySpme, cancellationToken);
        await ImportProjectsAsync(legacySpme, cancellationToken);
        await ImportReportsAsync(legacySpme, cancellationToken);
        await ImportKnowledgeAsync(legacySpme, cancellationToken);
        await ImportMemosAsync(legacySpme, cancellationToken);
        await ImportNotificationsAsync(legacySpme, cancellationToken);
        await RecordUnsafeSourceStateAsync(cancellationToken);
    }

    private async Task<Guid> ResolveMigrationActorAsync(CancellationToken cancellationToken)
    {
        var userId = await (
            from user in _target.Users.AsNoTracking()
            join assignment in _target.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().AsNoTracking()
                on user.Id equals assignment.UserId
            join role in _target.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            where role.NormalizedName == "PLATFORMADMIN"
            select (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (userId.HasValue)
            return userId.Value;

        userId = await _target.Users.AsNoTracking().Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return userId ?? throw new InvalidOperationException("A seeded V2 migration actor is required.");
    }

    private async Task ImportPositionTypesAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = await source.QueryAsync<LegacyPositionType>(
            new CommandDefinition(
                "select Id, Name, AnnualLeaveDays from dbo.PositionTypes order by Name",
                cancellationToken: cancellationToken));
        foreach (var row in rows)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacyAuthSpme", "PositionTypes", sourceKey);
            if (mapped.HasValue)
            {
                _positionTypesByLegacyId[row.Id] = mapped.Value;
                AddMapping("LegacyAuthSpme", "PositionTypes", sourceKey, "org", "PositionTypes", mapped.Value, row.Name, "resume-existing", row);
                continue;
            }

            var code = CodeFromText(row.Name, 32);
            var target = await _target.PositionTypes.FirstOrDefaultAsync(item => item.Code == code, cancellationToken);
            if (target is null)
            {
                target = new Csir.Spme.Domain.Org.PositionType(code, Limit(row.Name, 128), checked((short)row.AnnualLeaveDays));
                if (WritesWorkingState)
                    _target.PositionTypes.Add(target);
                _run.AddInserted();
            }

            _positionTypesByLegacyId[row.Id] = target.Id;
            AddMapping("LegacyAuthSpme", "PositionTypes", sourceKey, "org", "PositionTypes", target.Id, code, "code", row);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportLeaveAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        await ImportLeaveBalancesAsync(source, cancellationToken);
        await ImportHolidaysAsync(source, cancellationToken);
        await ImportHolidayPeriodsAsync(source, cancellationToken);
        await ImportCompassionateLeaveTypesAsync(source, cancellationToken);
        await ImportLeaveRequestsAsync(source, cancellationToken);
        await ImportLeaveApprovalsAsync(source, cancellationToken);
        await ImportLeaveHandoversAsync(source, cancellationToken);
        await ImportLeaveResumptionsAsync(source, cancellationToken);
    }

    private async Task ImportLeaveBalancesAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = await source.QueryAsync<LegacyLeaveBalance>(
            new CommandDefinition("select * from dbo.EmployeeLeaveRecords", cancellationToken: cancellationToken));
        foreach (var row in rows)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacyAuthSpme", "EmployeeLeaveRecords", sourceKey);
            if (mapped.HasValue)
            {
                AddMapping("LegacyAuthSpme", "EmployeeLeaveRecords", sourceKey, "leave", "LeaveBalances", mapped.Value, row.UserId.ToString(), "resume-existing", row);
                continue;
            }

            var employeeId = ResolveEmployeeByLegacyUser(row.UserId.ToString());
            if (!employeeId.HasValue)
            {
                AddIssue("LegacyAuthSpme", "EmployeeLeaveRecords", sourceKey, "error", "employee-not-found", "Leave balance employee could not be resolved.", row);
                continue;
            }

            const string leaveType = "annual";
            if (row.LeaveType.HasValue && row.LeaveType.Value != 1)
            {
                AddIssue("LegacyAuthSpme", "EmployeeLeaveRecords", sourceKey, "error", "unsupported-leave-type", $"Legacy leave type {row.LeaveType} has no approved V2 mapping.", row);
                continue;
            }

            var adjusted = row.RemainingDays - row.TotalDays + row.UsedDays;
            var target = await _target.LeaveBalances.FirstOrDefaultAsync(
                item => item.EmployeeId == employeeId && item.LeaveYear == row.Year && item.LeaveType == leaveType,
                cancellationToken);
            if (target is null)
            {
                target = LeaveBalance.CreateImported(
                    employeeId.Value,
                    leaveType,
                    checked((short)row.Year),
                    row.TotalDays,
                    row.UsedDays,
                    0,
                    adjusted);
                if (WritesWorkingState)
                    _target.LeaveBalances.Add(target);
                _run.AddInserted();
            }

            AddMapping("LegacyAuthSpme", "EmployeeLeaveRecords", sourceKey, "leave", "LeaveBalances", target.Id, $"{employeeId}:{row.Year}:{leaveType}", "employee-year-type", row);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportHolidaysAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = await source.QueryAsync<LegacyHoliday>(
            new CommandDefinition("select Id, Name, Date, IsFullDay, IsIslamic, Notes from dbo.Holidays", cancellationToken: cancellationToken));
        foreach (var row in rows)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacyAuthSpme", "Holidays", sourceKey);
            if (mapped.HasValue)
            {
                AddMapping("LegacyAuthSpme", "Holidays", sourceKey, "leave", "Holidays", mapped.Value, $"{row.Date:yyyy-MM-dd}:{row.Name}", "resume-existing", row);
                continue;
            }

            var name = Limit(row.Name, 128);
            var target = await _target.Holidays.FirstOrDefaultAsync(
                item => item.ScopeType == "csir-wide" && item.HolidayDate == row.Date.Date && item.Name == name,
                cancellationToken);
            if (target is null)
            {
                var result = Holiday.Create("csir-wide", null, name, row.Date, row.IsFullDay, row.IsIslamic, LimitOptional(row.Notes, 2000));
                if (!result.IsSuccess)
                {
                    AddIssue("LegacyAuthSpme", "Holidays", sourceKey, "error", "validation-failed", result.Error!.Message, row);
                    continue;
                }

                target = result.Value!;
                if (WritesWorkingState)
                    _target.Holidays.Add(target);
                _run.AddInserted();
            }

            AddMapping("LegacyAuthSpme", "Holidays", sourceKey, "leave", "Holidays", target.Id, $"{row.Date:yyyy-MM-dd}:{NormalizeKey(name)}", "date-name", row);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportHolidayPeriodsAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = await source.QueryAsync<LegacyHolidayPeriod>(
            new CommandDefinition("select * from dbo.HolidayPeriods", cancellationToken: cancellationToken));
        foreach (var row in rows)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacyAuthSpme", "HolidayPeriods", sourceKey);
            if (mapped.HasValue)
            {
                _holidayPeriodsByLegacyId[row.Id] = mapped.Value;
                AddMapping("LegacyAuthSpme", "HolidayPeriods", sourceKey, "leave", "HolidayPeriods", mapped.Value, row.Year.ToString(CultureInfo.InvariantCulture), "resume-existing", row);
                continue;
            }

            var instituteId = await ResolveInstituteOrNullAsync(row.InstituteCode);
            var scopeType = instituteId.HasValue ? "institute" : "csir-wide";
            var status = NormalizeHolidayPeriodStatus(row.Status, row.IsActive);
            var finalizedBy = row.FinalizedByUserId.HasValue
                ? ResolveLegacyUserId(row.FinalizedByUserId.Value.ToString())
                : null;
            var target = HolidayPeriod.CreateImported(
                scopeType,
                instituteId,
                checked((short)row.Year),
                row.ChristmasStartDate,
                row.ChristmasEndDate,
                row.NewYearStartDate,
                row.NewYearEndDate,
                row.AvailabilityStartDate,
                row.AvailabilityEndDate,
                checked((short)row.DeductionDays),
                status,
                LegacyValueParser.ParseDateTimeOffset(row.FinalizedAt),
                finalizedBy,
                LimitOptional(row.Notes, 2000));
            if (WritesWorkingState)
                _target.HolidayPeriods.Add(target);
            _run.AddInserted();
            _holidayPeriodsByLegacyId[row.Id] = target.Id;
            AddMapping("LegacyAuthSpme", "HolidayPeriods", sourceKey, "leave", "HolidayPeriods", target.Id, $"{scopeType}:{instituteId}:{row.Year}", "scope-year", row);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportCompassionateLeaveTypesAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = await source.QueryAsync<LegacyCompassionateLeaveType>(
            new CommandDefinition("select Id, Name, Days, DoesNotDeductFromBalance from dbo.CompassionateLeaveTypes", cancellationToken: cancellationToken));
        foreach (var row in rows)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacyAuthSpme", "CompassionateLeaveTypes", sourceKey);
            if (mapped.HasValue)
            {
                AddMapping("LegacyAuthSpme", "CompassionateLeaveTypes", sourceKey, "leave", "CompassionateLeaveTypes", mapped.Value, row.Name, "resume-existing", row);
                continue;
            }

            var code = CodeFromText(row.Name, 32).ToLowerInvariant();
            var target = await _target.CompassionateLeaveTypes.FirstOrDefaultAsync(item => item.Code == code, cancellationToken);
            if (target is null)
            {
                target = new CompassionateLeaveType(code, Limit(row.Name, 128), row.Days, row.DoesNotDeductFromBalance);
                if (WritesWorkingState)
                    _target.CompassionateLeaveTypes.Add(target);
                _run.AddInserted();
            }

            AddMapping("LegacyAuthSpme", "CompassionateLeaveTypes", sourceKey, "leave", "CompassionateLeaveTypes", target.Id, code, "code", row);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportLeaveRequestsAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = await source.QueryAsync<LegacyLeaveRequest>(
            new CommandDefinition("select * from dbo.LeaveRequests", cancellationToken: cancellationToken));
        foreach (var row in rows)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacyAuthSpme", "LeaveRequests", sourceKey);
            if (mapped.HasValue)
            {
                _leaveRequestsByLegacyId[row.Id] = mapped.Value;
                AddMapping("LegacyAuthSpme", "LeaveRequests", sourceKey, "leave", "LeaveRequests", mapped.Value, row.UserId.ToString(), "resume-existing", row);
                continue;
            }

            var leaveType = NormalizeLeaveType(row.LeaveType);
            if (leaveType is null)
            {
                AddIssue("LegacyAuthSpme", "LeaveRequests", sourceKey, "error", "unsupported-leave-type", $"Leave type '{row.LeaveType}' has no approved V2 controlled-value mapping.", row);
                continue;
            }

            var employeeId = ResolveEmployeeByLegacyUser(row.UserId.ToString());
            var instituteId = employeeId.HasValue
                ? await ResolveEmployeeInstituteAsync(employeeId.Value)
                : null;
            if (!employeeId.HasValue || !instituteId.HasValue)
            {
                AddIssue("LegacyAuthSpme", "LeaveRequests", sourceKey, "error", "employee-not-found", "Leave request employee could not be resolved.", row);
                continue;
            }

            var startDate = LegacyValueParser.ParseDate(row.StartDate);
            var endDate = LegacyValueParser.ParseDate(row.EndDate);
            if (!startDate.HasValue || !endDate.HasValue || endDate < startDate)
            {
                AddIssue("LegacyAuthSpme", "LeaveRequests", sourceKey, "error", "invalid-date-range", "Leave request dates are invalid.", row);
                continue;
            }

            var target = LeaveRequest.CreateDraft(
                employeeId.Value,
                instituteId.Value,
                leaveType,
                startDate.Value,
                endDate.Value,
                row.NumberOfDays,
                LimitOptional(row.Comment, 2000),
                LimitOptional(row.HandoverNotes, 2000),
                row.DelegateUserId.HasValue ? ResolveEmployeeByLegacyUser(row.DelegateUserId.Value.ToString()) : null,
                null,
                null,
                null);

            var submittedAt = LegacyValueParser.ParseDateTimeOffset(row.SubmittedAt) ?? DateTimeOffset.UtcNow;
            var submit = target.Submit("head-of-division", submittedAt);
            if (!submit.IsSuccess)
            {
                AddIssue("LegacyAuthSpme", "LeaveRequests", sourceKey, "error", "invalid-state-transition", submit.Error!.Message, row);
                continue;
            }

            if (row.Approved || row.Status?.Equals("Approved", StringComparison.OrdinalIgnoreCase) == true ||
                row.Status?.Equals("Completed", StringComparison.OrdinalIgnoreCase) == true)
            {
                target.Approve("head-of-division", "admin-director");
                target.Approve("admin-director", "institute-director");
                target.Approve("institute-director", null);
            }

            if (WritesWorkingState)
                _target.LeaveRequests.Add(target);
            _run.AddInserted();
            _leaveRequestsByLegacyId[row.Id] = target.Id;
            AddMapping("LegacyAuthSpme", "LeaveRequests", sourceKey, "leave", "LeaveRequests", target.Id, row.Id.ToString(), "source-id", row);

            foreach (var missingPath in new[] { row.MedicalDocumentUrl, row.AdmissionLetterUrl, row.HandoverNotesDocumentUrl }
                         .Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                AddIssue("LegacyAuthSpme", "LeaveRequests", sourceKey, "warning", "legacy-file-bytes-missing", "Leave document metadata was reconciled but not made downloadable because file bytes and scan evidence are unavailable.", new { row.Id, hasLegacyPath = true });
            }
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportLeaveApprovalsAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = await source.QueryAsync<LegacyLeaveApproval>(
            new CommandDefinition("select * from dbo.LeaveApprovals", cancellationToken: cancellationToken));
        var sequence = new Dictionary<Guid, short>();
        foreach (var row in rows.OrderBy(item => LegacyValueParser.ParseDateTimeOffset(item.ApprovedAt)))
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacyAuthSpme", "LeaveApprovals", sourceKey);
            if (mapped.HasValue)
            {
                AddMapping("LegacyAuthSpme", "LeaveApprovals", sourceKey, "leave", "LeaveRequestApprovals", mapped.Value, row.LeaveRequestId.ToString(), "resume-existing", row);
                continue;
            }

            if (!_leaveRequestsByLegacyId.TryGetValue(row.LeaveRequestId, out var leaveRequestId))
            {
                AddIssue("LegacyAuthSpme", "LeaveApprovals", sourceKey, "warning", "leave-request-not-imported", "Approval belongs to a leave type with no lawful V2 mapping.", row);
                continue;
            }

            var approverId = ResolveLegacyUserId(row.ApproverUserId);
            if (!approverId.HasValue)
            {
                AddIssue("LegacyAuthSpme", "LeaveApprovals", sourceKey, "error", "approver-not-found", "Leave approver user could not be resolved.", row);
                continue;
            }

            sequence.TryGetValue(row.LeaveRequestId, out var current);
            current++;
            sequence[row.LeaveRequestId] = current;
            var target = LeaveRequestApproval.Create(
                leaveRequestId,
                approverId.Value,
                NormalizeApprovalStage(row.ApprovalStage),
                row.IsApproved ? "approved" : "rejected",
                LimitOptional(row.Comments, 2000),
                LimitOptional(row.Signature, 256),
                current);
            if (WritesWorkingState)
                _target.LeaveRequestApprovals.Add(target);
            _run.AddInserted();
            AddMapping("LegacyAuthSpme", "LeaveApprovals", sourceKey, "leave", "LeaveRequestApprovals", target.Id, $"{leaveRequestId}:{current}", "request-sequence", row);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportLeaveHandoversAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = await source.QueryAsync<LegacyLeaveHandover>(
            new CommandDefinition(
                "select Id, LeaveRequestId, HandoverNotes, DelegateUserId from dbo.LeaveHandovers",
                cancellationToken: cancellationToken));
        foreach (var row in rows)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacyAuthSpme", "LeaveHandovers", sourceKey);
            if (mapped.HasValue)
            {
                AddMapping("LegacyAuthSpme", "LeaveHandovers", sourceKey, "leave", "LeaveHandovers", mapped.Value, row.LeaveRequestId.ToString(), "resume-existing", row);
                continue;
            }

            if (!_leaveRequestsByLegacyId.TryGetValue(row.LeaveRequestId, out var leaveRequestId))
            {
                AddIssue("LegacyAuthSpme", "LeaveHandovers", sourceKey, "warning", "leave-request-not-imported", "Handover belongs to a leave type with no lawful V2 mapping.", row);
                continue;
            }

            var target = LeaveHandover.CreateImported(
                leaveRequestId,
                row.DelegateUserId.HasValue ? ResolveEmployeeByLegacyUser(row.DelegateUserId.Value.ToString()) : null,
                LimitOptional(row.HandoverNotes, 4000));
            if (WritesWorkingState)
                _target.LeaveHandovers.Add(target);
            _run.AddInserted();
            AddMapping("LegacyAuthSpme", "LeaveHandovers", sourceKey, "leave", "LeaveHandovers", target.Id, leaveRequestId.ToString(), "request", row);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportLeaveResumptionsAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = await source.QueryAsync<LegacyLeaveResumption>(
            new CommandDefinition("select * from dbo.LeaveResumptions", cancellationToken: cancellationToken));
        foreach (var row in rows)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacyAuthSpme", "LeaveResumptions", sourceKey);
            if (mapped.HasValue)
            {
                AddMapping("LegacyAuthSpme", "LeaveResumptions", sourceKey, "leave", "LeaveResumptions", mapped.Value, row.LeaveRequestId.ToString(), "resume-existing", row);
                continue;
            }

            if (!_leaveRequestsByLegacyId.TryGetValue(row.LeaveRequestId, out var leaveRequestId))
            {
                AddIssue("LegacyAuthSpme", "LeaveResumptions", sourceKey, "warning", "leave-request-not-imported", "Resumption belongs to a leave type with no lawful V2 mapping.", row);
                continue;
            }

            var employeeId = ResolveEmployeeByLegacyUser(row.EmployeeId.ToString());
            var leaveRequest = await _target.LeaveRequests.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == leaveRequestId, cancellationToken);
            var resumptionDate = LegacyValueParser.ParseDate(row.ResumptionDate);
            if (!employeeId.HasValue || leaveRequest is null || !resumptionDate.HasValue)
            {
                AddIssue("LegacyAuthSpme", "LeaveResumptions", sourceKey, "error", "invalid-resumption", "Resumption employee, request, or date could not be resolved.", row);
                continue;
            }

            var result = LeaveResumption.Create(
                leaveRequestId,
                employeeId.Value,
                resumptionDate.Value,
                leaveRequest.EndDate,
                LimitOptional(row.EmployeeSignature, 256));
            if (!result.IsSuccess)
            {
                AddIssue("LegacyAuthSpme", "LeaveResumptions", sourceKey, "error", "invalid-resumption", result.Error!.Message, row);
                continue;
            }

            var target = result.Value!;
            if (WritesWorkingState)
                _target.LeaveResumptions.Add(target);
            _run.AddInserted();
            AddMapping("LegacyAuthSpme", "LeaveResumptions", sourceKey, "leave", "LeaveResumptions", target.Id, leaveRequestId.ToString(), "request", row);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportPlanningAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var thrustRows = (await source.QueryAsync<LegacyThrust>(
            new CommandDefinition("select * from dbo.Thrusts order by InstituteId, Id", cancellationToken: cancellationToken))).ToList();
        var plans = new Dictionary<Guid, StrategicPlan>();
        foreach (var sourceInstituteId in thrustRows.Select(row => row.InstituteId).Distinct())
        {
            if (!_institutesByLegacyId.TryGetValue(sourceInstituteId, out var instituteId))
                continue;

            var plan = await _target.StrategicPlans.FirstOrDefaultAsync(
                item => item.InstituteId == instituteId && item.Code == "LEGACY-HOLDING",
                cancellationToken);
            if (plan is null)
            {
                plan = StrategicPlan.Create(
                    instituteId,
                    "LEGACY-HOLDING",
                    "Legacy migration holding plan",
                    "Migration-only parent for legacy thrusts whose source database contains no strategic-plan record.",
                    "Preserve legacy planning records pending authorized plan classification.",
                    2020,
                    2026);
                if (WritesWorkingState)
                    _target.StrategicPlans.Add(plan);
                _run.AddInserted();
                AddIssue("LegacySpme", "StrategicPlans", sourceInstituteId.ToString(), "warning", "synthetic-holding-plan", "A non-active migration holding plan was created because the source contains thrusts but no strategic plans.", new { sourceInstituteId, instituteId });
            }

            plans[instituteId] = plan;
        }

        await SaveIfApplyAsync();

        short thrustOrder = 0;
        foreach (var row in thrustRows)
        {
            var sourceKey = row.Id.ToString(CultureInfo.InvariantCulture);
            var mapped = GetExistingMapping("LegacySpme", "Thrusts", sourceKey);
            if (mapped.HasValue)
            {
                _thrustsByLegacyId[row.Id] = mapped.Value;
                AddMapping("LegacySpme", "Thrusts", sourceKey, "plan", "Thrusts", mapped.Value, row.UID, "resume-existing", row);
                continue;
            }

            if (!_institutesByLegacyId.TryGetValue(row.InstituteId, out var instituteId) ||
                !plans.TryGetValue(instituteId, out var plan))
            {
                AddIssue("LegacySpme", "Thrusts", sourceKey, "error", "institute-not-found", "Thrust institute could not be resolved.", row);
                continue;
            }

            var code = UniqueCode(row.UID, "THRUST", row.Id, 32);
            var target = Thrust.Create(
                plan.Id,
                instituteId,
                code,
                Limit(FirstNonBlank(row.UID, $"Thrust {row.Id}")!, 256),
                Limit(row.Description, 4000),
                Limit(row.Objective, 4000),
                ++thrustOrder);
            if (WritesWorkingState)
                _target.Thrusts.Add(target);
            _run.AddInserted();
            _thrustsByLegacyId[row.Id] = target.Id;
            AddMapping("LegacySpme", "Thrusts", sourceKey, "plan", "Thrusts", target.Id, $"{plan.Id}:{code}", "plan-code", row);
        }

        await SaveIfApplyAsync();

        var outputRows = await source.QueryAsync<LegacyOutput>(
            new CommandDefinition(
                "select Id, OutputIdNumber, Description, ThrustId from dbo.Outputs order by ThrustId, Id",
                cancellationToken: cancellationToken));
        var outputOrder = new Dictionary<int, short>();
        foreach (var row in outputRows)
        {
            var sourceKey = row.Id.ToString(CultureInfo.InvariantCulture);
            var mapped = GetExistingMapping("LegacySpme", "Outputs", sourceKey);
            if (mapped.HasValue)
            {
                _outputsByLegacyId[row.Id] = mapped.Value;
                AddMapping("LegacySpme", "Outputs", sourceKey, "plan", "Outputs", mapped.Value, row.OutputIdNumber ?? string.Empty, "resume-existing", row);
                continue;
            }

            if (!_thrustsByLegacyId.TryGetValue(row.ThrustId, out var thrustId))
            {
                AddIssue("LegacySpme", "Outputs", sourceKey, "error", "thrust-not-found", "Output thrust could not be resolved.", row);
                continue;
            }

            outputOrder.TryGetValue(row.ThrustId, out var order);
            order++;
            outputOrder[row.ThrustId] = order;
            var code = UniqueCode(row.OutputIdNumber, "OUTPUT", row.Id, 32);
            var target = Output.Create(thrustId, code, Limit(row.Description, 4000), null, null, order);
            if (WritesWorkingState)
                _target.Outputs.Add(target);
            _run.AddInserted();
            _outputsByLegacyId[row.Id] = target.Id;
            AddMapping("LegacySpme", "Outputs", sourceKey, "plan", "Outputs", target.Id, $"{thrustId}:{code}", "thrust-code", row);
        }

        await SaveIfApplyAsync();

        var indicatorRows = await source.QueryAsync<LegacyIndicator>(
            new CommandDefinition("select * from dbo.Indicators", cancellationToken: cancellationToken));
        foreach (var row in indicatorRows)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacySpme", "Indicators", sourceKey);
            if (mapped.HasValue)
            {
                _indicatorsByLegacyId[row.Id] = mapped.Value;
                AddMapping("LegacySpme", "Indicators", sourceKey, "plan", "Indicators", mapped.Value, row.Id.ToString(), "resume-existing", row);
                continue;
            }

            if (!_outputsByLegacyId.TryGetValue(row.OutputId, out var outputId))
            {
                AddIssue("LegacySpme", "Indicators", sourceKey, "error", "output-not-found", "Indicator output could not be resolved.", row);
                continue;
            }

            var code = $"IND-{row.Id:N}"[..32];
            var target = Indicator.Create(
                outputId,
                code,
                Limit(row.Description, 4000),
                "count",
                row.Baseline,
                Convert.ToDecimal(row.Target, CultureInfo.InvariantCulture),
                LimitOptional(row.OVI, 2000),
                LegacyValueParser.ParseDate(row.DueDate));
            if (WritesWorkingState)
                _target.Indicators.Add(target);
            _run.AddInserted();
            _indicatorsByLegacyId[row.Id] = target.Id;
            AddMapping("LegacySpme", "Indicators", sourceKey, "plan", "Indicators", target.Id, $"{outputId}:{code}", "output-code", row);
        }

        await SaveIfApplyAsync();
        await ImportIndicatorMeasurementsAsync(source, cancellationToken);
    }

    private async Task ImportIndicatorMeasurementsAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = await source.QueryAsync<LegacyIndicatorMeasurement>(
            new CommandDefinition("select * from dbo.IndicatorData order by CreatedAt, Id", cancellationToken: cancellationToken));
        var stagedByIndicatorPeriod = new Dictionary<string, IndicatorMeasurement>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacySpme", "IndicatorData", sourceKey);
            if (mapped.HasValue)
            {
                AddMapping("LegacySpme", "IndicatorData", sourceKey, "plan", "IndicatorMeasurements", mapped.Value, row.IndicatorId.ToString(), "resume-existing", row);
                continue;
            }

            if (!_indicatorsByLegacyId.TryGetValue(row.IndicatorId, out var indicatorId))
            {
                AddIssue("LegacySpme", "IndicatorData", sourceKey, "error", "indicator-not-found", "Measurement indicator could not be resolved.", row);
                continue;
            }

            var instituteId = await (
                from indicator in _target.Indicators.AsNoTracking()
                join output in _target.Outputs.AsNoTracking() on indicator.OutputId equals output.Id
                join thrust in _target.Thrusts.AsNoTracking() on output.ThrustId equals thrust.Id
                where indicator.Id == indicatorId
                select thrust.InstituteId)
                .FirstOrDefaultAsync(cancellationToken);
            var period = await ResolveReportingPeriodAsync(instituteId, row.Year, row.Period, cancellationToken);
            if (period is null)
            {
                AddIssue("LegacySpme", "IndicatorData", sourceKey, "error", "invalid-reporting-period", "Measurement year/period could not be normalized without inventing dates.", row);
                continue;
            }

            var indicatorPeriodKey = $"{indicatorId:N}:{period.Id:N}";
            if (stagedByIndicatorPeriod.TryGetValue(indicatorPeriodKey, out var existingMeasurement))
            {
                existingMeasurement.Update(row.Achieved, LimitOptional(row.Remarks, 2000), null);
                _run.AddUpdated();
                AddMapping("LegacySpme", "IndicatorData", sourceKey, "plan", "IndicatorMeasurements", existingMeasurement.Id, indicatorPeriodKey, "duplicate-indicator-period-latest-wins", row);
                AddIssue("LegacySpme", "IndicatorData", sourceKey, "info", "duplicate-indicator-period-collapsed", "Multiple legacy measurements target the same V2 indicator/reporting period; the latest source row was retained and all source rows map to the same measurement.", new { row.Id, indicatorId, reportingPeriodId = period.Id });
                continue;
            }

            var target = IndicatorMeasurement.Create(
                indicatorId,
                period.Id,
                row.Achieved,
                LimitOptional(row.Remarks, 2000),
                null,
                ResolveLegacyUserId(row.Author) ?? _migrationActorUserId);
            if (WritesWorkingState)
                _target.IndicatorMeasurements.Add(target);
            stagedByIndicatorPeriod[indicatorPeriodKey] = target;
            _run.AddInserted();
            AddMapping("LegacySpme", "IndicatorData", sourceKey, "plan", "IndicatorMeasurements", target.Id, $"{indicatorId}:{period.Id}", "indicator-period", row);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportProjectsAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = await source.QueryAsync<LegacyProject>(
            new CommandDefinition("select * from dbo.Projects", cancellationToken: cancellationToken));
        foreach (var row in rows)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacySpme", "Projects", sourceKey);
            if (mapped.HasValue)
            {
                _projectsByLegacyId[row.Id] = mapped.Value;
                AddMapping("LegacySpme", "Projects", sourceKey, "projects", "Projects", mapped.Value, row.Name, "resume-existing", row);
                continue;
            }

            var instituteId = await ResolveInstituteOrNullAsync(row.Institute);
            var startDate = LegacyValueParser.ParseDate(row.StartDate);
            if (!instituteId.HasValue || !startDate.HasValue)
            {
                AddIssue("LegacySpme", "Projects", sourceKey, "error", "invalid-project-owner-or-date", "Project institute or start date could not be resolved.", row);
                continue;
            }

            var nature = NormalizeProjectNature(row.ProjectNature);
            if (nature is null && !string.IsNullOrWhiteSpace(row.ProjectNature) &&
                row.ProjectNature.Trim() is not ("N/A" or "Nil" or "null"))
            {
                AddIssue("LegacySpme", "Projects", sourceKey, "warning", "unmapped-project-nature", "Legacy project nature was retained in reconciliation only because it is not a controlled V2 value.", new { row.Id, row.ProjectNature });
            }

            var currency = NormalizeCurrency(row.Currency);
            var code = $"LEGACY-{row.Id:N}"[..32];
            var target = Project.Create(
                instituteId.Value,
                code,
                Limit(row.Name, 256),
                Limit(row.Objective, 4000),
                LimitOptional(row.Justification, 4000),
                null,
                LimitOptional(row.ExpectedResult, 4000),
                nature,
                startDate.Value,
                null,
                currency,
                Convert.ToDecimal(row.Amount, CultureInfo.InvariantCulture),
                LimitOptional(row.Innovation, 4000),
                LimitOptional(row.Impact, 4000),
                await ResolveEmployeeByExactTextAsync(row.LeadResearcher, instituteId.Value, cancellationToken),
                null);
            target.Update(
                Limit(row.Name, 256),
                Limit(row.Objective, 4000),
                LimitOptional(row.Justification, 4000),
                null,
                LimitOptional(row.ExpectedResult, 4000),
                LimitOptional(row.ActualResult, 4000),
                nature,
                startDate.Value,
                null,
                currency,
                Convert.ToDecimal(row.Amount, CultureInfo.InvariantCulture),
                LimitOptional(row.Innovation, 4000),
                LimitOptional(row.Impact, 4000),
                await ResolveEmployeeByExactTextAsync(row.LeadResearcher, instituteId.Value, cancellationToken),
                null);
            ApplyProjectStatus(target, row.Status);
            if (WritesWorkingState)
                _target.Projects.Add(target);
            _run.AddInserted();
            _projectsByLegacyId[row.Id] = target.Id;
            AddMapping("LegacySpme", "Projects", sourceKey, "projects", "Projects", target.Id, $"{instituteId}:{code}", "institute-code", row);

            if (!string.IsNullOrWhiteSpace(row.Sponsors))
            {
                var sponsor = new ProjectSponsor(
                    target.Id,
                    Limit(row.Sponsors, 256),
                    Convert.ToDecimal(row.Amount, CultureInfo.InvariantCulture),
                    currency);
                if (WritesWorkingState)
                    _target.ProjectSponsors.Add(sponsor);
            }

            if (!string.IsNullOrWhiteSpace(row.SuccessStory))
            {
                var story = SuccessStory.Create(
                    instituteId.Value,
                    target.Id,
                    null,
                    Limit($"{row.Name} success story", 256),
                    Limit(row.SuccessStory, 4000),
                    null);
                if (WritesWorkingState)
                    _target.SuccessStories.Add(story);
            }

            if (!string.IsNullOrWhiteSpace(row.FileUpload))
            {
                AddIssue("LegacySpme", "Projects", sourceKey, "warning", "legacy-file-bytes-missing", "Project file path was not copied because file bytes, checksum, ownership, and scan state are unavailable.", new { row.Id, hasLegacyPath = true });
            }
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportReportsAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = (await source.QueryAsync<LegacyReport>(
                new CommandDefinition("select * from dbo.Reports", cancellationToken: cancellationToken)))
            .OrderBy(row => LegacyRowPrecedence.From(row.CreatedAt, row.UpdatedAt, row.Id))
            .ToList();
        var resolvedRows = new List<ResolvedLegacyReport>(rows.Count);
        foreach (var row in rows)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacySpme", "Reports", sourceKey);
            if (mapped.HasValue)
            {
                _reportsByLegacyId[row.Id] = mapped.Value;
                AddMapping("LegacySpme", "Reports", sourceKey, "reporting", "Reports", mapped.Value, row.Id.ToString(), "resume-existing", row);
                continue;
            }

            var instituteId = await ResolveInstituteOrNullAsync(row.Institute);
            var reportType = NormalizeReportType(row.TypeOfReport);
            if (!instituteId.HasValue || reportType is null)
            {
                AddIssue("LegacySpme", "Reports", sourceKey, "error", "invalid-report-owner-or-type", "Report institute or type has no approved V2 mapping.", row);
                continue;
            }

            var period = await ResolveReportingPeriodAsync(instituteId.Value, row.Year, row.Period, cancellationToken);
            if (period is null)
            {
                AddIssue("LegacySpme", "Reports", sourceKey, "error", "invalid-reporting-period", "Report year/period could not be normalized.", row);
                continue;
            }

            resolvedRows.Add(new ResolvedLegacyReport(row, instituteId.Value, period, reportType));
        }

        foreach (var group in resolvedRows
                     .GroupBy(row => $"{row.InstituteId:N}:{row.Period.Id:N}:{row.ReportType}", StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            // Rows were ordered from oldest/lowest to latest/highest, so the final row is the
            // deterministic winner. The V2 contract permits only one report for this natural key.
            var candidates = group.ToList();
            var winner = candidates[^1];
            var row = winner.Row;
            var target = Report.Create(
                winner.InstituteId,
                winner.Period.Id,
                winner.ReportType,
                Limit($"{winner.Period.Name} {winner.ReportType} report", 256),
                Limit(row.Summary, 4000),
                LimitOptional(row.Abstract, 4000),
                row.KeyResults,
                LimitOptional(row.Conclusion, 4000));
            if (WritesWorkingState)
                _target.Reports.Add(target);
            _run.AddInserted();

            foreach (var candidate in candidates)
            {
                var candidateSourceKey = candidate.Row.Id.ToString();
                _reportsByLegacyId[candidate.Row.Id] = target.Id;
                if (candidate.Row.Id == winner.Row.Id)
                {
                    AddMapping(
                        "LegacySpme",
                        "Reports",
                        candidateSourceKey,
                        "reporting",
                        "Reports",
                        target.Id,
                        group.Key,
                        "period-type-latest",
                        candidate.Row);
                    continue;
                }

                _run.AddUpdated();
                AddMapping(
                    "LegacySpme",
                    "Reports",
                    candidateSourceKey,
                    "reporting",
                    "Reports",
                    target.Id,
                    group.Key,
                    "duplicate-report-period-type-latest-wins",
                    candidate.Row);
                AddIssue(
                    "LegacySpme",
                    "Reports",
                    candidateSourceKey,
                    "info",
                    "duplicate-report-period-type-collapsed",
                    "Multiple legacy reports target the same V2 institute/reporting period/type; the latest source row was retained and every source row maps to that report.",
                    new
                    {
                        collapsedSourceId = candidate.Row.Id,
                        retainedSourceId = winner.Row.Id,
                        winner.InstituteId,
                        reportingPeriodId = winner.Period.Id,
                        winner.ReportType
                    });
            }

            AddReportMetric(target.Id, "research-staff", row.ResearchStaff);
            AddReportMetric(target.Id, "publications", row.NumberOfPublications);
            AddReportMetric(target.Id, "policy-briefs", row.NumberOfPolicyBriefs);
            AddReportMetric(target.Id, "journals", row.NumberOfJounals);
            AddReportMetric(target.Id, "technical-reports", row.NumberOfTechnicalReports);
            AddReportMetric(target.Id, "papers", row.NumberOfPapers);
            AddReportMetric(target.Id, "posters", row.NumberOfPosters);
            AddReportMetric(target.Id, "other-outputs", row.Others, row.SpecifyOthers);

            if (!string.IsNullOrWhiteSpace(row.FileUri))
                AddIssue("LegacySpme", "Reports", row.Id.ToString(), "warning", "legacy-file-bytes-missing", "Report attachment path was not copied because file bytes and scan evidence are unavailable.", new { row.Id, hasLegacyPath = true });
        }

        await SaveIfApplyAsync();
    }

    private void AddReportMetric(Guid reportId, string code, int? value, string? text = null)
    {
        if (!value.HasValue && string.IsNullOrWhiteSpace(text))
            return;
        if (WritesWorkingState)
            _target.ReportMetrics.Add(new ReportMetric(reportId, code, value, text, "count"));
    }

    private async Task ImportKnowledgeAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = await source.QueryAsync<LegacyTechnology>(
            new CommandDefinition("select * from dbo.TechnologyInfo", cancellationToken: cancellationToken));
        foreach (var row in rows)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacySpme", "TechnologyInfo", sourceKey);
            if (mapped.HasValue)
            {
                _technologiesByLegacyId[row.Id] = mapped.Value;
                AddMapping("LegacySpme", "TechnologyInfo", sourceKey, "knowledge", "Technologies", mapped.Value, row.Name, "resume-existing", row);
                continue;
            }

            var instituteId = await ResolveInstituteOrNullAsync(row.Institute);
            if (!instituteId.HasValue)
            {
                AddIssue("LegacySpme", "TechnologyInfo", sourceKey, "error", "institute-not-found", "Technology institute could not be resolved.", row);
                continue;
            }

            short? year = short.TryParse(row.Year?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedYear)
                ? parsedYear
                : null;
            var code = $"TECH-{row.Id:N}"[..32];
            var lead = await ResolveEmployeeByExactTextAsync(row.LeadScientist, instituteId.Value, cancellationToken);
            var target = Technology.Create(
                instituteId.Value,
                code,
                Limit(row.Name, 256),
                Limit(row.Description, 4000),
                Limit(row.ApplicationArea, 256),
                lead,
                Limit(FirstNonBlank(row.Type, "other")!, 64),
                year,
                row.AnyIPR);
            if (WritesWorkingState)
                _target.Technologies.Add(target);
            _run.AddInserted();
            _technologiesByLegacyId[row.Id] = target.Id;
            AddMapping("LegacySpme", "TechnologyInfo", sourceKey, "knowledge", "Technologies", target.Id, $"{instituteId}:{code}", "institute-code", row);

            if (row.AnyPublication && !string.IsNullOrWhiteSpace(row.PublicationTitle))
            {
                var publication = Publication.Create(
                    instituteId.Value,
                    target.Id,
                    null,
                    Limit(row.PublicationTitle, 512),
                    LimitOptional(row.PublicationAbstract, 4000),
                    LegacyValueParser.ParseDate(row.PublishedDate),
                    "other",
                    lead,
                    LimitOptional(row.PublicationAuthor, 2000));
                if (WritesWorkingState)
                    _target.Publications.Add(publication);
            }

            if (!string.IsNullOrWhiteSpace(row.PublicationFile))
                AddIssue("LegacySpme", "TechnologyInfo", sourceKey, "warning", "legacy-file-bytes-missing", "Technology publication path was not copied because the underlying bytes and scan evidence are unavailable.", new { row.Id, hasLegacyPath = true });
        }

        await SaveIfApplyAsync();

        var publications = await source.QueryAsync<LegacyPublication>(
            new CommandDefinition("select * from dbo.Publications", cancellationToken: cancellationToken));
        foreach (var row in publications)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacySpme", "Publications", sourceKey);
            if (mapped.HasValue)
            {
                AddMapping("LegacySpme", "Publications", sourceKey, "knowledge", "Publications", mapped.Value, row.Title, "resume-existing", row);
                continue;
            }

            var instituteId = await ResolveInstituteOrNullAsync(row.Institute);
            if (!instituteId.HasValue)
            {
                AddIssue("LegacySpme", "Publications", sourceKey, "error", "institute-not-found", "Publication institute could not be resolved.", row);
                continue;
            }

            var publication = Publication.Create(
                instituteId.Value,
                _technologiesByLegacyId.GetValueOrDefault(row.TechnologyInfoId),
                null,
                Limit(row.Title, 512),
                LimitOptional(row.Abstract, 4000),
                LegacyValueParser.ParseDate(row.PublishedDate),
                "other",
                await ResolveEmployeeByExactTextAsync(row.LeadScientist, instituteId.Value, cancellationToken),
                LimitOptional(row.Author, 2000));
            if (WritesWorkingState)
                _target.Publications.Add(publication);
            _run.AddInserted();
            AddMapping("LegacySpme", "Publications", sourceKey, "knowledge", "Publications", publication.Id, $"{instituteId}:{NormalizeKey(row.Title)}", "institute-title", row);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportMemosAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = await source.QueryAsync<LegacyMemo>(
            new CommandDefinition("""
                select m.*, mi.InstituteId
                from dbo.Memos m
                left join dbo.MemoInstitutes mi on mi.MemoId = m.Id
                """, cancellationToken: cancellationToken));
        foreach (var row in rows)
        {
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacySpme", "Memos", sourceKey);
            if (mapped.HasValue)
            {
                AddMapping("LegacySpme", "Memos", sourceKey, "comms", "Memos", mapped.Value, row.Title, "resume-existing", row);
                continue;
            }

            if (!row.InstituteId.HasValue || !_institutesByLegacyId.TryGetValue(row.InstituteId.Value, out var instituteId))
            {
                AddIssue("LegacySpme", "Memos", sourceKey, "error", "institute-not-found", "Memo audience institute could not be resolved.", row);
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Body))
            {
                AddIssue("LegacySpme", "Memos", sourceKey, "warning", "legacy-file-bytes-missing", "Document memo was reconciled but not published in V2 because its file bytes and scan evidence are unavailable.", new { row.Id, row.Mode, hasLegacyPath = !string.IsNullOrWhiteSpace(row.FileUrl) });
                continue;
            }

            var result = Memo.Create(instituteId, Limit(row.Title, 256), row.Body);
            if (!result.IsSuccess)
            {
                AddIssue("LegacySpme", "Memos", sourceKey, "error", "validation-failed", result.Error!.Message, row);
                continue;
            }

            var memo = result.Value!;
            memo.RestorePublished(ResolveLegacyUserId(row.PublishedByUserId), LegacyValueParser.ParseDateTimeOffset(row.PublishedAt));
            if (WritesWorkingState)
            {
                _target.Memos.Add(memo);
                _target.MemoAudiences.Add(new MemoAudience(memo.Id, "institute", instituteId));
            }
            _run.AddInserted();
            AddMapping("LegacySpme", "Memos", sourceKey, "comms", "Memos", memo.Id, $"{instituteId}:{NormalizeKey(row.Title)}", "institute-title", row);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportNotificationsAsync(SqlConnection source, CancellationToken cancellationToken)
    {
        var rows = await source.QueryAsync<LegacyNotification>(
            new CommandDefinition(
                "select Id, UserId, Type, Title, Message, IsRead, RelatedEntityId, CreatedAt, UpdatedAt from dbo.Notifications order by CreatedAt, Id",
                cancellationToken: cancellationToken));
        var saved = 0;
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceKey = row.Id.ToString();
            var mapped = GetExistingMapping("LegacySpme", "Notifications", sourceKey);
            if (mapped.HasValue)
            {
                AddMapping("LegacySpme", "Notifications", sourceKey, "comms", "Notifications", mapped.Value, row.UserId, "resume-existing", row);
                continue;
            }

            if (!Guid.TryParse(row.UserId, out var personalInfoId) ||
                !_employeesByLegacyPersonalInfoId.TryGetValue(personalInfoId, out var employeeId) ||
                !_usersByEmployeeId.TryGetValue(employeeId, out var recipientUserId))
            {
                AddIssue("LegacySpme", "Notifications", sourceKey, "error", "recipient-not-found", "Notification recipient employee/account could not be resolved.", new { row.Id, row.UserId });
                continue;
            }

            var notification = new Notification(
                recipientUserId,
                Limit(row.Title, 256),
                Limit(row.Message, 4000));
            if (row.IsRead)
                notification.MarkRead(LegacyValueParser.ParseDateTimeOffset(row.UpdatedAt) ?? LegacyValueParser.ParseDateTimeOffset(row.CreatedAt) ?? DateTimeOffset.UtcNow);
            if (WritesWorkingState)
                _target.Notifications.Add(notification);
            _run.AddInserted();
            AddMapping("LegacySpme", "Notifications", sourceKey, "comms", "Notifications", notification.Id, recipientUserId.ToString(), "employee-account", row);

            saved++;
            if (WritesWorkingState && saved % 500 == 0)
                await _target.SaveChangesAsync(cancellationToken);
        }

        await SaveIfApplyAsync();
    }

    private async Task<ReportingPeriod?> ResolveReportingPeriodAsync(
        Guid instituteId,
        string? yearText,
        string? periodText,
        CancellationToken cancellationToken)
    {
        if (!short.TryParse(yearText?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) ||
            year is < 1900 or > 2200)
            return null;

        var period = NormalizePeriod(periodText);
        if (period is null)
            return null;

        var key = $"{instituteId:N}:{year}:{period.Code}";
        if (_reportingPeriods.TryGetValue(key, out var stagedId))
            return _target.ReportingPeriods.Local.FirstOrDefault(item => item.Id == stagedId)
                   ?? await _target.ReportingPeriods.AsNoTracking().FirstOrDefaultAsync(item => item.Id == stagedId, cancellationToken);

        var target = await _target.ReportingPeriods.FirstOrDefaultAsync(
            item => item.InstituteId == instituteId && item.Code == $"{year}-{period.Code}",
            cancellationToken);
        if (target is null)
        {
            var dates = PeriodDates(year, period.Code);
            var result = ReportingPeriod.Create(
                "institute",
                instituteId,
                $"{year}-{period.Code}",
                $"{period.Name} {year}",
                period.Type,
                dates.Start,
                dates.End,
                null);
            if (!result.IsSuccess)
                return null;

            target = result.Value!;
            if (WritesWorkingState)
                _target.ReportingPeriods.Add(target);
            _run.AddInserted();
        }

        _reportingPeriods[key] = target.Id;
        return target;
    }

    private Guid? ResolveLegacyUserId(string? legacyUserId) =>
        !string.IsNullOrWhiteSpace(legacyUserId) && _usersByLegacyId.TryGetValue(legacyUserId, out var userId)
            ? userId
            : null;

    private async Task<Guid?> ResolveEmployeeByExactTextAsync(
        string? text,
        Guid instituteId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = text.Trim().ToUpperInvariant();
        var matches = await _target.Employees.AsNoTracking()
            .Where(employee => employee.InstituteId == instituteId &&
                (employee.NormalizedStaffId == normalized ||
                 employee.NormalizedPrimaryEmail == normalized ||
                 (employee.Surname + " " + (employee.OtherNames ?? "")).ToUpper() == normalized ||
                 ((employee.OtherNames ?? "") + " " + employee.Surname).ToUpper() == normalized))
            .Select(employee => employee.Id)
            .Take(2)
            .ToListAsync(cancellationToken);
        return matches.Count == 1 ? matches[0] : null;
    }

    private void ApplyProjectStatus(Project project, string? sourceStatus)
    {
        switch (sourceStatus?.Trim().ToLowerInvariant())
        {
            case "inprogress":
            case "started":
                project.Submit();
                break;
            case "completed":
                project.Submit();
                project.MoveLifecycle("completed");
                break;
            case "canceled":
            case "cancelled":
                project.Submit();
                project.MoveLifecycle("cancelled");
                break;
        }
    }

    private async Task RecordUnsafeSourceStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var item in new[]
                 {
                     ("LegacyAuthSpme", "EmailQueues", "notification delivery queue"),
                     ("LegacyAuthSpme", "EmployeeAccessTokens", "employee access tokens"),
                     ("LegacyAuthSpme", "LeaveApprovalTokens", "leave approval tokens"),
                     ("LegacyAuthSpme", "LeaveResumptionApprovalTokens", "leave resumption approval tokens"),
                     ("LegacyAuthSpme", "SkeletalStaffApprovalTokens", "skeletal approval tokens"),
                     ("LegacyAuthSpme", "SystemLoginLocks", "login locks"),
                     ("LegacyAuthSpme", "UserVerificationChallenges", "verification challenges"),
                     ("LegacyAuthSpme", "RegistrationInvites", "registration invites"),
                     ("LegacyAuthSpme", "EmployeePushDevices", "unencrypted legacy push credentials"),
                     ("LegacySpme", "ImageUploads", "file metadata without bytes")
                 })
        {
            AddIssue(item.Item1, item.Item2, "*", "info", "unsafe-state-not-activated", $"Legacy {item.Item3} were reconciled but never activated in V2.", new { source = $"{item.Item1}.dbo.{item.Item2}" });
        }

        await SaveIfApplyAsync();
    }

    private static string? NormalizeLeaveType(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "annual" => "annual",
            "part" or "part leave" => "part",
            "sick" or "sick leave" => "sick",
            "examination" or "examination leave" => "examination",
            "maternity" or "maternity leave" => "maternity",
            "paternity" or "paternity leave" => "paternity",
            "leave of absence" or "leave-of-absence" => "leave-of-absence",
            "compassionate" or "compassionate leave" => "compassionate",
            _ => null
        };

    private static string NormalizeApprovalStage(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "hod" => "head-of-division",
            "sectionhead" => "section-head",
            "admindirector" => "admin-director",
            "director" => "institute-director",
            "corporateheadofadmin" => "corporate-head-of-admin",
            "ddg" => "ddg",
            "dg" => "dg",
            _ => "head-of-division"
        };

    private static string NormalizeHolidayPeriodStatus(string? status, bool isActive) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "open" => "open",
            "closed" => "closed",
            "finalized" => "finalized",
            "draft" => "draft",
            _ => isActive ? "open" : "draft"
        };

    private static string? NormalizeProjectNature(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "research" or "research based" or "research-based" => "research",
            "development" => "development",
            "consultancy" => "consultancy",
            "capacity building" or "capacity-building" => "capacity-building",
            "infrastructure" => "infrastructure",
            _ => null
        };

    private static string NormalizeCurrency(string? value)
    {
        var currency = value?.Trim().ToUpperInvariant();
        return currency is { Length: 3 } && currency.All(char.IsLetter) ? currency : "GHS";
    }

    private static string? NormalizeReportType(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "strategicreport" or "strategic" => "strategic",
            "rndreport" or "researchanddevelopment" or "research-and-development" => "research-and-development",
            "performancereport" or "performance" => "performance",
            "projectreport" or "project" => "project",
            "hrreport" or "hr" => "hr",
            _ => null
        };

    private static NormalizedPeriod? NormalizePeriod(string? value)
    {
        var normalized = new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return normalized switch
        {
            "annual" => new("ANNUAL", "Annual", "annual"),
            "firstquarter" => new("Q1", "First Quarter", "quarterly"),
            "secondquarter" => new("Q2", "Second Quarter", "quarterly"),
            "thirdquarter" => new("Q3", "Third Quarter", "quarterly"),
            "fourthquarter" or "fourthquater" => new("Q4", "Fourth Quarter", "quarterly"),
            _ => null
        };
    }

    private static (DateTime Start, DateTime End) PeriodDates(short year, string code) =>
        code switch
        {
            "Q1" => (new DateTime(year, 1, 1), new DateTime(year, 3, 31)),
            "Q2" => (new DateTime(year, 4, 1), new DateTime(year, 6, 30)),
            "Q3" => (new DateTime(year, 7, 1), new DateTime(year, 9, 30)),
            "Q4" => (new DateTime(year, 10, 1), new DateTime(year, 12, 31)),
            _ => (new DateTime(year, 1, 1), new DateTime(year, 12, 31))
        };

    private static string CodeFromText(string value, int length)
    {
        var code = new string(value.Trim().ToUpperInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());
        while (code.Contains("--", StringComparison.Ordinal))
            code = code.Replace("--", "-", StringComparison.Ordinal);
        code = code.Trim('-');
        return Limit(string.IsNullOrWhiteSpace(code) ? "LEGACY" : code, length);
    }

    private static string UniqueCode(string? source, string prefix, int id, int length)
    {
        var code = CodeFromText(FirstNonBlank(source, $"{prefix}-{id}")!, length);
        return Limit($"{code}-{id}", length);
    }

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string? LimitOptional(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : Limit(value.Trim(), maxLength);

    private sealed record NormalizedPeriod(string Code, string Name, string Type);
    private sealed record LegacyPositionType(Guid Id, string Name, int AnnualLeaveDays);
    private sealed class LegacyLeaveBalance
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
        public int? LeaveType { get; init; }
        public int Year { get; init; }
        public int TotalDays { get; init; }
        public int UsedDays { get; init; }
        public int RemainingDays { get; init; }
    }
    private sealed record LegacyHoliday(Guid Id, string Name, DateTime Date, bool IsFullDay, bool IsIslamic, string? Notes);
    private sealed class LegacyHolidayPeriod
    {
        public Guid Id { get; init; }
        public int Year { get; init; }
        public DateTime ChristmasStartDate { get; init; }
        public DateTime ChristmasEndDate { get; init; }
        public DateTime NewYearStartDate { get; init; }
        public DateTime NewYearEndDate { get; init; }
        public bool IsActive { get; init; }
        public string? Notes { get; init; }
        public DateTime AvailabilityStartDate { get; init; }
        public DateTime AvailabilityEndDate { get; init; }
        public int DeductionDays { get; init; }
        public string? FinalizedAt { get; init; }
        public Guid? FinalizedByUserId { get; init; }
        public string? InstituteCode { get; init; }
        public string? Status { get; init; }
    }
    private sealed record LegacyCompassionateLeaveType(Guid Id, string Name, int Days, bool DoesNotDeductFromBalance);
    private sealed class LegacyLeaveRequest
    {
        public Guid Id { get; init; }
        public int NumberOfDays { get; init; }
        public string StartDate { get; init; } = string.Empty;
        public string EndDate { get; init; } = string.Empty;
        public string LeaveType { get; init; } = string.Empty;
        public string? Status { get; init; }
        public bool Approved { get; init; }
        public string? Comment { get; init; }
        public Guid UserId { get; init; }
        public string? CompletedAt { get; init; }
        public Guid? DelegateUserId { get; init; }
        public string? HandoverNotes { get; init; }
        public string? RejectionReason { get; init; }
        public string? SubmittedAt { get; init; }
        public string? MedicalDocumentUrl { get; init; }
        public string? AdmissionLetterUrl { get; init; }
        public string? HandoverNotesDocumentUrl { get; init; }
    }
    private sealed class LegacyLeaveApproval
    {
        public Guid Id { get; init; }
        public Guid LeaveRequestId { get; init; }
        public string ApproverUserId { get; init; } = string.Empty;
        public string ApprovalStage { get; init; } = string.Empty;
        public bool IsApproved { get; init; }
        public string? Comments { get; init; }
        public string? Signature { get; init; }
        public string? ApprovedAt { get; init; }
    }
    private sealed record LegacyLeaveHandover(Guid Id, Guid LeaveRequestId, string HandoverNotes, Guid? DelegateUserId);
    private sealed class LegacyLeaveResumption
    {
        public Guid Id { get; init; }
        public Guid LeaveRequestId { get; init; }
        public Guid EmployeeId { get; init; }
        public string ResumptionDate { get; init; } = string.Empty;
        public string? EmployeeSignature { get; init; }
        public string Status { get; init; } = string.Empty;
    }
    private sealed class LegacyThrust
    {
        public int Id { get; init; }
        public string UID { get; init; } = string.Empty;
        public int InstituteId { get; init; }
        public string Description { get; init; } = string.Empty;
        public string Objective { get; init; } = string.Empty;
    }
    private sealed record LegacyOutput(int Id, string? OutputIdNumber, string Description, int ThrustId);
    private sealed class LegacyIndicator
    {
        public Guid Id { get; init; }
        public int OutputId { get; init; }
        public string Description { get; init; } = string.Empty;
        public int Baseline { get; init; }
        public double Target { get; init; }
        public string OVI { get; init; } = string.Empty;
        public string DueDate { get; init; } = string.Empty;
    }
    private sealed class LegacyIndicatorMeasurement
    {
        public Guid Id { get; init; }
        public Guid IndicatorId { get; init; }
        public int Achieved { get; init; }
        public string Period { get; init; } = string.Empty;
        public string Year { get; init; } = string.Empty;
        public string? Remarks { get; init; }
        public string? Author { get; init; }
    }
    private sealed class LegacyProject
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Author { get; init; } = string.Empty;
        public double Amount { get; init; }
        public string Sponsors { get; init; } = string.Empty;
        public string LeadResearcher { get; init; } = string.Empty;
        public string StartDate { get; init; } = string.Empty;
        public string? Justification { get; init; }
        public string Objective { get; init; } = string.Empty;
        public string? ExpectedResult { get; init; }
        public string? ActualResult { get; init; }
        public string Status { get; init; } = string.Empty;
        public string? Innovation { get; init; }
        public string? Impact { get; init; }
        public string? FileUpload { get; init; }
        public string? SuccessStory { get; init; }
        public string Institute { get; init; } = string.Empty;
        public string? Currency { get; init; }
        public string? ProjectNature { get; init; }
    }
    private sealed class LegacyReport
    {
        public Guid Id { get; init; }
        public string? CreatedAt { get; init; }
        public string? UpdatedAt { get; init; }
        public string Period { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public string? Abstract { get; init; }
        public string? KeyResults { get; init; }
        public string? Year { get; init; }
        public string? Conclusion { get; init; }
        public string TypeOfReport { get; init; } = string.Empty;
        public string? FileUri { get; init; }
        public string Institute { get; init; } = string.Empty;
        public int? ResearchStaff { get; init; }
        public int? NumberOfPublications { get; init; }
        public int? NumberOfPolicyBriefs { get; init; }
        public int? NumberOfJounals { get; init; }
        public int? NumberOfTechnicalReports { get; init; }
        public int? NumberOfPapers { get; init; }
        public int? NumberOfPosters { get; init; }
        public int? Others { get; init; }
        public string? SpecifyOthers { get; init; }
    }
    private sealed record ResolvedLegacyReport(
        LegacyReport Row,
        Guid InstituteId,
        ReportingPeriod Period,
        string ReportType);
    private sealed class LegacyTechnology
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string ApplicationArea { get; init; } = string.Empty;
        public string? Year { get; init; }
        public string? LeadScientist { get; init; }
        public string? Type { get; init; }
        public bool AnyPublication { get; init; }
        public string? PublicationAuthor { get; init; }
        public string? PublicationTitle { get; init; }
        public string Institute { get; init; } = string.Empty;
        public bool AnyIPR { get; init; }
        public string? PublishedDate { get; init; }
        public string? PublicationAbstract { get; init; }
        public string? PublicationFile { get; init; }
    }
    private sealed class LegacyPublication
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Author { get; init; } = string.Empty;
        public string Abstract { get; init; } = string.Empty;
        public string Institute { get; init; } = string.Empty;
        public Guid TechnologyInfoId { get; init; }
        public string PublishedDate { get; init; } = string.Empty;
        public string? LeadScientist { get; init; }
    }
    private sealed class LegacyMemo
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Mode { get; init; } = string.Empty;
        public string? Body { get; init; }
        public string? FileUrl { get; init; }
        public string PublishedByUserId { get; init; } = string.Empty;
        public string PublishedAt { get; init; } = string.Empty;
        public Guid? TargetEmployeeId { get; init; }
        public int? InstituteId { get; init; }
    }
    private sealed record LegacyNotification(
        Guid Id,
        string UserId,
        string Type,
        string Title,
        string Message,
        bool IsRead,
        string? RelatedEntityId,
        string? CreatedAt,
        string? UpdatedAt);
}
