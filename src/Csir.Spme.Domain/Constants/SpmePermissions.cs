namespace Csir.Spme.Domain.Constants;

/// <summary>Explicit permission codes. No wildcard permission is granted in production.</summary>
public static class SpmePermissions
{
    public const string PlatformManage = "platform.manage";

    public const string UsersRead = "users.read";
    public const string UsersWrite = "users.write";
    public const string UsersAssignRoles = "users.assign-roles";

    public const string ApiClientsRead = "api-clients.read";
    public const string ApiClientsWrite = "api-clients.write";

    public const string InstitutesRead = "institutes.read";
    public const string InstitutesManage = "institutes.manage";
    public const string OrganizationRead = "organization.read";
    public const string OrganizationManage = "organization.manage";

    public const string EmployeesRead = "employees.read";
    public const string EmployeesReadSensitive = "employees.read-sensitive";
    public const string EmployeesWrite = "employees.write";
    public const string EmployeesVerify = "employees.verify";

    public const string EmployeeImportsRead = "employee-imports.read";
    public const string EmployeeImportsCreate = "employee-imports.create";
    public const string EmployeeImportsCommit = "employee-imports.commit";
    public const string EmployeeImportsCancel = "employee-imports.cancel";

    public const string HrAnalyticsRead = "hr.analytics.read";
    public const string AppraisalsSelf = "appraisals.self";
    public const string AppraisalsReview = "appraisals.review";
    public const string AppraisalsFinalApprove = "appraisals.final-approve";
    public const string AppraisalsAdmin = "appraisals.admin";
    public const string AppraisalsFinalRead = "appraisals.final-read";

    public const string LeaveRead = "leave.read";
    public const string LeaveRequest = "leave.request";
    public const string LeaveApprove = "leave.approve";
    public const string LeaveManage = "leave.manage";

    public const string StrategicPlansRead = "strategic-plans.read";
    public const string StrategicPlansWrite = "strategic-plans.write";
    public const string StrategicPlansActivate = "strategic-plans.activate";

    public const string ThrustsRead = "thrusts.read";
    public const string ThrustsWrite = "thrusts.write";
    public const string StakeholderPlansRead = "stakeholder-plans.read";
    public const string StakeholderPlansWrite = "stakeholder-plans.write";

    public const string IndicatorsRead = "indicators.read";
    public const string IndicatorsWrite = "indicators.write";
    public const string OutputsRead = "outputs.read";
    public const string OutputsWrite = "outputs.write";

    public const string ProjectsRead = "projects.read";
    public const string ProjectsWrite = "projects.write";
    public const string ProjectsApprove = "projects.approve";

    public const string ReportsRead = "reports.read";
    public const string ReportsWrite = "reports.write";
    public const string ReportsSubmit = "reports.submit";
    public const string ReportsApprove = "reports.approve";
    public const string ReportsExport = "reports.export";
    public const string ReportsSelf = "reports.self";
    public const string ReportsReview = "reports.review";

    public const string AnalyticsRead = "analytics.read";

    public const string KnowledgeRead = "knowledge.read";
    public const string KnowledgeWrite = "knowledge.write";

    public const string PromotionsSelfRead = "promotions.self.read";
    public const string PromotionsRead = "promotions.read";
    public const string PromotionsWrite = "promotions.write";
    public const string PromotionsApprove = "promotions.approve";

    public const string MemosRead = "memos.read";
    public const string MemosWrite = "memos.write";
    public const string MemosPublish = "memos.publish";
    public const string FaqsRead = "faqs.read";
    public const string FaqsManage = "faqs.manage";

    public const string NotificationsSelf = "notifications.self";
    public const string NotificationsManage = "notifications.manage";

    public const string FilesRead = "files.read";
    public const string FilesWrite = "files.write";
    public const string FilesDelete = "files.delete";

    public const string AuditRead = "audit.read";
    public const string ConfigurationManage = "configuration.manage";

    /// <summary>Every permission code in the catalogue.</summary>
    public static readonly string[] All =
    [
        PlatformManage,
        UsersRead, UsersWrite, UsersAssignRoles,
        ApiClientsRead, ApiClientsWrite,
        InstitutesRead, InstitutesManage, OrganizationRead, OrganizationManage,
        EmployeesRead, EmployeesReadSensitive, EmployeesWrite, EmployeesVerify,
        EmployeeImportsRead, EmployeeImportsCreate, EmployeeImportsCommit, EmployeeImportsCancel,
        HrAnalyticsRead, AppraisalsSelf, AppraisalsReview, AppraisalsFinalApprove, AppraisalsAdmin, AppraisalsFinalRead,
        LeaveRead, LeaveRequest, LeaveApprove, LeaveManage,
        StrategicPlansRead, StrategicPlansWrite, StrategicPlansActivate,
        ThrustsRead, ThrustsWrite, StakeholderPlansRead, StakeholderPlansWrite,
        IndicatorsRead, IndicatorsWrite, OutputsRead, OutputsWrite,
        ProjectsRead, ProjectsWrite, ProjectsApprove,
        ReportsRead, ReportsWrite, ReportsSubmit, ReportsApprove, ReportsExport, ReportsSelf, ReportsReview,
        AnalyticsRead,
        KnowledgeRead, KnowledgeWrite,
        PromotionsSelfRead, PromotionsRead, PromotionsWrite, PromotionsApprove,
        MemosRead, MemosWrite, MemosPublish, FaqsRead, FaqsManage,
        NotificationsSelf, NotificationsManage,
        FilesRead, FilesWrite, FilesDelete,
        AuditRead, ConfigurationManage
    ];
}
