using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Comms;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Csir.Spme.Infrastructure.Communications;

public sealed class WorkflowNotificationOutbox(
    SpmeDbContext db,
    BrandedEmailRenderer renderer,
    IWorkflowApprovalTokenService tokenService,
    IWorkflowApproverResolver approverResolver) : IWorkflowNotificationOutbox
{
    private const string ReportSubmittedEventType = "report.submitted.v1";
    private const string LeaveApprovedEventType = "leave.approved.v1";
    private const string LeaveRejectedEventType = "leave.rejected.v1";

    public async Task StageAppraisalNoticeAsync(
        Guid appraisalId,
        Guid recipientUserId,
        string eventName,
        string title,
        string message,
        string idempotencySuffix,
        CancellationToken ct = default)
    {
        var eventType = $"appraisal.{eventName}.v1";
        await StageAsync(new CommunicationOutboxMessage(
            "event", recipientUserId.ToString("N"), eventType,
            JsonSerializer.Serialize(new { eventType, appraisalId, recipientUserId }),
            false, $"appraisal-{eventName}", $"appraisal-{eventName}:{appraisalId:N}:{idempotencySuffix}"), ct);
        db.Notifications.Add(new Notification(recipientUserId, title, message, $"/appraisals/{appraisalId:D}"));

        var recipient = await db.Users.AsNoTracking()
            .Where(user => user.Id == recipientUserId && user.AccountStatus == "active")
            .Select(user => new { user.DisplayName, user.Email, user.PhoneNumber, user.EmployeeId })
            .FirstOrDefaultAsync(ct);
        if (recipient is null) return;

        var employeeContact = recipient.EmployeeId.HasValue
            ? await db.Employees.AsNoTracking()
                .Where(employee => employee.Id == recipient.EmployeeId.Value)
                .Select(employee => new { Email = employee.PrimaryEmail, employee.Phone })
                .FirstOrDefaultAsync(ct)
            : null;
        var email = FirstDeliverableEmail(recipient.Email, employeeContact?.Email);
        var phone = FirstDeliverablePhone(recipient.PhoneNumber, employeeContact?.Phone);
        var category = $"appraisal-{eventName}";
        if (email is not null)
        {
            var rendered = renderer.AppraisalNotice(recipient.DisplayName, title, message, appraisalId);
            await StageEmailAsync(email, rendered.Subject, rendered.HtmlBody, rendered.TextBody, category,
                $"{category}-email:{appraisalId:N}:{idempotencySuffix}:{DestinationKey(email)}", ct);
        }
        if (phone is not null)
        {
            var link = $"{renderer.StaffPortalUrl.TrimEnd('/')}/appraisals/{appraisalId:D}";
            await StageAsync(new CommunicationOutboxMessage(
                "sms", phone, null, $"CSIR: {message} Sign in: {link}", false, category,
                $"{category}-sms:{appraisalId:N}:{idempotencySuffix}:{DestinationKey(phone)}"), ct);
        }
    }

    public async Task StageStaffQuarterlyReportSubmittedAsync(
        StaffQuarterlyReportNotification notification,
        CancellationToken ct = default)
    {
        var occurrence = notification.SubmittedAt.UtcDateTime.Ticks;
        await StageAsync(new CommunicationOutboxMessage(
            "event",
            notification.ReviewerUserId.ToString("N"),
            "staff-quarterly-report.submitted.v1",
            JsonSerializer.Serialize(new
            {
                eventType = "staff-quarterly-report.submitted.v1",
                reportId = notification.ReportId,
                instituteId = notification.InstituteId,
                ownerEmployeeId = notification.OwnerEmployeeId,
                reviewerUserId = notification.ReviewerUserId
            }),
            false,
            "staff-quarterly-report-submitted",
            $"staff-quarterly-report-submitted:{notification.ReportId:N}:{occurrence}"), ct);

        var link = $"/reports/{notification.ReportId:D}";
        db.Notifications.Add(new Notification(
            notification.ReviewerUserId,
            "Quarterly report submitted",
            $"{notification.StaffDisplayName} submitted {notification.PeriodName} for your review.",
            link));

        var email = renderer.StaffQuarterlyReportSubmitted(notification);
        var pdf = StaffQuarterlyReportPdf.Build(notification);
        var attachmentsJson = JsonSerializer.Serialize(new[]
        {
            new EmailAttachment(
                "staff-quarterly-report.pdf",
                "application/pdf",
                Convert.ToBase64String(pdf))
        });
        await StageEmailAsync(notification.ReviewerEmail, email.Subject, email.HtmlBody, email.TextBody,
            "staff-quarterly-report-submitted",
            $"staff-quarterly-report-email:{notification.ReportId:N}:{DestinationKey(notification.ReviewerEmail)}",
            ct, attachmentsJson);

        var portalLink = $"{renderer.StaffPortalUrl.TrimEnd('/')}{link}";
        await StageAsync(new CommunicationOutboxMessage(
            "sms", notification.ReviewerPhone, null,
            $"{notification.StaffDisplayName} submitted a quarterly report for {notification.PeriodName}. Check it on the staff portal: {portalLink}",
            false, "staff-quarterly-report-submitted",
            $"staff-quarterly-report-sms:{notification.ReportId:N}:{DestinationKey(notification.ReviewerPhone)}"), ct);
    }

    public async Task StageReportSubmittedAsync(
        Guid reportId,
        Guid instituteId,
        Guid submittedByUserId,
        DateTimeOffset submittedAt,
        string title,
        CancellationToken ct = default)
    {
        var occurrence = submittedAt.UtcDateTime.Ticks;
        await StageAsync(new CommunicationOutboxMessage(
            "event",
            instituteId.ToString("N"),
            ReportSubmittedEventType,
            JsonSerializer.Serialize(new
            {
                eventType = ReportSubmittedEventType,
                reportId,
                instituteId,
                submittedByUserId
            }),
            false,
            "report-submitted",
            $"report-submitted:{reportId:N}:{occurrence}"), ct);

        var recipients = await (
            from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.Email != null &&
                  (user.InstituteId == instituteId || user.InstituteId == null) &&
                  (role.Name == "ReportsAdmin" || role.Name == "PlatformAdmin")
            select user.Email)
            .Distinct()
            .ToListAsync(ct);

        foreach (var recipient in recipients)
        {
            await StageEmailAsync(
                recipient!,
                "Report submitted for review",
                $"The report \"{title}\" has been submitted for review.",
                "report-submitted",
                $"report-submitted:{reportId:N}:{occurrence}:{recipient!.ToUpperInvariant()}",
                ct);
        }
    }

    public async Task StageLeaveDecisionAsync(
        Guid leaveRequestId,
        Guid instituteId,
        Guid employeeId,
        Guid decidedByUserId,
        string decision,
        CancellationToken ct = default)
    {
        var eventType = decision switch
        {
            "approved" => LeaveApprovedEventType,
            "rejected" => LeaveRejectedEventType,
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unsupported leave decision.")
        };

        await StageAsync(new CommunicationOutboxMessage(
            "event",
            employeeId.ToString("N"),
            eventType,
            JsonSerializer.Serialize(new
            {
                eventType,
                leaveRequestId,
                instituteId,
                employeeId,
                decidedByUserId,
                decision
            }),
            false,
            $"leave-{decision}",
            $"leave-{decision}:{leaveRequestId:N}"), ct);

        var owner = await FindEmployeeUserAsync(employeeId, ct);
        if (owner is not null)
        {
            db.Notifications.Add(new Notification(
                owner.UserId,
                decision == "approved" ? "Leave request approved" : "Leave request rejected",
                $"Your leave request was {decision}.",
                $"/leave/{leaveRequestId:D}"));
        }

        var request = db.LeaveRequests.Local.FirstOrDefault(candidate =>
            candidate.Id == leaveRequestId &&
            candidate.InstituteId == instituteId &&
            candidate.EmployeeId == employeeId);
        request ??= await db.LeaveRequests.SingleAsync(candidate =>
            candidate.Id == leaveRequestId &&
            candidate.InstituteId == instituteId &&
            candidate.EmployeeId == employeeId, ct);
        var displayName = owner?.DisplayName ?? await EmployeeDisplayNameAsync(employeeId, ct);
        var recipient = owner?.Email ?? await EmployeeEmailAsync(employeeId, ct);
        if (recipient is null)
            return;

        var rendered = renderer.LeaveDecision(
            displayName,
            decision,
            request.LeaveType,
            request.StartDate,
            request.EndDate,
            request.WorkingDays,
            request.RejectionReason,
            leaveRequestId);
        await StageEmailAsync(
            recipient,
            rendered.Subject,
            rendered.HtmlBody,
            rendered.TextBody,
            $"leave-{decision}",
            $"leave-{decision}:{leaveRequestId:N}:{DestinationKey(recipient)}",
            ct);
    }

    public async Task StageLeaveAwaitingApprovalAsync(
        Guid leaveRequestId,
        Guid instituteId,
        Guid employeeId,
        string approvalStage,
        string leaveType,
        DateTime startDate,
        DateTime endDate,
        decimal workingDays,
        CancellationToken ct = default)
    {
        var staffName = await db.Users.AsNoTracking()
            .Where(user => user.EmployeeId == employeeId && user.DisplayName != "")
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(staffName))
        {
            var employee = await db.Employees.AsNoTracking()
                .Where(candidate => candidate.Id == employeeId)
                .Select(candidate => new { candidate.PreferredName, candidate.OtherNames, candidate.Surname })
                .FirstOrDefaultAsync(ct);
            staffName = employee?.PreferredName ??
                string.Join(' ', new[] { employee?.OtherNames, employee?.Surname }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        foreach (var approver in await approverResolver.FindStageApproversAsync(instituteId, employeeId, approvalStage, ct))
        {
            var issued = await tokenService.IssueAsync(
                WorkflowApprovalPurposes.Leave,
                leaveRequestId,
                approver.UserId,
                approvalStage,
                ct);
            db.Notifications.Add(new Notification(
                approver.UserId,
                "Leave request awaiting approval",
                $"{staffName} submitted a leave request that needs your review.",
                $"/approvals/leave?token={issued.RawToken}"));
            var rendered = renderer.LeaveAwaitingApproval(
                approver.DisplayName,
                staffName,
                approvalStage,
                leaveType,
                startDate,
                endDate,
                workingDays,
                leaveRequestId,
                issued.RawToken);
            await StageEmailAsync(
                approver.Email,
                rendered.Subject,
                rendered.HtmlBody,
                rendered.TextBody,
                "leave-pending-approval",
                $"leave-pending-approval:{leaveRequestId:N}:{approvalStage}:{DestinationKey(approver.Email)}",
                ct);

            if (!string.IsNullOrWhiteSpace(approver.Phone))
            {
                var portalLink = $"{renderer.StaffPortalUrl.TrimEnd('/')}/approvals/leave";
                await StageAsync(new CommunicationOutboxMessage(
                    "sms", approver.Phone, null,
                    $"{staffName} submitted a leave request for your approval. Sign in on the staff portal: {portalLink}",
                    false, "leave-pending-approval",
                    $"leave-pending-approval-sms:{leaveRequestId:N}:{approvalStage}:{DestinationKey(approver.Phone)}"), ct);
            }
        }
    }

    public async Task StageLeaveOwnerNoticeAsync(
        Guid leaveRequestId,
        Guid instituteId,
        Guid employeeId,
        string eventName,
        string title,
        string body,
        CancellationToken ct = default)
    {
        var owner = await FindEmployeeUserAsync(employeeId, ct);
        if (owner is null)
            return;
        db.Notifications.Add(new Notification(owner.UserId, title, body, $"/leave/{leaveRequestId:D}"));
        await StageAsync(new CommunicationOutboxMessage(
            "event",
            employeeId.ToString("N"),
            $"leave.{eventName}.v1",
            JsonSerializer.Serialize(new
            {
                eventType = $"leave.{eventName}.v1",
                leaveRequestId,
                instituteId,
                employeeId
            }),
            false,
            $"leave-{eventName}",
            $"leave-{eventName}:{leaveRequestId:N}"), ct);
    }

    public async Task StageStaffQuarterlyReportReviewedAsync(
        Guid reportId,
        Guid instituteId,
        Guid ownerEmployeeId,
        string periodName,
        string title,
        string outcome,
        string? returnReason,
        CancellationToken ct = default)
    {
        var owner = await FindEmployeeUserAsync(ownerEmployeeId, ct);
        if (owner is null)
            return;

        var link = $"/reports/{reportId:D}";
        var heading = outcome == "returned" ? "Quarterly report returned" : "Quarterly report approved";
        var body = outcome == "returned"
            ? $"Your {periodName} report \"{title}\" was returned for correction."
            : $"Your {periodName} report \"{title}\" was approved.";
        db.Notifications.Add(new Notification(owner.UserId, heading, body, link));

        var rendered = renderer.StaffQuarterlyReportReviewed(
            owner.DisplayName, periodName, title, outcome, returnReason, reportId);
        if (!string.IsNullOrWhiteSpace(owner.Email))
        {
            await StageEmailAsync(owner.Email, rendered.Subject, rendered.HtmlBody, rendered.TextBody,
                $"staff-quarterly-report-{outcome}",
                $"staff-quarterly-report-{outcome}-email:{reportId:N}:{DestinationKey(owner.Email)}", ct);
        }

        if (!string.IsNullOrWhiteSpace(owner.Phone))
        {
            var portalLink = $"{renderer.StaffPortalUrl.TrimEnd('/')}{link}";
            await StageAsync(new CommunicationOutboxMessage(
                "sms", owner.Phone, null,
                outcome == "returned"
                    ? $"Your quarterly report for {periodName} was returned. Review: {portalLink}"
                    : $"Your quarterly report for {periodName} was approved. Open: {portalLink}",
                false, $"staff-quarterly-report-{outcome}",
                $"staff-quarterly-report-{outcome}-sms:{reportId:N}:{DestinationKey(owner.Phone)}"), ct);
        }
    }

    public async Task StageMemoPublishedAsync(
        Guid memoId,
        Guid instituteId,
        string title,
        string emailBody,
        string smsSynopsis,
        IReadOnlyList<MemoChannelRecipient> recipients,
        CancellationToken ct = default)
    {
        await StageAsync(new CommunicationOutboxMessage(
            "event",
            instituteId.ToString("N"),
            "memo.published.v1",
            JsonSerializer.Serialize(new
            {
                eventType = "memo.published.v1",
                memoId,
                instituteId,
                recipientCount = recipients.Count
            }),
            false,
            "memo-published",
            $"memo-published:{memoId:N}"), ct);

        foreach (var recipient in recipients)
        {
            if (recipient.SendEmail && !string.IsNullOrWhiteSpace(recipient.Email))
            {
                var email = recipient.Email.Trim();
                await StageEmailAsync(
                    email,
                    title,
                    emailBody,
                    "memo",
                    MemoChannelKey(memoId, "email", recipient.UserId, email),
                    ct);
            }

            if (recipient.SendSms && !string.IsNullOrWhiteSpace(recipient.Phone))
            {
                var phone = recipient.Phone.Trim();
                await StageAsync(new CommunicationOutboxMessage(
                    "sms",
                    phone,
                    null,
                    smsSynopsis,
                    false,
                    "memo",
                    MemoChannelKey(memoId, "sms", recipient.UserId, phone)), ct);
            }
        }
    }

    public async Task StageHrApprovalAccessAsync(
        Guid employeeId,
        Guid instituteId,
        CancellationToken ct = default)
    {
        var employee = await db.Employees
            .Where(candidate => candidate.Id == employeeId && candidate.InstituteId == instituteId)
            .Select(candidate => new
            {
                candidate.StaffId,
                candidate.Prefix,
                candidate.PreferredName,
                candidate.OtherNames,
                candidate.Surname,
                candidate.PrimaryEmail,
                candidate.Phone
            })
            .FirstOrDefaultAsync(ct);
        if (employee is null)
            return;

        await StageAsync(new CommunicationOutboxMessage(
            "event",
            employeeId.ToString("N"),
            "hr.employee-approved.v1",
            JsonSerializer.Serialize(new
            {
                eventType = "hr.employee-approved.v1",
                employeeId,
                instituteId
            }),
            false,
            "hr-approval",
            $"hr-approval:{employeeId:N}"), ct);

        var displayName = employee.PreferredName
            ?? string.Join(' ', new[] { employee.Prefix, employee.OtherNames, employee.Surname }
                .Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "Staff member";

        var owner = await FindEmployeeUserAsync(employeeId, ct);
        if (owner is not null)
        {
            db.Notifications.Add(new Notification(
                owner.UserId,
                "Staff portal access approved",
                "Your staff record has been approved. Sign in to the staff portal for leave, reports, and more.",
                "/"));
        }

        var rendered = renderer.HrApprovalAccess(displayName, employee.StaffId);
        var email = FirstDeliverableEmail(employee.PrimaryEmail, owner?.Email);
        if (email is not null)
        {
            await StageEmailAsync(
                email,
                rendered.Subject,
                rendered.HtmlBody,
                rendered.TextBody,
                "hr-approval",
                $"hr-approval-email:{employeeId:N}:{DestinationKey(email)}",
                ct);
        }

        var phone = FirstDeliverablePhone(employee.Phone, owner?.Phone);
        if (phone is not null)
        {
            await StageAsync(new CommunicationOutboxMessage(
                "sms",
                phone,
                null,
                renderer.HrApprovalAccessSms(),
                false,
                "hr-approval",
                $"hr-approval-sms:{employeeId:N}:{DestinationKey(phone)}"), ct);
        }
    }

    public async Task StageSkeletalStaffAwaitingApprovalAsync(
        Guid requestId,
        Guid instituteId,
        Guid employeeId,
        string approvalStage,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken ct = default)
    {
        var staffName = await EmployeeDisplayNameAsync(employeeId, ct);
        foreach (var approver in await approverResolver.FindStageApproversAsync(instituteId, employeeId, approvalStage, ct))
        {
            var issued = await tokenService.IssueAsync(
                WorkflowApprovalPurposes.SkeletalStaff,
                requestId,
                approver.UserId,
                approvalStage,
                ct);
            db.Notifications.Add(new Notification(
                approver.UserId,
                "Skeletal staff request awaiting approval",
                $"{staffName} submitted a skeletal staff request that needs your review.",
                $"/approvals/skeletal-staff?token={issued.RawToken}"));
            var rendered = renderer.SkeletalStaffAwaitingApproval(
                approver.DisplayName,
                staffName,
                approvalStage,
                periodStart,
                periodEnd,
                requestId,
                issued.RawToken);
            await StageEmailAsync(
                approver.Email,
                rendered.Subject,
                rendered.HtmlBody,
                rendered.TextBody,
                "skeletal-staff-pending-approval",
                $"skeletal-staff-pending-approval:{requestId:N}:{approvalStage}:{DestinationKey(approver.Email)}",
                ct);

            if (!string.IsNullOrWhiteSpace(approver.Phone))
            {
                var portalLink = $"{renderer.StaffPortalUrl.TrimEnd('/')}/approvals/skeletal-staff";
                await StageAsync(new CommunicationOutboxMessage(
                    "sms", approver.Phone, null,
                    $"{staffName} submitted a skeletal staff request for your approval. Sign in on the staff portal: {portalLink}",
                    false, "skeletal-staff-pending-approval",
                    $"skeletal-staff-pending-approval-sms:{requestId:N}:{approvalStage}:{DestinationKey(approver.Phone)}"), ct);
            }
        }
    }

    public async Task StageSkeletalStaffDecisionAsync(
        Guid requestId,
        Guid instituteId,
        Guid employeeId,
        string decision,
        string? reason,
        CancellationToken ct = default)
    {
        var owner = await FindEmployeeUserAsync(employeeId, ct);
        if (owner is null)
            return;

        var request = db.SkeletalStaffRequests.Local.FirstOrDefault(candidate => candidate.Id == requestId)
            ?? await db.SkeletalStaffRequests.AsNoTracking().SingleAsync(candidate => candidate.Id == requestId, ct);
        db.Notifications.Add(new Notification(
            owner.UserId,
            decision == "approved" ? "Skeletal staff request approved" : "Skeletal staff request rejected",
            $"Your skeletal staff request was {decision}.",
            $"/skeletal-staff/{requestId:D}"));

        var recipient = owner.Email ?? await EmployeeEmailAsync(employeeId, ct);
        if (recipient is null)
            return;

        var rendered = renderer.SkeletalStaffDecision(
            owner.DisplayName,
            decision,
            request.SelectedStartDate ?? DateTime.MinValue,
            request.SelectedEndDate ?? DateTime.MinValue,
            reason,
            requestId);
        await StageEmailAsync(
            recipient,
            rendered.Subject,
            rendered.HtmlBody,
            rendered.TextBody,
            $"skeletal-staff-{decision}",
            $"skeletal-staff-{decision}:{requestId:N}:{DestinationKey(recipient)}",
            ct);
    }

    public async Task StageSkeletalStaffServiceReportAsync(
        SkeletalStaffServiceReportNotification notification,
        CancellationToken ct = default)
    {
        db.Notifications.Add(new Notification(
            notification.RecipientUserId,
            "Skeletal staff service report",
            $"{notification.StaffDisplayName} completed skeletal staff service for {notification.PeriodName}.",
            $"/skeletal-staff/{notification.RequestId:D}"));

        var rendered = renderer.SkeletalStaffServiceReport(
            notification.RecipientDisplayName,
            notification.StaffDisplayName,
            notification.PeriodName,
            notification.RequestId);
        var attachmentsJson = notification.AttachPdf
            ? System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new EmailAttachment(
                    "skeletal-staff-service-report.pdf",
                    "application/pdf",
                    Convert.ToBase64String(notification.PdfContent))
            })
            : null;
        await StageEmailAsync(
            notification.RecipientEmail,
            rendered.Subject,
            rendered.HtmlBody,
            rendered.TextBody,
            "skeletal-staff-service-report",
            $"skeletal-staff-service-report:{notification.RequestId:N}:{DestinationKey(notification.RecipientEmail)}",
            ct,
            attachmentsJson);

        if (!string.IsNullOrWhiteSpace(notification.RecipientPhone))
        {
            var portalLink = $"{renderer.StaffPortalUrl.TrimEnd('/')}/skeletal-staff/{notification.RequestId:D}";
            await StageAsync(new CommunicationOutboxMessage(
                "sms", notification.RecipientPhone, null,
                $"{notification.StaffDisplayName} completed skeletal staff service for {notification.PeriodName}. Review: {portalLink}",
                false, "skeletal-staff-service-report",
                $"skeletal-staff-service-report-sms:{notification.RequestId:N}:{DestinationKey(notification.RecipientPhone)}"), ct);
        }
    }

    private async Task<EmployeeUser?> FindEmployeeUserAsync(
        Guid employeeId, CancellationToken ct)
    {
        return await db.Users.AsNoTracking()
            .Where(candidate => candidate.EmployeeId == employeeId)
            .Select(candidate => new EmployeeUser(candidate.Id, candidate.DisplayName, candidate.Email, candidate.PhoneNumber))
            .FirstOrDefaultAsync(ct);
    }

    private sealed record EmployeeUser(Guid UserId, string DisplayName, string? Email, string? Phone);

    private async Task<string> EmployeeDisplayNameAsync(Guid employeeId, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking()
            .Where(candidate => candidate.Id == employeeId)
            .Select(candidate => new { candidate.PreferredName, candidate.OtherNames, candidate.Surname })
            .FirstOrDefaultAsync(ct);
        return employee?.PreferredName ??
            string.Join(' ', new[] { employee?.OtherNames, employee?.Surname }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private Task<string?> EmployeeEmailAsync(Guid employeeId, CancellationToken ct) =>
        db.Employees.AsNoTracking()
            .Where(candidate => candidate.Id == employeeId)
            .Select(candidate => candidate.PrimaryEmail)
            .FirstOrDefaultAsync(ct);

    private static string MemoChannelKey(Guid memoId, string channel, Guid? userId, string destination)
    {
        var identity = userId?.ToString("N") ?? destination.ToUpperInvariant();
        if (identity.Length > 48)
            identity = identity[..48];
        return $"memo-{channel}:{memoId:N}:{identity}";
    }

    private static string DestinationKey(string destination) =>
        Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(destination.Trim().ToUpperInvariant())));

    private static string? FirstDeliverableEmail(params string?[] candidates) =>
        candidates.Select(IsDeliverableEmail).FirstOrDefault(value => value is not null);

    private static string? FirstDeliverablePhone(params string?[] candidates) =>
        candidates.Select(IsDeliverablePhone).FirstOrDefault(value => value is not null);

    private static string? IsDeliverableEmail(string? value)
    {
        var email = value?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.IndexOf('@') <= 0)
            return null;
        var normalized = email.ToLowerInvariant();
        if (normalized.EndsWith("@pending.csir.local", StringComparison.Ordinal) ||
            normalized.EndsWith("@invalid", StringComparison.Ordinal) ||
            normalized.EndsWith("@example.invalid", StringComparison.Ordinal) ||
            normalized.Contains("placeholder", StringComparison.Ordinal))
            return null;
        return email;
    }

    private static string? IsDeliverablePhone(string? value)
    {
        var phone = value?.Trim();
        if (string.IsNullOrWhiteSpace(phone) || phone.Contains("000000", StringComparison.Ordinal))
            return null;
        return phone;
    }

    private Task StageEmailAsync(
        string recipient,
        string subject,
        string body,
        string category,
        string idempotencyKey,
        CancellationToken ct) =>
        StageEmailAsync(recipient, subject, body, null, category, idempotencyKey, ct);

    private Task StageEmailAsync(
        string recipient,
        string subject,
        string body,
        string? textBody,
        string category,
        string idempotencyKey,
        CancellationToken ct,
        string? attachmentsJson = null) =>
        StageAsync(new CommunicationOutboxMessage(
            "email", recipient, subject, body, textBody is not null, category, idempotencyKey, textBody,
            attachmentsJson), ct);

    private async Task StageAsync(CommunicationOutboxMessage message, CancellationToken ct)
    {
        if (await db.CommunicationOutboxMessages.AnyAsync(
                candidate => candidate.IdempotencyKey == message.IdempotencyKey, ct) ||
            db.CommunicationOutboxMessages.Local.Any(
                local => local.IdempotencyKey == message.IdempotencyKey))
            return;

        db.CommunicationOutboxMessages.Add(message);
    }
}
