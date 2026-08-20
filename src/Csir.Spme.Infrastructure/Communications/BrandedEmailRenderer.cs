using System.Net;
using System.Text;
using Csir.Spme.Application.Common.Interfaces;

namespace Csir.Spme.Infrastructure.Communications;

public sealed class PortalUrlOptions
{
    public const string SectionName = "PortalUrls";
    public string StaffPasswordResetUrl { get; set; } = string.Empty;
    public string HrPasswordResetUrl { get; set; } = string.Empty;
    public string StaffPortalUrl { get; set; } = string.Empty;
    public string HrPortalUrl { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
}

public sealed record RenderedEmail(string Subject, string HtmlBody, string TextBody);

public sealed class BrandedEmailRenderer
{
    private const string Navy = "#13294B";
    private const string Pink = "#D0006F";
    private readonly PortalUrlOptions _portals;

    public BrandedEmailRenderer(Microsoft.Extensions.Options.IOptions<PortalUrlOptions> portals) =>
        _portals = portals.Value;

    public string StaffPortalUrl => _portals.StaffPortalUrl;

    public RenderedEmail PasswordReset(string displayName, string resetUrl) =>
        Render(
            "Reset your CSIR SPME password",
            displayName,
            "Reset your password",
            "We received a request to reset the password for your CSIR SPME account.",
            "Reset password",
            resetUrl,
            "This secure link expires in 24 hours and can be used only once. If you did not request this change, you can safely ignore this email. Never share this link with anyone.");

    public RenderedEmail LeaveDecision(
        string displayName,
        string decision,
        string leaveType,
        DateTime startDate,
        DateTime endDate,
        decimal workingDays,
        string? rejectionReason,
        Guid leaveRequestId)
    {
        var detail = $"Your {leaveType} leave request for {startDate:dd MMM yyyy} to {endDate:dd MMM yyyy} ({workingDays:0.##} working days) has been {decision}.";
        if (decision == "rejected" && !string.IsNullOrWhiteSpace(rejectionReason))
            detail += $" Reason: {rejectionReason.Trim()}";
        var link = $"{_portals.StaffPortalUrl.TrimEnd('/')}/leave/{leaveRequestId:D}";
        return Render(
            $"Leave request {decision}",
            displayName,
            $"Leave request {decision}",
            detail,
            "View leave request",
            link,
            "Sign in to the CSIR staff portal to review the request details.");
    }

    public RenderedEmail LeaveAwaitingApproval(
        string approverDisplayName,
        string staffDisplayName,
        string approvalStage,
        string leaveType,
        DateTime startDate,
        DateTime endDate,
        decimal workingDays,
        Guid leaveRequestId,
        string? approvalToken = null)
    {
        var stageLabel = approvalStage.Replace('-', ' ');
        var detail =
            $"{staffDisplayName} submitted a {leaveType} leave request for {startDate:dd MMM yyyy} to {endDate:dd MMM yyyy} ({workingDays:0.##} working days). It is waiting for {stageLabel} review.";
        var link = string.IsNullOrWhiteSpace(approvalToken)
            ? $"{_portals.StaffPortalUrl.TrimEnd('/')}/leave/{leaveRequestId:D}"
            : $"{_portals.StaffPortalUrl.TrimEnd('/')}/approvals/leave?token={Uri.EscapeDataString(approvalToken)}";
        return Render(
            "Leave request awaiting your approval",
            approverDisplayName,
            "Leave request awaiting approval",
            detail,
            "Review leave request",
            link,
            "Sign in to the CSIR staff portal to approve or return this request. The secure email link expires in 48 hours.");
    }

    public RenderedEmail SkeletalStaffAwaitingApproval(
        string approverDisplayName,
        string staffDisplayName,
        string approvalStage,
        DateTime periodStart,
        DateTime periodEnd,
        Guid requestId,
        string approvalToken)
    {
        var stageLabel = approvalStage.Replace('-', ' ');
        var dateSummary = $"{periodStart:dd MMM yyyy} to {periodEnd:dd MMM yyyy}";
        var detail =
            $"{staffDisplayName} submitted a skeletal staff request for the period {dateSummary}. It is waiting for {stageLabel} review.";
        var link = $"{_portals.StaffPortalUrl.TrimEnd('/')}/approvals/skeletal-staff?token={Uri.EscapeDataString(approvalToken)}";
        return Render(
            "Skeletal staff request awaiting your approval",
            approverDisplayName,
            "Skeletal staff request awaiting approval",
            detail,
            "Review skeletal staff request",
            link,
            "Sign in to the CSIR staff portal to approve or reject this request. The secure email link expires in 48 hours.");
    }

