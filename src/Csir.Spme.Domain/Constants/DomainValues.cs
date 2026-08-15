namespace Csir.Spme.Domain.Constants;

/// <summary>
/// Single catalogue of controlled values. Wire values are lowercase kebab-case and are used
/// for JSON, database check constraints, and event payloads.
/// </summary>
public static class DomainValues
{
    /// <summary>Returns true when <paramref name="value"/> is one of <paramref name="allowed"/>.</summary>
    public static bool Contains(string[] allowed, string? value) =>
        value is not null && allowed.Contains(value, StringComparer.Ordinal);
}

public static class ScopeTypes
{
    public const string Self = "self";
    public const string Institute = "institute";
    public const string InstituteHierarchy = "institute-hierarchy";
    public const string CsirWide = "csir-wide";

    public static readonly string[] All = [Self, Institute, InstituteHierarchy, CsirWide];
}

public static class MemoAudienceTypes
{
    public const string AllEmployees = "all-employees";
    public const string Institute = "institute";
    public const string Division = "division";
    public const string Section = "section";
    public const string Role = "role";
    public const string Employee = "employee";

    public static readonly string[] All = [AllEmployees, Institute, Division, Section, Role, Employee];
}

public static class MemoStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Withdrawn = "withdrawn";
    public const string Archived = "archived";

    public static readonly string[] All = [Draft, Published, Withdrawn, Archived];
}

public static class ReportingPeriodTypes
{
    public const string Quarterly = "quarterly";
    public const string SemiAnnual = "semi-annual";
    public const string Annual = "annual";
    public const string AdHoc = "ad-hoc";

    public static readonly string[] All = [Quarterly, SemiAnnual, Annual, AdHoc];
}

public static class ReportingPeriodStatuses
{
    public const string Draft = "draft";
    public const string Open = "open";
    public const string Closed = "closed";
    public const string Finalized = "finalized";

    public static readonly string[] All = [Draft, Open, Closed, Finalized];
}

public static class ReportTypes
{
    public const string Strategic = "strategic";
    public const string ResearchAndDevelopment = "research-and-development";
    public const string Performance = "performance";
    public const string Project = "project";
    public const string Hr = "hr";
    public const string StaffQuarterly = "staff-quarterly";

    public static readonly string[] InstituteReportTypes = [Strategic, ResearchAndDevelopment, Performance, Project, Hr];
    public static readonly string[] All = [Strategic, ResearchAndDevelopment, Performance, Project, Hr, StaffQuarterly];
}

public static class ReportScopes
{
    public const string Institute = "institute";
    public const string EmployeeQuarterly = "employee-quarterly";

    public static readonly string[] All = [Institute, EmployeeQuarterly];
}

public static class ReportStatuses
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string UnderReview = "under-review";
    public const string Returned = "returned";
    public const string Approved = "approved";
    public const string Archived = "archived";

    public static readonly string[] All = [Draft, Submitted, UnderReview, Returned, Approved, Archived];
}

public static class ProjectStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string OnHold = "on-hold";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";

    public static readonly string[] All = [Draft, Active, OnHold, Completed, Cancelled, Archived];
}

public static class ProjectNatures
{
    public const string Research = "research";
    public const string Development = "development";
    public const string Consultancy = "consultancy";
    public const string CapacityBuilding = "capacity-building";
    public const string Infrastructure = "infrastructure";
    public const string Other = "other";

    public static readonly string[] All = [Research, Development, Consultancy, CapacityBuilding, Infrastructure, Other];
}

public static class TechnologyStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Archived = "archived";

    public static readonly string[] All = [Draft, Published, Archived];
}

public static class StrategicPlanStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Closed = "closed";
    public const string Archived = "archived";

    public static readonly string[] All = [Draft, Active, Closed, Archived];
}

/// <summary>Controlled values shared by thrust, output, and indicator status fields.</summary>
public static class PlanItemStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string OnTrack = "on-track";
    public const string AtRisk = "at-risk";
    public const string Completed = "completed";
    public const string Archived = "archived";

    public static readonly string[] All = [Draft, Active, OnTrack, AtRisk, Completed, Archived];
}

