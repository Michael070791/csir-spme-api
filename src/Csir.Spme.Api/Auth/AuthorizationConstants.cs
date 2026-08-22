namespace Csir.Spme.Api.Auth;

public static class SpmeRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string InstituteAdmin = "InstituteAdmin";
    public const string HrAdmin = "HrAdmin";
    public const string StrategicPlanAdmin = "StrategicPlanAdmin";
    public const string ReportsAdmin = "ReportsAdmin";
    public const string Employee = "Employee";
    public const string ServiceClient = "ServiceClient";
    public const string HeadOfSection = "HeadOfSection";
    public const string HeadOfDivision = "HeadOfDivision";
    public const string HeadOfAdmin = "HeadOfAdmin";
    public const string InstituteDirector = "InstituteDirector";
    public const string DeputyDirectorGeneral = "DeputyDirectorGeneral";
    public const string DirectorGeneral = "DirectorGeneral";
    public const string ScientificSecretary = "ScientificSecretary";

    public static readonly string[] All =
    [
        PlatformAdmin,
        InstituteAdmin,
        HrAdmin,
        StrategicPlanAdmin,
        ReportsAdmin,
        Employee,
        ServiceClient,
        HeadOfSection,
        HeadOfDivision,
        HeadOfAdmin,
        InstituteDirector,
        DeputyDirectorGeneral,
        DirectorGeneral,
        ScientificSecretary
    ];
}

public static class AuthorizationPolicies
{
    public const string PlatformAdmin = "platform-admin";
    public const string ManageUsers = "users.manage";
    public const string ManageScopedUsers = "users.manage-scoped";
    public const string ReadUsers = "users.read";
    public const string ReadRoles = "roles.read";
    public const string ManageHumanResources = "human-resources.manage";
    public const string ReadHumanResources = "human-resources.read";
    public const string ReadProfileImages = "employee-profile-images.read";
    public const string ManageProfileImages = "employee-profile-images.manage";
    public const string ReadPromotions = "promotions.read";
    public const string WritePromotions = "promotions.write";
    public const string ApprovePromotions = "promotions.approve";
    public const string ReadOwnPromotionStatus = "promotions.self.read";
    public const string ReadVisiblePromotionSubmission = "promotions.submission.read";
    public const string ReadPromotionReports = "promotion-reports.read";
    public const string WriteOwnPromotionReports = "promotion-reports.self.write";
    public const string ReadReports = "reports.read";
    public const string WriteReports = "reports.write";
    public const string SubmitReports = "reports.submit";
    public const string ApproveReports = "reports.approve";
    public const string ManageOwnReports = "reports.self";
    public const string ReviewStaffReports = "reports.review";
    public const string ReadStaffQuarterlyReports = "staff-quarterly-reports.read";
    public const string ReadOrganization = "organization.read";
    public const string ManageOrganization = "organization.manage";
    public const string ReadMemos = "memos.read";
    public const string ManageMemos = "memos.write";
    public const string PublishMemos = "memos.publish";
    public const string ReadHolidays = "holidays.read";
    public const string ManageHolidays = "holidays.manage";
    public const string ReadNotifications = "notifications.self";
    public const string ManageNotifications = "notifications.manage";
    public const string ReadLeave = "leave.read";
    public const string RequestLeave = "leave.request";
    public const string ApproveLeave = "leave.approve";
    public const string ManageLeave = "leave.manage";
    public const string ReadHrDashboard = "hr.dashboard.read";
    public const string ManageAppraisalCycles = "appraisals.manage-cycles";
    public const string ReadAppraisals = "appraisals.final-read";
    public const string ManageOwnAppraisals = "appraisals.self";
    public const string ReviewAppraisals = "appraisals.review";
    public const string ApproveAppraisals = "appraisals.final-approve";
    public const string ReadKnowledge = "knowledge.read";
    public const string WriteKnowledge = "knowledge.write";
    public const string ReadProjects = "projects.read";
    public const string WriteProjects = "projects.write";
    public const string ApproveProjects = "projects.approve";
    public const string ReadStrategicPlans = "strategic-plans.read";
    public const string WriteStrategicPlans = "strategic-plans.write";
    public const string ActivateStrategicPlans = "strategic-plans.activate";
    public const string ReadThrusts = "thrusts.read";
    public const string WriteThrusts = "thrusts.write";
    public const string ReadOutputs = "outputs.read";
    public const string WriteOutputs = "outputs.write";
    public const string ReadIndicators = "indicators.read";
    public const string WriteIndicators = "indicators.write";
}