    public RenderedEmail SkeletalStaffDecision(
        string displayName,
        string decision,
        DateTime periodStart,
        DateTime periodEnd,
        string? rejectionReason,
        Guid requestId)
    {
        var dateSummary = $"{periodStart:dd MMM yyyy} to {periodEnd:dd MMM yyyy}";
        var detail = $"Your skeletal staff request for the period {dateSummary} has been {decision}.";
        if (decision == "rejected" && !string.IsNullOrWhiteSpace(rejectionReason))
            detail += $" Reason: {rejectionReason.Trim()}";
        var link = $"{_portals.StaffPortalUrl.TrimEnd('/')}/skeletal-staff/{requestId:D}";
        return Render(
            $"Skeletal staff request {decision}",
            displayName,
            $"Skeletal staff request {decision}",
            detail,
            "View skeletal staff request",
            link,
            "Sign in to the CSIR staff portal to review the request details.");
    }

    public RenderedEmail SkeletalStaffServiceReport(
        string recipientDisplayName,
        string staffDisplayName,
        string periodName,
        Guid requestId)
    {
        var link = $"{_portals.StaffPortalUrl.TrimEnd('/')}/skeletal-staff/{requestId:D}";
        return Render(
            "Skeletal staff service report",
            recipientDisplayName,
            "Skeletal staff service report",
            $"{staffDisplayName} completed skeletal staff service for {periodName}. The attached report is available for your records.",
            "Open skeletal staff request",
            link,
            "Sign in to the CSIR staff portal to review the request and attached report.");
    }

    public RenderedEmail StaffQuarterlyReportReviewed(
        string ownerDisplayName,
        string periodName,
        string title,
        string outcome,
        string? returnReason,
        Guid reportId)
    {
        var heading = outcome == "returned" ? "Quarterly report returned" : "Quarterly report approved";
        var detail = outcome == "returned"
            ? $"Your {periodName} report \"{title}\" was returned for correction."
            : $"Your {periodName} report \"{title}\" was approved.";
        if (outcome == "returned" && !string.IsNullOrWhiteSpace(returnReason))
            detail += $" {returnReason.Trim()}";
        var link = $"{_portals.StaffPortalUrl.TrimEnd('/')}/reports/{reportId:D}";
        return Render(
            heading,
            ownerDisplayName,
            heading,
            detail,
            "Open quarterly report",
            link,
            "Sign in to the CSIR staff portal to review the current report status.");
    }

    public RenderedEmail HrApprovalAccess(string displayName, string staffId)
    {
        var portalUrl = _portals.StaffPortalUrl.TrimEnd('/');
        var staffLabel = string.IsNullOrWhiteSpace(staffId) ? "your staff ID" : staffId.Trim();
        var rendered = Render(
            "Your CSIR staff portal access is ready",
            displayName,
            "Your staff record has been approved",
            "Human Resources has approved your CSIR staff record. You can now sign in to the Strategic Planning, Monitoring and Evaluation staff portal to use self-service features that were previously unavailable.",
            "Open staff portal",
            portalUrl,
            "Sign in with your staff ID, verified email, or phone number. If you have not set a password yet, use Forgot password or account activation on the sign-in page. Do not share this message.");
        var details = $"""
            <p style="line-height:1.6">Staff ID: <strong>{WebUtility.HtmlEncode(staffLabel)}</strong></p>
            <p style="line-height:1.6">Once you sign in, you can:</p>
            <ul style="line-height:1.7;padding-left:20px">
              <li>Review and update your employee profile</li>
              <li>Apply for leave and follow approval progress</li>
              <li>Submit quarterly reports and related self-service work</li>
              <li>Access promotions and other features assigned to your role</li>
            </ul>
            """;
        return rendered with
        {
            HtmlBody = rendered.HtmlBody.Replace(
                "<p style=\"margin:28px 0\">",
                details + "<p style=\"margin:28px 0\">",
                StringComparison.Ordinal),
            TextBody = $"""
                Hello {NormalizeText(displayName, "Staff member")},

                Your staff record has been approved
                Human Resources has approved your CSIR staff record. You can now sign in to the Strategic Planning, Monitoring and Evaluation staff portal to use self-service features that were previously unavailable.

                Staff ID: {staffLabel}
                Once you sign in, you can review your profile, apply for leave, submit quarterly reports, and access other assigned features.

                Open staff portal: {portalUrl}

                Sign in with your staff ID, verified email, or phone number. If you have not set a password yet, use Forgot password or account activation on the sign-in page. Do not share this message.
                """
        };
    }

    public string HrApprovalAccessSms() =>
        $"CSIR: Your staff record is approved. Access more features at {_portals.StaffPortalUrl.TrimEnd('/')}";

