using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Csir.Spme.Api.OpenApi;

internal sealed class BearerSecuritySchemeTransformer(IConfiguration configuration) : IOpenApiDocumentTransformer
{
    private static readonly IReadOnlyDictionary<string, string> TagDescriptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Reports"] =
                "Institute-scoped reporting resources covering strategic, research and development, performance, project, and HR reporting categories. Reports follow a controlled draft, submission, correction, and approval workflow.",
            ["Reporting Periods"] =
                "Institute-scoped and CSIR-wide reporting-period catalogues that define reporting windows and the lifecycle in which indicator measurements may be changed. Periods are constrained by the caller's effective institute access.",
            ["Human Resources"] =
                "Institute-scoped employee records, employment history, profile images, spouse records, child dependants, and HR approval state. HR callers are constrained by authorized institute scope, while platform administration can perform permitted cross-institute operations.",
            ["Promotions"] =
                "Promotion cycles, eligibility, status checks, submissions, and confidential employee-authored submission reports. Employee self-service remains owner-scoped, while HR review access follows the caller's authorized institute scope.",
            ["Leave"] =
                "Leave requests, leave balances, public holidays, and leave administration resources. Institute callers see their own institute's leave data plus CSIR-wide holidays, while platform administration can manage CSIR-wide scope.",
            ["Memos"] =
                "Institute-scoped internal memoranda with controlled drafting, audience targeting, publication, withdrawal, employee acknowledgement, and downstream in-application notification delivery.",
            ["Notifications"] =
                "Private in-application notifications for authenticated users. Notification records are recipient-owned and can be listed or marked read only by the user to whom they were delivered.",
            ["Settings"] =
                "Authenticated self-service account settings for profile details, verified email changes, password changes, and notification preferences. Every operation applies only to the caller's own identity record.",
            ["Technologies"] =
                "Institute-scoped technologies and innovations recorded for knowledge management, publication readiness, transfer tracking, and strategic performance reporting.",
            ["Projects"] =
                "Institute-scoped research, development, consultancy, capacity-building, and infrastructure projects, including their lifecycle state, ownership, strategic alignment, budget, innovation, and impact information.",
            ["Staff Portal"] =
                "Authenticated employee dashboard projection for the staff portal. The payload is derived only from the bearer-token employee and institute claims and is not cacheable.",
            ["Files"] =
                "Resumable promotion-document upload completion. Staff finish a previously created upload session after bytes are written to storage; files are virus-scanned before they can satisfy a requirement.",
            ["Identity and Access"] =
                "Session creation, refresh, logout, account activation, password reset, and the authenticated user context. Anonymous routes issue credentials; protected routes require a bearer token.",
            ["System Users"] =
                "Platform and institute administration of Identity users, including role assignment and institute linkage. Operations are constrained by the caller's authorized institute scope.",
            ["Institutes"] =
                "Institute, division, and section catalogue operations for CSIR organization structure. Reads require organization.read and mutations require organization.manage. Platform administrators may filter by institute; other callers remain in their authenticated scope, and missing or cross-institute resources share a non-disclosing not-found response.",
            ["Strategic Plans"] =
                "Institute-scoped strategic plans, including activation. Plans own thrusts; creating or updating a plan does not infer a promotion grade or job title.",
            ["Thrusts"] =
                "Strategic-plan thrusts that group outputs and indicators. List operations are institute-scoped and support cursor pagination.",
            ["Outputs"] =
                "Plan outputs under a thrust. Indicators are created under an output; thrust-level indicator lists remain read-only.",
            ["Indicators"] =
                "Performance indicators attached to outputs, with thrust-level read lists. Measurements are recorded against a reporting period.",
            ["Indicator Measurements"] =
                "Period-bound indicator measurements. Creates require an idempotency key and follow the reporting-period lifecycle.",
            ["Staff quarterly reports"] =
                "Employee-owned quarterly reports with a reviewer queue for heads of division. Staff create and submit their own drafts; reviewers approve or return assigned submissions.",
            ["Promotion submissions"] =
                "Confidential staff-owned promotion submissions, requirements, declarations, documents, and HR review transitions. Staff may edit only draft and returned submissions."
        };

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info.Title = "CSIR SPME API V2";
        document.Info.Version = "2.0.0";
        document.Info.Description =
            "The CSIR Strategic Plan Management and Evaluation (SPME) API V2 provides institute-scoped human resources, strategic planning, projects, reporting, promotions, leave, and communications operations. Application routes are rooted at /api/v2. Authenticate with a JWT bearer token issued by POST /api/v2/auth/sessions. Interactive reference is published at /scalar/v2.";
        document.Info.Contact = new OpenApiContact
        {
            Name = "CSIR SPME Support"
        };
        var supportEmail = configuration["Documentation:SupportEmail"];
        if (!string.IsNullOrWhiteSpace(supportEmail))
        {
            document.Info.Contact.Email = supportEmail;
        }

        var docsSiteUrl = configuration["Documentation:SiteUrl"];
        if (Uri.TryCreate(docsSiteUrl, UriKind.Absolute, out var docsSiteUri))
        {
            document.Info.Contact.Url = docsSiteUri;
        }

        var serverUrl = configuration["OpenApi:ServerUrl"]?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(serverUrl) &&
            Uri.TryCreate(serverUrl, UriKind.Absolute, out _))
        {
            document.Servers ??= [];
            document.Servers.Clear();
            document.Servers.Add(new OpenApiServer
            {
                Url = serverUrl,
                Description = "CSIR SPME API V2"
            });
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["BearerAuth"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT bearer token issued by POST /api/v2/auth/sessions."
        };

        if (document.Tags is not null)
        {
            foreach (var tag in document.Tags)
            {
                if (tag.Name is not null &&
                    TagDescriptions.TryGetValue(tag.Name, out var description))
                {
                    tag.Description = description;
                }
            }
        }

        return Task.CompletedTask;
    }
}