public static class LeaveTypes
{
    public const string Annual = "annual";
    public const string Part = "part";
    public const string Sick = "sick";
    public const string Examination = "examination";
    public const string Maternity = "maternity";
    public const string Paternity = "paternity";
    public const string LeaveOfAbsence = "leave-of-absence";
    public const string Compassionate = "compassionate";

    public static readonly string[] All =
        [Annual, Part, Sick, Examination, Maternity, Paternity, LeaveOfAbsence, Compassionate];
}

public static class LeaveRequestStatuses
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string UnderReview = "under-review";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";
    public const string ResumptionPending = "resumption-pending";
    public const string Resumed = "resumed";

    public static readonly string[] All =
        [Draft, Submitted, UnderReview, Approved, Rejected, Cancelled, ResumptionPending, Resumed];
}

public static class HolidayPeriodStatuses
{
    public const string Draft = "draft";
    public const string Open = "open";
    public const string Closed = "closed";
    public const string Finalized = "finalized";

    public static readonly string[] All = [Draft, Open, Closed, Finalized];
}

public static class SkeletalStaffRequestStatuses
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string UnderReview = "under-review";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static readonly string[] All = [Draft, Submitted, UnderReview, Approved, Rejected, Completed, Cancelled];
}

public static class LeaveApprovalStages
{
    public const string SectionHead = "section-head";
    public const string HeadOfDivision = "head-of-division";
    public const string AdminDirector = "admin-director";
    public const string InstituteDirector = "institute-director";
    public const string CorporateHeadOfAdmin = "corporate-head-of-admin";
    public const string Ddg = "ddg";
    public const string Dg = "dg";

    public static readonly string[] All =
        [SectionHead, HeadOfDivision, AdminDirector, InstituteDirector, CorporateHeadOfAdmin, Ddg, Dg];

    /// <summary>Default sequential chain used when no institute-specific chain is configured.</summary>
    public static readonly string[] DefaultChain = [SectionHead, HeadOfDivision, InstituteDirector];
}

public static class ApprovalDecisions
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Returned = "returned";
    public const string Cancelled = "cancelled";

    public static readonly string[] All = [Pending, Approved, Rejected, Returned, Cancelled];
}

public static class LeaveResumptionStatuses
{
    public const string Submitted = "submitted";
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    public static readonly string[] All = [Submitted, Approved, Rejected];
}

public static class EmployeeProfileStatuses
{
    public const string PendingHrApproval = "pending-hr-approval";
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Archived = "archived";

    public static readonly string[] All = [PendingHrApproval, Active, Inactive, Archived];
}

public static class StaffCategories
{
    public const string JuniorStaff = "junior-staff";
    public const string SeniorStaff = "senior-staff";
    public const string SeniorMember = "senior-member";

    public static readonly string[] All = [JuniorStaff, SeniorStaff, SeniorMember];
}

public static class StaffReportUploadKinds
{
    public const string ConceptNote = "concept-note";
    public const string ReportImage = "report-image";

    public static readonly string[] All = [ConceptNote, ReportImage];
}

public static class StaffReportUploadStatuses
{
    public const string Pending = "pending";
    public const string Completed = "completed";
    public const string Expired = "expired";

    public static readonly string[] All = [Pending, Completed, Expired];
}

public static class StaffReportAttachmentTypes
{
    public const string ReportImage = "report-image";

    public static readonly string[] All = [ReportImage];
}

public static class QualificationLevels
{
    public const string Certificate = "certificate";
    public const string Diploma = "diploma";
    public const string BachelorOrEquivalent = "bachelor-or-equivalent";
    public const string MastersOrEquivalent = "masters-or-equivalent";
    public const string DoctorateOrEquivalent = "doctorate-or-equivalent";
    public const string Other = "other";

    public static readonly string[] All =
    [
        Certificate, Diploma, BachelorOrEquivalent, MastersOrEquivalent, DoctorateOrEquivalent, Other
    ];
}

public static class ChildGenders
{
    public const string Male = "male";
    public const string Female = "female";

    public static readonly string[] All = [Male, Female];
}