    public RenderedEmail StaffQuarterlyReportSubmitted(StaffQuarterlyReportNotification notification)
    {
        var reportUrl = $"{_portals.StaffPortalUrl.TrimEnd('/')}/reports/{notification.ReportId:D}";
        static string JoinNames(IReadOnlyList<string> values) =>
            values.Count == 0 ? "None" : string.Join(", ", values);
        var details = $"""
            <p><strong>Staff:</strong> {WebUtility.HtmlEncode(notification.StaffDisplayName)}</p>
            <p><strong>Quarter:</strong> {WebUtility.HtmlEncode(notification.PeriodName)}</p>
            <p><strong>Title:</strong> {WebUtility.HtmlEncode(notification.Title)}</p>
            <p><strong>Abstract:</strong> {WebUtility.HtmlEncode(notification.Abstract ?? "Not provided")}</p>
            <p><strong>Work summary:</strong> {WebUtility.HtmlEncode(notification.WorkSummary)}</p>
            <p><strong>Key results:</strong> {WebUtility.HtmlEncode(notification.KeyResults ?? "Not provided")}</p>
            <p><strong>Conclusion and next steps:</strong> {WebUtility.HtmlEncode(notification.ConclusionNextSteps ?? "Not provided")}</p>
            <p><strong>Projects:</strong> {WebUtility.HtmlEncode(JoinNames(notification.Projects))}</p>
            <p><strong>Technologies:</strong> {WebUtility.HtmlEncode(JoinNames(notification.Technologies))}</p>
            """;
        var rendered = Render(
            $"Quarterly report submitted: {notification.Title}",
            notification.ReviewerDisplayName,
            "Quarterly report submitted for your review",
            $"{notification.StaffDisplayName} submitted a report for {notification.PeriodName}.",
            "Review quarterly report",
            reportUrl,
            "Sign in to the CSIR staff portal to review and decide this report. The full report is also attached as a PDF.");
        return rendered with
        {
            HtmlBody = rendered.HtmlBody.Replace("<p style=\"margin:28px 0\">",
                details + "<p>The full report is attached as a PDF.</p><p style=\"margin:28px 0\">", StringComparison.Ordinal),
            TextBody = $"""
                Hello {NormalizeText(notification.ReviewerDisplayName, "HOD")},

                Quarterly report submitted for your review. The full report is attached as a PDF.
                Staff: {NormalizeText(notification.StaffDisplayName, "Staff member")}
                Quarter: {NormalizeText(notification.PeriodName, "Quarter")}
                Title: {NormalizeText(notification.Title, "Untitled")}
                Abstract: {NormalizeText(notification.Abstract, "Not provided")}
                Work summary: {NormalizeText(notification.WorkSummary, "Not provided")}
                Key results: {NormalizeText(notification.KeyResults, "Not provided")}
                Conclusion and next steps: {NormalizeText(notification.ConclusionNextSteps, "Not provided")}
                Projects: {JoinNames(notification.Projects)}
                Technologies: {JoinNames(notification.Technologies)}

                Review quarterly report: {reportUrl}
                """
        };
    }

    private RenderedEmail Render(
        string subject,
        string displayName,
        string heading,
        string message,
        string cta,
        string actionUrl,
        string securityNotice)
    {
        var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(displayName) ? "Staff member" : displayName.Trim());
        var safeHeading = WebUtility.HtmlEncode(heading);
        var safeMessage = WebUtility.HtmlEncode(message);
        var safeCta = WebUtility.HtmlEncode(cta);
        var safeUrl = WebUtility.HtmlEncode(actionUrl);
        var safeNotice = WebUtility.HtmlEncode(securityNotice);
        var logo = BuildLogo();
        var html = $"""
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{safeHeading}</title></head>
            <body style="margin:0;background:#f4f6f8;color:#172033;font-family:Arial,sans-serif">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f4f6f8;padding:24px 12px">
                <tr><td align="center">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:640px;background:#fff;border-radius:8px;overflow:hidden">
                    <tr><td style="background:{Navy};padding:24px;color:#fff">{logo}<strong style="font-size:20px">CSIR Strategic Planning, Monitoring and Evaluation</strong></td></tr>
                    <tr><td style="padding:32px">
                      <p style="margin-top:0">Hello {safeName},</p>
                      <h1 style="color:{Navy};font-size:26px">{safeHeading}</h1>
                      <p style="line-height:1.6">{safeMessage}</p>
                      <p style="margin:28px 0"><a href="{safeUrl}" style="display:inline-block;background:{Pink};color:#fff;text-decoration:none;padding:14px 22px;border-radius:4px;font-weight:bold">{safeCta}</a></p>
                      <p style="font-size:14px;line-height:1.5;color:#4b5563">{safeNotice}</p>
                      <p style="font-size:12px;color:#6b7280;word-break:break-all">If the button does not work, copy this address into your browser:<br>{safeUrl}</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
        var text = new StringBuilder()
            .AppendLine($"Hello {NormalizeText(displayName, "Staff member")},")
            .AppendLine()
            .AppendLine(heading)
            .AppendLine(message)
            .AppendLine()
            .AppendLine($"{cta}: {actionUrl}")
            .AppendLine()
            .AppendLine(securityNotice)
            .ToString();
        return new RenderedEmail(subject, html, text);
    }

    private string BuildLogo()
    {
        if (string.IsNullOrWhiteSpace(_portals.LogoUrl))
            return string.Empty;
        return $"<img src=\"{WebUtility.HtmlEncode(_portals.LogoUrl.Trim())}\" alt=\"CSIR\" width=\"72\" style=\"display:block;margin-bottom:16px;max-width:72px;height:auto\">";
    }

    private static string NormalizeText(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Replace("\r", " ").Replace("\n", " ").Trim();
}
