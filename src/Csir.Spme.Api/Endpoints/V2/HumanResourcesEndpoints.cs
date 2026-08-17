using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Infrastructure.Identity;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class HumanResourcesEndpoints
{
    private const int MaximumChildrenPerEmployee = 2;
    private const long MaximumProfileImageBytes = 5 * 1024 * 1024;
    private const string LeadershipRoleDelimiter = ", ";
    private static readonly char[] LeadershipRoleSeparators = [',', ';'];
    private static readonly HashSet<string> SupportedProfileImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    public static void MapHumanResourcesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var employees = endpoints.MapGroup("/api/v2/employees")
            .WithGroupName("v2")
            .WithTags("Human Resources")
            .WithDescription("Institute-scoped human resources records for employee profiles, current employment context, profile images, spouse records, and child dependants. HR users are restricted to their authorized institute scope, while platform administration can work across institutes where permitted.")
            .RequireAuthorization(AuthorizationPolicies.ReadHumanResources)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        employees.MapGet("", GetEmployeesAsync)
            .WithName("Employees_List")
            .WithSummary("List employees.")
            .WithDescription("Returns a paged employee directory with institute and current employment context, including staff category, Conditions of Service job title, and grade step as separate fields. Clients can search staff IDs, names, email, or phone, and filter by institute, division, section, profile status, service status, combined status tokens, HR approval state, and Head of Division/Section (isHod) within the caller's authorized scope. Response includes approvedTotal, unapprovedTotal, and hodTotal for the same scope ignoring the matching tab filter so directory tabs stay accurate, plus the current-year remaining annual leave days for each listed employee.")
            .Produces<EmployeePageResponse>(StatusCodes.Status200OK);
        employees.MapGet("/{id:guid}", GetEmployeeAsync)
            .WithName("Employees_Get")
            .WithSummary("Get an employee profile.")
            .WithDescription("Returns one employee profile with institute metadata, contact fields, profile status, HR approval flags, profile image reference, and the current employment summary. The employment summary includes staff category, Conditions of Service job title (gradeId, gradeCode, gradeName), and grade step as separate fields. Employees outside the caller's authorized institute scope are returned as not found.")
            .Produces<EmployeeDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        employees.MapPost("", CreateEmployeeAsync)
            .WithName("Employees_Create")
            .WithSummary("Create an employee profile.")
            .WithDescription("Creates an institute-scoped employee profile and initial current employment record. Job title is the Conditions of Service title from the grade catalog when gradeId is supplied. GradeStep is the salary step and does not select a promotion path. The API validates required identity fields, institute access, organization references, staff ID uniqueness within the institute, email uniqueness, controlled status values, and leadership role length before writing an audit event.")
            .RequireAuthorization(AuthorizationPolicies.ManageHumanResources)
            .Produces<EmployeeDetailResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
        employees.MapPatch("/{id:guid}", UpdateEmployeeAsync)
            .WithName("Employees_Update")
            .WithSummary("Update an employee profile.")
            .WithDescription("Updates an accessible employee profile and its current employment context. Job title is the Conditions of Service title from the grade catalog when gradeId is supplied. GradeStep is the salary step and does not select a promotion path. The same institute, organization reference, controlled value, staff ID, and email checks used at creation are enforced, and inaccessible employees use a non-disclosing not-found response.")
            .RequireAuthorization(AuthorizationPolicies.ManageHumanResources)
            .Produces<EmployeeDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        var profileImages = endpoints.MapGroup("/api/v2/employees/{id:guid}/profile-image")
            .WithGroupName("v2")
            .WithTags("Human Resources")
            .WithDescription("Secure employee profile-image upload and delivery. Image authorization is independent from access to full HR records, remains institute-scoped, and never exposes permanent object-storage locations.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        profileImages.MapPost("", UploadEmployeeProfileImageAsync)
            .WithName("Employees_UploadProfileImage")
            .WithSummary("Upload an employee profile image.")
            .WithDescription("Validates, decodes, orients, strips metadata from, resizes, and re-encodes a profile image as bounded WebP before private storage. Employees may replace only their own image; HR and platform administrators remain constrained by their authorized institute scope.")
            .RequireAuthorization(AuthorizationPolicies.ManageProfileImages)
            .RequireRateLimiting("profile-image-upload")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(6 * 1024 * 1024))
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<EmployeeDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        profileImages.MapGet("", GetEmployeeProfileImageAsync)
            .WithName("Employees_GetProfileImage")
            .WithSummary("Read an employee profile image.")
            .WithDescription("Authorizes access to the current institute-scoped profile image, then redirects the caller to a five-minute, read-only URL for direct private Blob Storage delivery. Missing, deleted, unauthorized, and out-of-scope images use a non-disclosing not-found response.")
            .RequireAuthorization(AuthorizationPolicies.ReadProfileImages)
            .Produces(StatusCodes.Status307TemporaryRedirect)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        profileImages.MapGet("/access", GetEmployeeProfileImageAccessAsync)
            .WithName("Employees_GetProfileImageAccess")
            .WithSummary("Create temporary profile-image read access.")
            .WithDescription("Returns a five-minute, read-only URL for the employee's current private profile image after enforcing caller role, employee identity, and institute scope. The response is never cacheable and contains no container name or storage key fields.")
            .RequireAuthorization(AuthorizationPolicies.ReadProfileImages)
            .Produces<ProfileImageAccessResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        employees.MapGet("/{employeeId:guid}/employment-records", GetEmploymentRecordsAsync)
            .WithName("Employees_ListEmploymentRecords")
            .WithSummary("List an employee's employment records.")
            .WithDescription("Returns the employee's employment history ordered by most recent effective date, including division and section names, Conditions of Service job title, leadership roles, staff category, grade step, specialization, service status, location, pension metadata, and appointment or promotion dates.")
            .Produces<CollectionResponse<EmploymentRecordResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        employees.MapGet("/{employeeId:guid}/spouse", GetSpouseAsync)
            .WithName("EmployeeSpouses_Get")
            .WithSummary("Get an employee spouse record.")
            .WithDescription("Returns the spouse record attached to an accessible employee, including optional date of birth, phone, email, occupation, and employer fields. Missing employees, out-of-scope employees, and employees without a spouse record receive not-found responses.")
            .Produces<EmployeeSpouseResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        employees.MapPost("/{employeeId:guid}/spouse", CreateSpouseAsync)
            .WithName("EmployeeSpouses_Create")
            .WithSummary("Create an employee spouse record.")
            .WithDescription("Creates the single spouse record allowed for an accessible employee after validating required spouse name input. Attempts to create a duplicate spouse record are rejected with a conflict response.")
            .RequireAuthorization(AuthorizationPolicies.ManageHumanResources)
            .Produces<EmployeeSpouseResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        employees.MapPut("/{employeeId:guid}/spouse/{spouseId:guid}", UpdateSpouseAsync)
            .WithName("EmployeeSpouses_Update")
            .WithSummary("Update an employee spouse record.")
            .WithDescription("Replaces the editable spouse details for an accessible employee while preserving the employee ownership relationship. The spouse identifier must belong to the employee in the route, otherwise the request is treated as not found.")
            .RequireAuthorization(AuthorizationPolicies.ManageHumanResources)
            .Produces<EmployeeSpouseResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        employees.MapDelete("/{employeeId:guid}/spouse/{spouseId:guid}", DeleteSpouseAsync)
            .WithName("EmployeeSpouses_Delete")
            .WithSummary("Delete an employee spouse record.")
            .WithDescription("Deletes the spouse record only when both the employee and spouse are accessible and the spouse belongs to that employee. Requests for another employee's spouse record are returned as not found.")
            .RequireAuthorization(AuthorizationPolicies.ManageHumanResources)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var selfRecords = endpoints.MapGroup("/api/v2/employees")
            .WithGroupName("v2")
            .WithTags("Human Resources")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        selfRecords.MapGet("/{employeeId:guid}/education", GetEducationAsync)
            .WithName("EmployeeEducation_List")
            .WithSummary("List accessible employee education records.")
            .WithDescription("Returns education records for an accessible employee, ordered by completion date and institution. Out-of-scope and unknown employee identifiers receive the same non-disclosing not-found response.");
        selfRecords.MapPost("/{employeeId:guid}/education", CreateEducationAsync)
            .WithName("EmployeeEducation_Create")
            .WithSummary("Create an owned or HR-managed education record.")
            .WithDescription("Creates and audits an education record when the caller owns the employee profile or has permitted HR management access. Certificate awarded must be a canonical catalog value such as BSc, BE, MSc, or MPhil, qualification level must match that award, and completion may not precede commencement.");
        selfRecords.MapGet("/{employeeId:guid}/education/{educationId:guid}", GetEducationRecordAsync)
            .WithName("EmployeeEducation_Get")
            .WithSummary("Get an accessible education record.")
            .WithDescription("Returns one education record only when it belongs to the employee in the route and that employee is accessible to the caller. Missing, mismatched, and out-of-scope records are represented as not found.");
        selfRecords.MapPatch("/{employeeId:guid}/education/{educationId:guid}", UpdateEducationAsync)
            .WithName("EmployeeEducation_Update")
            .WithSummary("Update staff-controlled education details.")
            .WithDescription("Updates and audits staff-controlled qualification details for an owned or HR-managed education record while preserving HR recognition and relevant-field review decisions.");
        selfRecords.MapDelete("/{employeeId:guid}/education/{educationId:guid}", DeleteEducationAsync)
            .WithName("EmployeeEducation_Delete")
            .WithSummary("Delete an owned or HR-managed education record.")
            .WithDescription("Deletes and audits an accessible education record unless a promotion qualification assessment already references it. Mismatched ownership and out-of-scope identifiers are represented as not found.");
        selfRecords.MapPost("/{employeeId:guid}/education/{educationId:guid}/review", ReviewEducationAsync)
            .WithName("EmployeeEducation_Review")
            .WithSummary("Review education recognition and relevant-field status.")
            .WithDescription("Lets authorized HR set institution recognition and relevant-field verification after staff submit education details. Status values follow the controlled InstitutionRecognitionStatus and RelevantFieldStatus lists. At least one status field is required.")
            .RequireAuthorization(AuthorizationPolicies.ManageHumanResources)
            .Produces<EducationRecordResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        selfRecords.MapGet("/{employeeId:guid}/children", GetChildrenAsync)
            .WithName("EmployeeChildren_List")
            .WithSummary("List an employee's child records.")
            .WithDescription("Returns the child dependant records attached to an accessible employee, ordered by date of birth. Each item includes gender limited to male or female, birth certificate number, and linked birth certificate file reference.")
            .Produces<CollectionResponse<EmployeeChildResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        selfRecords.MapPost("/{employeeId:guid}/children", CreateChildAsync)
            .WithName("EmployeeChildren_Create")
            .WithSummary("Create an employee child record.")
            .WithDescription("Creates a child dependant record for an accessible employee after validating required name input and restricting gender to male or female. The endpoint enforces the configured maximum of two child records per employee and returns a conflict when the limit is already reached.")
            .Produces<EmployeeChildResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        selfRecords.MapPut("/{employeeId:guid}/children/{childId:guid}", UpdateChildAsync)
            .WithName("EmployeeChildren_Update")
            .WithSummary("Update an employee child record.")
            .WithDescription("Replaces the editable details for one child dependant owned by an accessible employee. The child identifier must belong to the employee in the route, otherwise the request is represented as not found.")
            .Produces<EmployeeChildResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        selfRecords.MapPatch("/{employeeId:guid}/children/{childId:guid}", UpdateChildAsync)
            .WithName("EmployeeChildren_Patch")
            .WithSummary("Update an employee child record.")
            .WithDescription("Updates the editable details for one child dependant owned by an accessible employee. The child identifier must belong to the employee in the route, otherwise the request is represented as not found.")
            .Produces<EmployeeChildResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        selfRecords.MapDelete("/{employeeId:guid}/children/{childId:guid}", DeleteChildAsync)
            .WithName("EmployeeChildren_Delete")
            .WithSummary("Delete an employee child record.")
            .WithDescription("Deletes a child dependant record only when the employee is accessible and the child record belongs to that employee. Out-of-scope employees and mismatched child identifiers receive not-found responses.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapEmployeeProfileEndpoints();

        var verifications = endpoints.MapGroup("/api/v2/employee-verifications")
            .WithGroupName("v2")
            .WithTags("Human Resources")
            .WithDescription("HR verification commands that set or clear employee IsHrApproved without rewriting the full profile. Callers must satisfy human-resources.manage and remain constrained by institute scope. Unscoped HrAdmin and PlatformAdmin may verify across institutes.")
            .RequireAuthorization(AuthorizationPolicies.ManageHumanResources)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        verifications.MapPost("/bulk-approve", BulkApproveEmployeesAsync)
            .WithName("EmployeeVerifications_BulkApprove")
            .WithSummary("Bulk-approve employees for HR.")
            .WithDescription("Approves up to 100 employee IDs in one request. Newly approved employees receive a branded portal-access email and a brief SMS when a deliverable contact exists. Returns per-item outcomes without aborting the batch. Out-of-scope and unknown IDs are not_found. Already-approved employees are skipped. Empty or oversized lists return validation_failed.")
            .Produces<BulkApproveEmployeesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        verifications.MapPost("/{employeeId:guid}/approve", ApproveEmployeeVerificationAsync)
            .WithName("EmployeeVerifications_Approve")
            .WithSummary("Approve an employee for HR.")
            .WithDescription("Sets IsHrApproved to true for an accessible employee without changing ProfileStatus or other profile fields. Newly approved employees receive a branded portal-access email and a brief SMS when a deliverable contact exists. Already-approved employees return the current detail idempotently and are not re-notified. Out-of-scope employees use a non-disclosing not-found response.")
            .Produces<EmployeeDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        verifications.MapPost("/{employeeId:guid}/reject", RejectEmployeeVerificationAsync)
            .WithName("EmployeeVerifications_Reject")
            .WithSummary("Reject HR approval for an employee.")
            .WithDescription("Clears IsHrApproved for an accessible employee without changing ProfileStatus. Already-unapproved employees return the current detail idempotently. Out-of-scope employees use a non-disclosing not-found response.")
            .Produces<EmployeeDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapGroup("/api/v2/education-certificate-types")
            .WithGroupName("v2")
            .WithTags("Human Resources")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .MapGet("", GetEducationCertificateTypesAsync)
            .WithName("EducationCertificateTypes_List")
            .WithSummary("List canonical education certificate types.")
            .WithDescription("Returns the controlled academic awards used when staff add or update education records, including BSc, BE, MSc, MPhil, and doctorate types. Clients may filter by qualification level so the certificate select stays aligned with analytics grouping. Open awards such as Other remain available at every level.")
            .Produces<CollectionResponse<EducationCertificateTypeResponse>>(StatusCodes.Status200OK);

        endpoints.MapGroup("/api/v2/grades")
            .WithGroupName("v2")
            .WithTags("Human Resources")
            .RequireAuthorization(AuthorizationPolicies.ReadHumanResources)
            .MapGet("", GetGradesAsync)
            .WithName("Grades_List")
            .WithSummary("List active employment and promotion grades.")
            .WithDescription("Returns active grades ordered by rank, including staff category, promotion stream, promotion level, and Conditions of Service job title. Clients may filter by staff category.")
            .Produces<CollectionResponse<GradeResponse>>(StatusCodes.Status200OK);
    }

    private static async Task<Results<Ok<EmployeePageResponse>, ProblemHttpResult>> GetEmployeesAsync(
        SpmeDbContext db,
        HttpContext context,
        Guid? instituteId,
        Guid? divisionId,
        Guid? sectionId,
        string? search,
        string? profileStatus,
        string? serviceStatus,
        string? statuses,
        bool? isHrApproved,
        bool? isHod,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var scopeError = InstituteStaffAccess.RequireInstituteAssignmentForEmployees(context.User);
        if (scopeError is not null)
            return EndpointProblems.FromError(scopeError);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = ApplyInstituteScope(db.Employees.AsNoTracking(), context);

        if (instituteId.HasValue && !CurrentInstituteId(context).HasValue)
            query = query.Where(employee => employee.InstituteId == instituteId.Value);
        if (!string.IsNullOrWhiteSpace(profileStatus))
        {
            var normalizedProfileStatus = NormalizeStatusToken(profileStatus);
            query = query.Where(employee => employee.ProfileStatus == normalizedProfileStatus);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            var rawSearch = search.Trim();
            query = query.Where(employee =>
                employee.NormalizedStaffId.Contains(normalizedSearch) ||
                employee.Surname.Contains(rawSearch) ||
                (employee.OtherNames != null && employee.OtherNames.Contains(rawSearch)) ||
                (employee.NormalizedPrimaryEmail != null && employee.NormalizedPrimaryEmail.Contains(normalizedSearch)) ||
                (employee.Phone != null && employee.Phone.Contains(rawSearch)));
        }
        if (divisionId.HasValue || sectionId.HasValue || !string.IsNullOrWhiteSpace(serviceStatus))
        {
            var normalizedServiceStatus = string.IsNullOrWhiteSpace(serviceStatus) ? null : NormalizeStatusToken(serviceStatus);
            query = query.Where(employee => db.EmploymentRecords.Any(record =>
                record.EmployeeId == employee.Id &&
                record.IsCurrent &&
                (!divisionId.HasValue || record.DivisionId == divisionId.Value) &&
                (!sectionId.HasValue || record.SectionId == sectionId.Value) &&
                (normalizedServiceStatus == null || record.ServiceStatus == normalizedServiceStatus)));
        }
        var statusTokens = SplitStatusTokens(statuses);
        if (statusTokens.Length > 0)
        {
            var includeActive = statusTokens.Contains("active", StringComparer.OrdinalIgnoreCase);
            var profileStatuses = statusTokens
                .Where(token => token is not "active" and not "on-leave" and not "retired")
                .Select(ToProfileStatus)
                .Distinct()
                .ToArray();
            var serviceStatuses = statusTokens
                .Where(token => token is not "active" && IsServiceStatusToken(token))
                .Distinct()
                .ToArray();
            query = query.Where(employee =>
                profileStatuses.Contains(employee.ProfileStatus) ||
                db.EmploymentRecords.Any(record =>
                    record.EmployeeId == employee.Id &&
                    record.IsCurrent &&
                    serviceStatuses.Contains(record.ServiceStatus)) ||
                (includeActive &&
                    employee.ProfileStatus == "active" &&
                    (!db.EmploymentRecords.Any(record => record.EmployeeId == employee.Id && record.IsCurrent) ||
                        db.EmploymentRecords.Any(record =>
                            record.EmployeeId == employee.Id &&
                            record.IsCurrent &&
                            record.ServiceStatus == "active"))));
        }

        // Tab totals ignore their own filter so Approved/Unapproved/HOD counts stay correct on every page.
        var approvedTotal = await query.CountAsync(employee => employee.IsHrApproved, cancellationToken);
        var unapprovedTotal = await query.CountAsync(employee => !employee.IsHrApproved, cancellationToken);
        var hodTotal = await query.CountAsync(
            employee => db.EmploymentRecords.Any(record =>
                record.EmployeeId == employee.Id &&
                record.IsCurrent &&
                record.LeadershipRoles != null &&
                (record.LeadershipRoles.Contains("Head of Division") ||
                 record.LeadershipRoles.Contains("Head of Section") ||
                 record.LeadershipRoles.Contains("Division Head") ||
                 record.LeadershipRoles.Contains("Section Head") ||
                 record.LeadershipRoles.Contains("head-of-division") ||
                 record.LeadershipRoles.Contains("head-of-section") ||
                 record.LeadershipRoles.Contains("head of division") ||
                 record.LeadershipRoles.Contains("head of section"))),
            cancellationToken);

        if (isHrApproved.HasValue)
            query = query.Where(employee => employee.IsHrApproved == isHrApproved.Value);
        if (isHod == true)
        {
            query = query.Where(employee => db.EmploymentRecords.Any(record =>
                record.EmployeeId == employee.Id &&
                record.IsCurrent &&
                record.LeadershipRoles != null &&
                (record.LeadershipRoles.Contains("Head of Division") ||
                 record.LeadershipRoles.Contains("Head of Section") ||
                 record.LeadershipRoles.Contains("Division Head") ||
                 record.LeadershipRoles.Contains("Section Head") ||
                 record.LeadershipRoles.Contains("head-of-division") ||
                 record.LeadershipRoles.Contains("head-of-section") ||
                 record.LeadershipRoles.Contains("head of division") ||
                 record.LeadershipRoles.Contains("head of section"))));
        }
        else if (isHod == false)
        {
            query = query.Where(employee => !db.EmploymentRecords.Any(record =>
                record.EmployeeId == employee.Id &&
                record.IsCurrent &&
                record.LeadershipRoles != null &&
                (record.LeadershipRoles.Contains("Head of Division") ||
                 record.LeadershipRoles.Contains("Head of Section") ||
                 record.LeadershipRoles.Contains("Division Head") ||
                 record.LeadershipRoles.Contains("Section Head") ||
                 record.LeadershipRoles.Contains("head-of-division") ||
                 record.LeadershipRoles.Contains("head-of-section") ||
                 record.LeadershipRoles.Contains("head of division") ||
                 record.LeadershipRoles.Contains("head of section"))));
        }

        var total = await query.CountAsync(cancellationToken);
        var employees = await query
            .OrderBy(employee => employee.Surname)
            .ThenBy(employee => employee.OtherNames)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .GroupJoin(
                db.Institutes.AsNoTracking(),
                employee => employee.InstituteId,
                institute => institute.Id,
                (employee, institutes) => new { employee, institutes })
            .SelectMany(
                item => item.institutes.DefaultIfEmpty(),
                (item, institute) => new
                {
                    item.employee.Id,
                    item.employee.StaffId,
                    item.employee.Prefix,
                    item.employee.Surname,
                    item.employee.OtherNames,
                    item.employee.Gender,
                    item.employee.Religion,
                    item.employee.PrimaryEmail,
                    item.employee.Phone,
                    item.employee.ProfileStatus,
                    item.employee.IsHrApproved,
                    item.employee.CreatedAt,
                    item.employee.ProfileImageFileId,
                    Institute = institute == null
                        ? null
                        : new EmployeeInstituteSummary(institute.Id, institute.Code, institute.Name, institute.Kind)
                })
            .ToListAsync(cancellationToken);

        var currentEmploymentByEmployeeId = await GetCurrentEmploymentByEmployeeIdAsync(
            db,
            employees.Select(employee => employee.Id),
            cancellationToken);
        var profileImagesByEmployeeId = await GetProfileImageReferencesAsync(
            db,
            employees.Select(employee => (employee.Id, employee.ProfileImageFileId)),
            cancellationToken);
        var onLeaveUntilByEmployeeId = await GetOnLeaveUntilByEmployeeIdAsync(
            db,
            employees.Select(employee => employee.Id),
            cancellationToken);
        var remainingLeaveByEmployeeId = await GetRemainingAnnualLeaveDaysByEmployeeIdAsync(
            db,
            employees.Select(employee => employee.Id),
            cancellationToken);

        var items = employees
            .Select(employee => new EmployeeListItem(
                employee.Id,
                employee.StaffId,
                employee.Prefix,
                employee.Surname,
                employee.OtherNames,
                employee.Gender,
                employee.Religion,
                employee.PrimaryEmail,
                employee.Phone,
                employee.ProfileStatus,
                employee.IsHrApproved,
                employee.CreatedAt,
                employee.ProfileImageFileId,
                profileImagesByEmployeeId.GetValueOrDefault(employee.Id),
                employee.Institute,
                currentEmploymentByEmployeeId.GetValueOrDefault(employee.Id),
                onLeaveUntilByEmployeeId.GetValueOrDefault(employee.Id),
                remainingLeaveByEmployeeId.GetValueOrDefault(employee.Id)))
            .ToList();

        return TypedResults.Ok(new EmployeePageResponse(items, total, page, pageSize, approvedTotal, unapprovedTotal, hodTotal));
    }

    private static async Task<Results<Ok<EmployeeDetailResponse>, ProblemHttpResult>> GetEmployeeAsync(
        Guid id,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scopeError = InstituteStaffAccess.RequireInstituteAssignmentForEmployees(context.User);
        if (scopeError is not null)
            return EndpointProblems.FromError(scopeError);

        var employee = await GetEmployeeDetailResponseAsync(id, db, context, cancellationToken);
        if (employee is null)
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        return TypedResults.Ok(employee);
    }

    private static async Task<Results<Created<EmployeeDetailResponse>, ProblemHttpResult>> CreateEmployeeAsync(
        UpsertEmployeeRequest request,
        SpmeDbContext db,
        UserManager<User> userManager,
        HttpContext context,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var validationError = await ValidateEmployeeRequestAsync(request, null, db, context, cancellationToken);
        if (validationError is not null)
            return EndpointProblems.FromError(validationError);

        request = await NormalizeEmploymentRequestAsync(request, db, cancellationToken);
        var instituteId = ResolveInstituteId(request, context)!.Value;
        var employee = new Employee(instituteId, request.StaffId.Trim(), request.Surname.Trim(), request.Gender.Trim());
        employee.UpdateProfile(
            request.StaffId,
            request.Prefix,
            request.Surname,
            request.OtherNames,
            request.Gender,
            request.DateOfBirth,
            request.Nationality,
            request.Religion,
            request.MaritalStatus,
            request.PrimaryEmail,
            request.Phone,
            NormalizeStatus(request.ProfileStatus, "active"),
            request.IsHrApproved ?? false);

        db.Employees.Add(employee);
        db.EmploymentRecords.Add(CreateEmploymentRecord(employee.Id, instituteId, request));
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAndSaveAsync("employees.create", "Employee", employee.Id.ToString(), after: employee.StaffId, ct: cancellationToken);
        await EmployeeLeadershipIdentitySync.SyncScientificSecretaryRoleAsync(
            userManager, audit, employee.Id, request.LeadershipRoles, cancellationToken);

        var response = await GetEmployeeDetailResponseAsync(employee.Id, db, context, cancellationToken)
            ?? throw new InvalidOperationException("Created employee could not be loaded.");

        return TypedResults.Created($"/api/v2/employees/{employee.Id}", response);
    }

    private static async Task<Results<Ok<EmployeeDetailResponse>, ProblemHttpResult>> UpdateEmployeeAsync(
        Guid id,
        UpsertEmployeeRequest request,
        SpmeDbContext db,
        UserManager<User> userManager,
        HttpContext context,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var employee = await ApplyInstituteScope(db.Employees, context)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (employee is null)
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var validationError = await ValidateEmployeeRequestAsync(request, id, db, context, cancellationToken);
        if (validationError is not null)
            return EndpointProblems.FromError(validationError);

        request = await NormalizeEmploymentRequestAsync(request, db, cancellationToken);
        var before = employee.StaffId;
        employee.UpdateProfile(
            request.StaffId,
            request.Prefix,
            request.Surname,
            request.OtherNames,
            request.Gender,
            request.DateOfBirth,
            request.Nationality,
            request.Religion,
            request.MaritalStatus,
            request.PrimaryEmail,
            request.Phone,
            NormalizeStatus(request.ProfileStatus, employee.ProfileStatus),
            request.IsHrApproved ?? employee.IsHrApproved);

        var currentEmployment = await db.EmploymentRecords
            .Where(record => record.EmployeeId == employee.Id && record.IsCurrent)
            .OrderByDescending(record => record.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentEmployment is null)
        {
            db.EmploymentRecords.Add(CreateEmploymentRecord(employee.Id, employee.InstituteId, request));
        }
        else
        {
            currentEmployment.UpdateCurrent(
                request.DivisionId,
                request.SectionId,
                currentEmployment.PositionTypeId,
                request.GradeId ?? currentEmployment.GradeId,
                request.JobTitle,
                SerializeLeadershipRoles(request.LeadershipRoles),
                request.StaffCategory,
                request.GradeStep,
                request.AreaOfSpecialization,
                NormalizeStatus(request.ServiceStatus, currentEmployment.ServiceStatus),
                request.Organization,
                request.Location,
                request.Region,
                request.District,
                request.AppointmentDate,
                request.PromotionDate,
                request.PensionType,
                request.PensionId);
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAndSaveAsync("employees.update", "Employee", employee.Id.ToString(), before, employee.StaffId, cancellationToken);
        await EmployeeLeadershipIdentitySync.SyncScientificSecretaryRoleAsync(
            userManager, audit, employee.Id, request.LeadershipRoles, cancellationToken);

        var response = await GetEmployeeDetailResponseAsync(employee.Id, db, context, cancellationToken)
            ?? throw new InvalidOperationException("Updated employee could not be loaded.");

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<EmployeeDetailResponse>, ProblemHttpResult>> ApproveEmployeeVerificationAsync(
        Guid employeeId,
        SpmeDbContext db,
        HttpContext context,
        IAuditService audit,
        IWorkflowNotificationOutbox notifications,
        CancellationToken cancellationToken)
    {
        var employee = await ApplyInstituteScope(db.Employees, context)
            .FirstOrDefaultAsync(candidate => candidate.Id == employeeId, cancellationToken);
        if (employee is null)
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var changed = employee.ApproveHr();
        if (changed)
        {
            await notifications.StageHrApprovalAccessAsync(employee.Id, employee.InstituteId, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await audit.RecordAndSaveAsync(
                "employees.verify.approve",
                "Employee",
                employee.Id.ToString(),
                "isHrApproved=false",
                "isHrApproved=true",
                cancellationToken);
        }

        var response = await GetEmployeeDetailResponseAsync(employee.Id, db, context, cancellationToken)
            ?? throw new InvalidOperationException("Approved employee could not be loaded.");
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<EmployeeDetailResponse>, ProblemHttpResult>> RejectEmployeeVerificationAsync(
        Guid employeeId,
        SpmeDbContext db,
        HttpContext context,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var employee = await ApplyInstituteScope(db.Employees, context)
            .FirstOrDefaultAsync(candidate => candidate.Id == employeeId, cancellationToken);
        if (employee is null)
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var changed = employee.RejectHr();
        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
            await audit.RecordAndSaveAsync(
                "employees.verify.reject",
                "Employee",
                employee.Id.ToString(),
                "isHrApproved=true",
                "isHrApproved=false",
                cancellationToken);
        }

        var response = await GetEmployeeDetailResponseAsync(employee.Id, db, context, cancellationToken)
            ?? throw new InvalidOperationException("Rejected employee could not be loaded.");
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<BulkApproveEmployeesResponse>, ProblemHttpResult>> BulkApproveEmployeesAsync(
        BulkApproveEmployeesRequest request,
        SpmeDbContext db,
        HttpContext context,
        IAuditService audit,
        IWorkflowNotificationOutbox notifications,
        CancellationToken cancellationToken)
    {
        var requestedIds = request.EmployeeIds ?? [];
        if (requestedIds.Count == 0)
            return EndpointProblems.FromError(Error.Validation("At least one employee ID is required."));
        if (requestedIds.Count > 100)
            return EndpointProblems.FromError(Error.Validation("A maximum of 100 employee IDs can be approved in one request."));

        var uniqueIds = requestedIds.Distinct().ToArray();
        var employees = await ApplyInstituteScope(db.Employees, context)
            .Where(employee => uniqueIds.Contains(employee.Id))
            .ToDictionaryAsync(employee => employee.Id, cancellationToken);

        var results = new List<BulkApproveEmployeeResult>(uniqueIds.Length);
        var approved = 0;
        var skipped = 0;
        var failed = 0;
        var changedIds = new List<Guid>();

        foreach (var employeeId in uniqueIds)
        {
            if (!employees.TryGetValue(employeeId, out var employee))
            {
                failed++;
                results.Add(new BulkApproveEmployeeResult(
                    employeeId,
                    "not-found",
                    SpmeErrorCodes.NotFound,
                    "Employee not found."));
                continue;
            }

            if (employee.IsHrApproved)
            {
                skipped++;
                results.Add(new BulkApproveEmployeeResult(
                    employeeId,
                    "skipped-already-approved",
                    null,
                    "Employee is already HR-approved."));
                continue;
            }

            if (!employee.ApproveHr())
            {
                skipped++;
                results.Add(new BulkApproveEmployeeResult(
                    employeeId,
                    "skipped-already-approved",
                    null,
                    "Employee is already HR-approved."));
                continue;
            }

            approved++;
            changedIds.Add(employeeId);
            results.Add(new BulkApproveEmployeeResult(
                employeeId,
                "approved",
                null,
                null));
        }

        if (changedIds.Count > 0)
        {
            foreach (var changedId in changedIds)
            {
                var employee = employees[changedId];
                await notifications.StageHrApprovalAccessAsync(employee.Id, employee.InstituteId, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            foreach (var employeeId in changedIds)
            {
                await audit.RecordAndSaveAsync(
                    "employees.verify.approve",
                    "Employee",
                    employeeId.ToString(),
                    "isHrApproved=false",
                    "isHrApproved=true",
                    cancellationToken);
            }
        }

        return TypedResults.Ok(new BulkApproveEmployeesResponse(approved, skipped, failed, results));
    }

    private static async Task<IResult> UploadEmployeeProfileImageAsync(
        Guid id,
        IFormFile file,
        SpmeDbContext db,
        HttpContext context,
        IFileStorageService storage,
        IProfileImageProcessor processor,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var employee = await ApplyInstituteScope(db.Employees, context)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (employee is null || !CanReplaceProfileImage(context, employee.Id))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        if (file.Length <= 0)
            return EndpointProblems.Unprocessable("Profile image is required.");
        if (file.Length > MaximumProfileImageBytes)
            return EndpointProblems.PayloadTooLarge("Profile image must be 5 MiB or smaller.");
        if (string.IsNullOrWhiteSpace(file.FileName))
            return EndpointProblems.Unprocessable("Profile image file name is required.");
        if (!SupportedProfileImageContentTypes.Contains(file.ContentType))
            return EndpointProblems.UnsupportedMediaType("Profile image must be a JPEG, PNG, WebP, or GIF file.");

        await using var uploadStream = file.OpenReadStream();
        ProfileImageProcessingResult normalized;
        try
        {
            normalized = await processor.ProcessAsync(uploadStream, cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return EndpointProblems.Unprocessable(exception.Message);
        }
        if (!string.Equals(normalized.SourceContentType, file.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            await normalized.Content.DisposeAsync();
            return EndpointProblems.UnsupportedMediaType(
                "The declared profile-image media type does not match the decoded image format.");
        }

        await using var normalizedContent = normalized.Content;
        var fileId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var storageKey = $"employee-profile-images/{employee.InstituteId:N}/{now:yyyy}/{now:MM}/{fileId:N}.webp";
        var upload = await storage.UploadAsync(normalizedContent, storageKey, normalized.ContentType, cancellationToken);
        var fileRecord = new FileRecord(
            upload.StorageKey,
            "profile.webp",
            normalized.ContentType,
            upload.SizeBytes,
            upload.Checksum,
            resourceType: "employee-profile-image",
            instituteId: employee.InstituteId,
            classification: "internal",
            id: fileId);
        var previousFile = employee.ProfileImageFileId.HasValue
            ? await db.FileRecords.FirstOrDefaultAsync(candidate => candidate.Id == employee.ProfileImageFileId.Value, cancellationToken)
            : null;
        db.FileRecords.Add(fileRecord);
        employee.UpdateProfileImage(fileRecord.Id);
        previousFile?.MarkDeleted(now);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteUploadedFileAsync(storage, upload.StorageKey, cancellationToken);
            throw;
        }

        if (previousFile is not null)
            await TryDeleteRetiredFileAsync(previousFile, db, storage, cancellationToken);
        await audit.RecordAndSaveAsync("employees.profile-image.upload", "Employee", employee.Id.ToString(), after: fileRecord.Id.ToString(), ct: cancellationToken);

        var response = await GetEmployeeDetailResponseAsync(employee.Id, db, context, cancellationToken)
            ?? throw new InvalidOperationException("Updated employee could not be loaded.");
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> GetEmployeeProfileImageAsync(
        Guid id,
        SpmeDbContext db,
        HttpContext context,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var image = await GetAccessibleProfileImageAsync(id, db, context, cancellationToken);
        if (image is null)
            return EndpointProblems.FromError(Error.NotFound("Profile image not found."));

        context.Response.Headers.CacheControl = "private, no-store";

        // Stream through the API so authenticated portal clients can display the
        // image without depending on browser-reachable object-storage hostnames.
        var content = await storage.DownloadAsync(image.StorageKey, cancellationToken);
        if (content is not null)
            return Results.File(content, image.ContentType);

        var access = await storage.CreateReadAccessAsync(image.StorageKey, cancellationToken);
        if (access is null)
            return EndpointProblems.FromError(Error.NotFound("Profile image not found."));

        return TypedResults.Redirect(access.Uri.AbsoluteUri, permanent: false, preserveMethod: true);
    }

    private static async Task<IResult> GetEmployeeProfileImageAccessAsync(
        Guid id,
        SpmeDbContext db,
        HttpContext context,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var image = await GetAccessibleProfileImageAsync(id, db, context, cancellationToken);
        if (image is null)
            return EndpointProblems.FromError(Error.NotFound("Profile image not found."));

        var access = await storage.CreateReadAccessAsync(image.StorageKey, cancellationToken);
        if (access is null)
            return EndpointProblems.FromError(Error.NotFound("Profile image not found."));

        context.Response.Headers.CacheControl = "private, no-store";
        return TypedResults.Ok(new ProfileImageAccessResponse(
            image.Id,
            access.Uri.AbsoluteUri,
            access.ExpiresAt,
            image.ContentType,
            FormatImageEtag(image.Checksum)));
    }

    private static async Task<Results<Ok<CollectionResponse<EmploymentRecordResponse>>, ProblemHttpResult>> GetEmploymentRecordsAsync(
        Guid employeeId,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var records = await db.EmploymentRecords.AsNoTracking()
            .Where(record => record.EmployeeId == employeeId)
            .OrderByDescending(record => record.EffectiveFrom)
            .ToListAsync(cancellationToken);
        var divisionNames = await GetDivisionNamesAsync(db, records.Select(record => record.DivisionId), cancellationToken);
        var sectionNames = await GetSectionNamesAsync(db, records.Select(record => record.SectionId), cancellationToken);
        var grades = await GetGradeLookupsAsync(db, records.Select(record => record.GradeId), cancellationToken);

        var items = records
            .Select(record =>
            {
                var grade = record.GradeId.HasValue ? grades.GetValueOrDefault(record.GradeId.Value) : default;
                return new EmploymentRecordResponse(
                record.Id,
                record.DivisionId,
                record.DivisionId.HasValue ? divisionNames.GetValueOrDefault(record.DivisionId.Value) : null,
                record.SectionId,
                record.SectionId.HasValue ? sectionNames.GetValueOrDefault(record.SectionId.Value) : null,
                record.JobTitle,
                ParseLeadershipRoles(record.LeadershipRoles),
                record.StaffCategory,
                record.GradeId,
                grade.Code,
                grade.Name,
                record.GradeStep,
                record.AreaOfSpecialization,
                record.ServiceStatus,
                record.Organization,
                record.Location,
                record.Region,
                record.District,
                record.PensionType,
                record.PensionId,
                record.AppointmentDate,
                record.PromotionDate,
                record.EffectiveFrom,
                record.EffectiveTo,
                record.IsCurrent);
            })
            .ToList();

        return TypedResults.Ok(new CollectionResponse<EmploymentRecordResponse>(items, items.Count));
    }

    private static async Task<Results<Ok<EmployeeSpouseResponse>, ProblemHttpResult>> GetSpouseAsync(
        Guid employeeId,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var spouse = await db.EmployeeSpouses.AsNoTracking()
            .Where(candidate => candidate.EmployeeId == employeeId)
            .Select(candidate => ToSpouseResponse(candidate))
            .FirstOrDefaultAsync(cancellationToken);

        return spouse is null
            ? EndpointProblems.FromError(Error.NotFound("Spouse record not found."))
            : TypedResults.Ok(spouse);
    }

    private static async Task<Results<Created<EmployeeSpouseResponse>, ProblemHttpResult>> CreateSpouseAsync(
        Guid employeeId,
        UpsertEmployeeSpouseRequest request,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateSpouse(request);
        if (validationError is not null)
            return EndpointProblems.FromError(validationError);
        if (!await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));
        if (await db.EmployeeSpouses.AnyAsync(spouse => spouse.EmployeeId == employeeId, cancellationToken))
            return EndpointProblems.FromError(Error.Conflict("Employee already has a spouse record."));

        var spouse = new EmployeeSpouse(
            employeeId,
            request.Name,
            request.DateOfBirth,
            request.Phone,
            request.Email,
            request.Occupation,
            request.Employer);

        db.EmployeeSpouses.Add(spouse);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/v2/employees/{employeeId}/spouse",
            ToSpouseResponse(spouse));
    }

    private static async Task<Results<Ok<EmployeeSpouseResponse>, ProblemHttpResult>> UpdateSpouseAsync(
        Guid employeeId,
        Guid spouseId,
        UpsertEmployeeSpouseRequest request,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateSpouse(request);
        if (validationError is not null)
            return EndpointProblems.FromError(validationError);
        if (!await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var spouse = await db.EmployeeSpouses
            .FirstOrDefaultAsync(candidate => candidate.Id == spouseId && candidate.EmployeeId == employeeId, cancellationToken);
        if (spouse is null)
            return EndpointProblems.FromError(Error.NotFound("Spouse record not found."));

        spouse.Update(request.Name, request.DateOfBirth, request.Phone, request.Email, request.Occupation, request.Employer);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(ToSpouseResponse(spouse));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteSpouseAsync(
        Guid employeeId,
        Guid spouseId,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var spouse = await db.EmployeeSpouses
            .FirstOrDefaultAsync(candidate => candidate.Id == spouseId && candidate.EmployeeId == employeeId, cancellationToken);
        if (spouse is null)
            return EndpointProblems.FromError(Error.NotFound("Spouse record not found."));

        db.EmployeeSpouses.Remove(spouse);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<CollectionResponse<EducationRecordResponse>>, ProblemHttpResult>> GetEducationAsync(
        Guid employeeId,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var items = await db.EducationRecords.AsNoTracking()
            .Where(record => record.EmployeeId == employeeId)
            .OrderByDescending(record => record.DateCompleted)
            .ThenBy(record => record.InstitutionName)
            .Select(record => ToEducationResponse(record))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(new CollectionResponse<EducationRecordResponse>(items, items.Count));
    }

    private static async Task<Results<Ok<EducationRecordResponse>, ProblemHttpResult>> GetEducationRecordAsync(
        Guid employeeId, Guid educationId, SpmeDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        if (!await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Education record not found."));
        var record = await db.EducationRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == educationId && x.EmployeeId == employeeId, cancellationToken);
        return record is null
            ? EndpointProblems.FromError(Error.NotFound("Education record not found."))
            : TypedResults.Ok(ToEducationResponse(record));
    }

    private static async Task<Results<Created<EducationRecordResponse>, ProblemHttpResult>> CreateEducationAsync(
        Guid employeeId, UpsertEducationRecordRequest request, SpmeDbContext db, HttpContext context,
        IAuditService audit, CancellationToken cancellationToken)
    {
        if (!CanManageSelfRecord(context, employeeId) ||
            !await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));
        var validation = ValidateEducation(request, out var certificateAwarded, out var qualificationLevel);
        if (validation is not null) return EndpointProblems.FromError(validation);

        var record = new EducationRecord(employeeId, request.InstitutionName, request.CourseStudied,
            certificateAwarded, qualificationLevel, request.Grade,
            request.Specialization, request.ProfessionalQualifications, request.Affiliations,
            request.CertificateNumber, request.DateCommenced, request.DateCompleted);
        db.EducationRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAndSaveAsync("education-record.created", "EducationRecord", record.Id.ToString(), null,
            $"employee={employeeId}", cancellationToken);
        return TypedResults.Created($"/api/v2/employees/{employeeId}/education/{record.Id}", ToEducationResponse(record));
    }

    private static async Task<Results<Ok<EducationRecordResponse>, ProblemHttpResult>> UpdateEducationAsync(
        Guid employeeId, Guid educationId, UpsertEducationRecordRequest request, SpmeDbContext db,
        HttpContext context, IAuditService audit, CancellationToken cancellationToken)
    {
        if (!CanManageSelfRecord(context, employeeId) ||
            !await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Education record not found."));
        var validation = ValidateEducation(request, out var certificateAwarded, out var qualificationLevel);
        if (validation is not null) return EndpointProblems.FromError(validation);
        var record = await db.EducationRecords.SingleOrDefaultAsync(
            x => x.Id == educationId && x.EmployeeId == employeeId, cancellationToken);
        if (record is null) return EndpointProblems.FromError(Error.NotFound("Education record not found."));

        record.UpdateStaffDetails(request.InstitutionName, request.CourseStudied, certificateAwarded,
            qualificationLevel, request.Grade, request.Specialization,
            request.ProfessionalQualifications, request.Affiliations, request.CertificateNumber,
            request.DateCommenced, request.DateCompleted);
        if (IsCurrentEmployee(context, employeeId) && !HasHrWriteAccess(context))
            record.ResetHrReview();
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAndSaveAsync("education-record.updated", "EducationRecord", record.Id.ToString(), null,
            $"employee={employeeId};hr-review-fields=preserved", cancellationToken);
        return TypedResults.Ok(ToEducationResponse(record));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteEducationAsync(
        Guid employeeId, Guid educationId, SpmeDbContext db, HttpContext context,
        IAuditService audit, CancellationToken cancellationToken)
    {
        if (!CanManageSelfRecord(context, employeeId) ||
            !await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Education record not found."));
        if (IsCurrentEmployee(context, employeeId) && !HasHrWriteAccess(context))
            return EndpointProblems.FromError(Error.Forbidden("Staff cannot delete education records."));
        var record = await db.EducationRecords.SingleOrDefaultAsync(
            x => x.Id == educationId && x.EmployeeId == employeeId, cancellationToken);
        if (record is null) return EndpointProblems.FromError(Error.NotFound("Education record not found."));
        if (await db.PromotionQualificationAssessments.AsNoTracking()
            .AnyAsync(x => x.EducationRecordId == educationId, cancellationToken))
            return EndpointProblems.FromError(Error.Conflict("An education record used by a promotion assessment cannot be deleted."));

        db.EducationRecords.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAndSaveAsync("education-record.deleted", "EducationRecord", record.Id.ToString(),
            $"employee={employeeId}", null, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<EducationRecordResponse>, ProblemHttpResult>> ReviewEducationAsync(
        Guid employeeId,
        Guid educationId,
        ReviewEducationRecordRequest request,
        SpmeDbContext db,
        HttpContext context,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Education record not found."));

        if (string.IsNullOrWhiteSpace(request.InstitutionRecognitionStatus) &&
            string.IsNullOrWhiteSpace(request.RelevantFieldStatus))
            return EndpointProblems.FromError(Error.Validation("Provide at least one education review status."));

        var record = await db.EducationRecords.SingleOrDefaultAsync(
            x => x.Id == educationId && x.EmployeeId == employeeId, cancellationToken);
        if (record is null)
            return EndpointProblems.FromError(Error.NotFound("Education record not found."));

        try
        {
            if (!string.IsNullOrWhiteSpace(request.InstitutionRecognitionStatus))
            {
                if (string.Equals(request.InstitutionRecognitionStatus.Trim(), "verified", StringComparison.OrdinalIgnoreCase) &&
                    !await EmployeeProfileEndpoints.HasCleanCertificateForEducationAsync(db, employeeId, record, cancellationToken))
                    return EndpointProblems.FromError(Error.Conflict(
                        "Upload and scan the matching degree certificate before HR can verify this education record."));
                record.SetInstitutionRecognitionStatus(request.InstitutionRecognitionStatus);
            }
            if (!string.IsNullOrWhiteSpace(request.RelevantFieldStatus))
            {
                if (string.Equals(request.RelevantFieldStatus.Trim(), "verified", StringComparison.OrdinalIgnoreCase) &&
                    !await EmployeeProfileEndpoints.HasCleanCertificateForEducationAsync(db, employeeId, record, cancellationToken))
                    return EndpointProblems.FromError(Error.Conflict(
                        "Upload and scan the matching degree certificate before HR can verify this education record."));
                Guid? reviewerId = Guid.TryParse(
                    context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    out var userId)
                    ? userId
                    : null;
                record.SetRelevantFieldStatus(request.RelevantFieldStatus, reviewerId, DateTimeOffset.UtcNow);
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return EndpointProblems.FromError(Error.Validation(ex.Message));
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAndSaveAsync(
            "education-record.reviewed",
            "EducationRecord",
            record.Id.ToString(),
            null,
            $"employee={employeeId};institution={record.InstitutionRecognitionStatus};field={record.RelevantFieldStatus}",
            cancellationToken);
        return TypedResults.Ok(ToEducationResponse(record));
    }

    private static async Task<Results<Ok<CollectionResponse<EmployeeChildResponse>>, ProblemHttpResult>> GetChildrenAsync(
        Guid employeeId,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var items = await db.EmployeeChildren.AsNoTracking()
            .Where(child => child.EmployeeId == employeeId)
            .OrderBy(child => child.DateOfBirth)
            .Select(child => ToChildResponse(child))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new CollectionResponse<EmployeeChildResponse>(items, items.Count));
    }

    private static async Task<Results<Created<EmployeeChildResponse>, ProblemHttpResult>> CreateChildAsync(
        Guid employeeId,
        UpsertEmployeeChildRequest request,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!CanManageSelfRecord(context, employeeId))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));
        var validationError = ValidateChild(request);
        if (validationError is not null)
            return EndpointProblems.FromError(validationError);
        if (!await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var childCount = await db.EmployeeChildren.CountAsync(child => child.EmployeeId == employeeId, cancellationToken);
        if (childCount >= MaximumChildrenPerEmployee)
            return EndpointProblems.FromError(Error.Conflict("Employee cannot have more than two child records."));

        var child = new EmployeeChild(
            employeeId,
            request.Name,
            request.DateOfBirth,
            NormalizeChildGender(request.Gender),
            request.BirthCertificateNumber,
            request.BirthCertificateFileId);

        db.EmployeeChildren.Add(child);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/v2/employees/{employeeId}/children/{child.Id}",
            ToChildResponse(child));
    }

    private static async Task<Results<Ok<EmployeeChildResponse>, ProblemHttpResult>> UpdateChildAsync(
        Guid employeeId,
        Guid childId,
        UpsertEmployeeChildRequest request,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!CanManageSelfRecord(context, employeeId))
            return EndpointProblems.FromError(Error.NotFound("Child record not found."));
        var validationError = ValidateChild(request);
        if (validationError is not null)
            return EndpointProblems.FromError(validationError);
        if (!await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var child = await db.EmployeeChildren
            .FirstOrDefaultAsync(candidate => candidate.Id == childId && candidate.EmployeeId == employeeId, cancellationToken);
        if (child is null)
            return EndpointProblems.FromError(Error.NotFound("Child record not found."));

        child.Update(
            request.Name,
            request.DateOfBirth,
            NormalizeChildGender(request.Gender),
            request.BirthCertificateNumber,
            request.BirthCertificateFileId);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(ToChildResponse(child));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteChildAsync(
        Guid employeeId,
        Guid childId,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!CanManageSelfRecord(context, employeeId))
            return EndpointProblems.FromError(Error.NotFound("Child record not found."));
        if (IsCurrentEmployee(context, employeeId) && !HasHrWriteAccess(context))
            return EndpointProblems.FromError(Error.Forbidden("Staff cannot delete dependant records."));
        if (!await EmployeeExistsAsync(employeeId, db, context, cancellationToken))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var child = await db.EmployeeChildren
            .FirstOrDefaultAsync(candidate => candidate.Id == childId && candidate.EmployeeId == employeeId, cancellationToken);
        if (child is null)
            return EndpointProblems.FromError(Error.NotFound("Child record not found."));

        db.EmployeeChildren.Remove(child);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    private static IQueryable<Employee> ApplyInstituteScope(IQueryable<Employee> query, HttpContext context)
    {
        var instituteId = CurrentInstituteId(context);
        return instituteId.HasValue
            ? query.Where(employee => employee.InstituteId == instituteId.Value)
            : query;
    }

    private static async Task<bool> EmployeeExistsAsync(
        Guid employeeId,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!CanReadSelfRecord(context, employeeId)) return false;
        return await ApplyInstituteScope(db.Employees.AsNoTracking(), context)
            .AnyAsync(employee => employee.Id == employeeId, cancellationToken);
    }

    internal static bool CanReadEmployeeRecord(HttpContext context, Guid employeeId) =>
        CanReadSelfRecord(context, employeeId);

    private static bool HasHrWriteAccess(HttpContext context) =>
        context.User.IsInRole(SpmeRoles.PlatformAdmin) ||
        context.User.IsInRole(SpmeRoles.HrAdmin) ||
        InstituteStaffAccess.HasStaffManagementWriteCompatibility(context.User);

    private static bool CanReadSelfRecord(HttpContext context, Guid employeeId) =>
        IsCurrentEmployee(context, employeeId) ||
        context.User.IsInRole(SpmeRoles.PlatformAdmin) ||
        context.User.IsInRole(SpmeRoles.InstituteAdmin) ||
        context.User.IsInRole(SpmeRoles.HrAdmin) ||
        InstituteStaffAccess.HasStaffManagementReadCompatibility(context.User);

    private static bool CanManageSelfRecord(HttpContext context, Guid employeeId) =>
        IsCurrentEmployee(context, employeeId) ||
        context.User.IsInRole(SpmeRoles.PlatformAdmin) ||
        context.User.IsInRole(SpmeRoles.HrAdmin) ||
        InstituteStaffAccess.HasStaffManagementWriteCompatibility(context.User);

    private static bool IsCurrentEmployee(HttpContext context, Guid employeeId) =>
        Guid.TryParse(context.User.FindFirst("employee_id")?.Value, out var currentEmployeeId) &&
        currentEmployeeId == employeeId;

    private static async Task<Dictionary<Guid, EmployeeCurrentEmploymentSummary>> GetCurrentEmploymentByEmployeeIdAsync(
        SpmeDbContext db,
        IEnumerable<Guid> employeeIds,
        CancellationToken cancellationToken)
    {
        var scopedEmployeeIds = employeeIds.Distinct().ToArray();
        if (scopedEmployeeIds.Length == 0)
            return [];

        var currentRecords = await db.EmploymentRecords.AsNoTracking()
            .Where(record => scopedEmployeeIds.Contains(record.EmployeeId) && record.IsCurrent)
            .OrderByDescending(record => record.EffectiveFrom)
            .ToListAsync(cancellationToken);
        var divisionNames = await GetDivisionNamesAsync(db, currentRecords.Select(record => record.DivisionId), cancellationToken);
        var sectionNames = await GetSectionNamesAsync(db, currentRecords.Select(record => record.SectionId), cancellationToken);
        var grades = await GetGradeLookupsAsync(db, currentRecords.Select(record => record.GradeId), cancellationToken);

        return currentRecords
            .GroupBy(record => record.EmployeeId)
            .ToDictionary(group => group.Key, group =>
            {
                var record = group.First();
                var grade = record.GradeId.HasValue ? grades.GetValueOrDefault(record.GradeId.Value) : default;
                return new EmployeeCurrentEmploymentSummary(
                    record.DivisionId,
                    record.DivisionId.HasValue ? divisionNames.GetValueOrDefault(record.DivisionId.Value) : null,
                    record.SectionId,
                    record.SectionId.HasValue ? sectionNames.GetValueOrDefault(record.SectionId.Value) : null,
                    record.JobTitle,
                    ParseLeadershipRoles(record.LeadershipRoles),
                    record.StaffCategory,
                    record.GradeId,
                    grade.Code,
                    grade.Name,
                    record.GradeStep,
                    record.AreaOfSpecialization,
                    record.ServiceStatus,
                    record.Organization,
                    record.Location,
                    record.Region,
                    record.District,
                    record.PensionType,
                    record.PensionId,
                    record.AppointmentDate,
                    record.PromotionDate);
            });
    }

    private static async Task<Dictionary<Guid, DateTime?>> GetOnLeaveUntilByEmployeeIdAsync(
        SpmeDbContext db,
        IEnumerable<Guid> employeeIds,
        CancellationToken cancellationToken)
    {
        var scopedEmployeeIds = employeeIds.Distinct().ToArray();
        if (scopedEmployeeIds.Length == 0)
            return [];

        var today = DateTime.UtcNow.Date;
        var activeStatuses = new[]
        {
            LeaveRequestStatuses.Approved,
            LeaveRequestStatuses.ResumptionPending
        };

        var rows = await db.LeaveRequests.AsNoTracking()
            .Where(request =>
                scopedEmployeeIds.Contains(request.EmployeeId) &&
                activeStatuses.Contains(request.Status) &&
                request.StartDate <= today &&
                request.EndDate >= today)
            .Select(request => new { request.EmployeeId, request.EndDate })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => (DateTime?)group.Max(row => row.EndDate.Date));
    }

    private static async Task<Dictionary<Guid, decimal>> GetRemainingAnnualLeaveDaysByEmployeeIdAsync(
        SpmeDbContext db,
        IEnumerable<Guid> employeeIds,
        CancellationToken cancellationToken)
    {
        var scopedEmployeeIds = employeeIds.Distinct().ToArray();
        if (scopedEmployeeIds.Length == 0)
            return [];

        var leaveYear = (short)DateTime.UtcNow.Year;
        var rows = await db.LeaveBalances.AsNoTracking()
            .Where(balance =>
                scopedEmployeeIds.Contains(balance.EmployeeId) &&
                balance.LeaveType == LeaveTypes.Annual &&
                balance.LeaveYear == leaveYear)
            .Select(balance => new
            {
                balance.EmployeeId,
                RemainingDays = balance.TotalDays + balance.AdjustedDays - balance.UsedDays - balance.PendingDays
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.EmployeeId)
            .ToDictionary(group => group.Key, group => group.First().RemainingDays);
    }

    private static async Task<EmployeeDetailResponse?> GetEmployeeDetailResponseAsync(
        Guid id,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var employee = await ApplyInstituteScope(db.Employees.AsNoTracking(), context)
            .Where(candidate => candidate.Id == id)
            .GroupJoin(
                db.Institutes.AsNoTracking(),
                employee => employee.InstituteId,
                institute => institute.Id,
                (employee, institutes) => new { employee, institutes })
            .SelectMany(
                item => item.institutes.DefaultIfEmpty(),
                (item, institute) => new
                {
                    item.employee.Id,
                    item.employee.StaffId,
                    item.employee.Prefix,
                    item.employee.Surname,
                    item.employee.OtherNames,
                    item.employee.PreferredName,
                    item.employee.Gender,
                    item.employee.DateOfBirth,
                    item.employee.Nationality,
                    item.employee.Religion,
                    item.employee.MaritalStatus,
                    item.employee.PrimaryEmail,
                    item.employee.Phone,
                    item.employee.ProfileStatus,
                    item.employee.IsHrApproved,
                    item.employee.IsContactVerified,
                    item.employee.CreatedAt,
                    item.employee.UpdatedAt,
                    item.employee.ProfileImageFileId,
                    Institute = institute == null
                        ? null
                        : new EmployeeInstituteSummary(institute.Id, institute.Code, institute.Name, institute.Kind)
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (employee is null)
            return null;

        var currentEmployment = await GetCurrentEmploymentByEmployeeIdAsync(db, [employee.Id], cancellationToken);
        var profileImages = await GetProfileImageReferencesAsync(
            db,
            [(employee.Id, employee.ProfileImageFileId)],
            cancellationToken);

        return new EmployeeDetailResponse(
            employee.Id,
            employee.StaffId,
            employee.Prefix,
            employee.Surname,
            employee.OtherNames,
            employee.PreferredName,
            employee.Gender,
            employee.DateOfBirth,
            employee.Nationality,
            employee.Religion,
            employee.MaritalStatus,
            employee.PrimaryEmail,
            employee.Phone,
            employee.ProfileStatus,
            employee.IsHrApproved,
            employee.IsContactVerified,
            employee.CreatedAt,
            employee.UpdatedAt,
            employee.ProfileImageFileId,
            profileImages.GetValueOrDefault(employee.Id),
            employee.Institute,
            currentEmployment.GetValueOrDefault(employee.Id));
    }

    private static async Task<UpsertEmployeeRequest> NormalizeEmploymentRequestAsync(
        UpsertEmployeeRequest request,
        SpmeDbContext db,
        CancellationToken cancellationToken)
    {
        if (!request.GradeId.HasValue)
            return request;

        var gradeName = await db.Grades.AsNoTracking()
            .Where(grade => grade.Id == request.GradeId.Value && grade.IsActive)
            .Select(grade => grade.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(gradeName) ? request : request with { JobTitle = gradeName };
    }

    private static EmploymentRecord CreateEmploymentRecord(Guid employeeId, Guid instituteId, UpsertEmployeeRequest request) =>
        new(
            employeeId,
            instituteId,
            request.DivisionId,
            request.SectionId,
            null,
            request.GradeId,
            request.JobTitle,
            SerializeLeadershipRoles(request.LeadershipRoles),
            request.StaffCategory,
            request.GradeStep,
            request.AreaOfSpecialization,
            NormalizeStatus(request.ServiceStatus, "active"),
            request.Organization,
            request.Location,
            request.Region,
            request.District,
            request.AppointmentDate,
            request.PromotionDate,
            request.PensionType,
            request.PensionId,
            DateTime.UtcNow.Date,
            true);

    private static async Task<Error?> ValidateEmployeeRequestAsync(
        UpsertEmployeeRequest request,
        Guid? employeeId,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.StaffId))
            return Error.Validation("Staff ID is required.");
        if (string.IsNullOrWhiteSpace(request.Surname))
            return Error.Validation("Surname is required.");
        if (string.IsNullOrWhiteSpace(request.Gender))
            return Error.Validation("Gender is required.");
        if (!string.IsNullOrWhiteSpace(request.PrimaryEmail) && !request.PrimaryEmail.Contains('@', StringComparison.Ordinal))
            return Error.Validation("A valid email address is required.");
        if (!string.IsNullOrWhiteSpace(request.ProfileStatus) &&
            !EmployeeProfileStatuses.All.Contains(request.ProfileStatus.Trim(), StringComparer.OrdinalIgnoreCase))
            return Error.Validation("Profile status is not supported.");
        var serializedLeadershipRoles = SerializeLeadershipRoles(request.LeadershipRoles);
        if (serializedLeadershipRoles?.Length > 512)
            return Error.Validation("Leadership roles must be 512 characters or fewer.");

        var instituteId = ResolveInstituteId(request, context);
        if (!instituteId.HasValue)
            return Error.Validation("Institute is required.");
        if (!await db.Institutes.AsNoTracking().AnyAsync(institute => institute.Id == instituteId.Value && institute.IsActive, cancellationToken))
            return Error.Validation("Selected institute is not available.");

        var normalizedStaffId = request.StaffId.Trim().ToUpperInvariant();
        var staffIdExists = await db.Employees.AsNoTracking()
            .AnyAsync(employee =>
                employee.InstituteId == instituteId.Value &&
                employee.NormalizedStaffId == normalizedStaffId &&
                (!employeeId.HasValue || employee.Id != employeeId.Value),
                cancellationToken);
        if (staffIdExists)
            return Error.Conflict("An employee with this staff ID already exists in the institute.");

        if (!string.IsNullOrWhiteSpace(request.PrimaryEmail))
        {
            var normalizedEmail = request.PrimaryEmail.Trim().ToUpperInvariant();
            var emailExists = await db.Employees.AsNoTracking()
                .AnyAsync(employee =>
                    employee.NormalizedPrimaryEmail == normalizedEmail &&
                    (!employeeId.HasValue || employee.Id != employeeId.Value),
                    cancellationToken);
            if (emailExists)
                return Error.Conflict("An employee with this email already exists.");
        }

        if (request.DivisionId.HasValue)
        {
            var divisionExists = await db.Divisions.AsNoTracking()
                .AnyAsync(division => division.Id == request.DivisionId.Value && division.InstituteId == instituteId.Value && division.IsActive, cancellationToken);
            if (!divisionExists)
                return Error.Validation("Selected division is not available for the employee institute.");
        }

        if (request.SectionId.HasValue)
        {
            var section = await db.Sections.AsNoTracking()
                .Where(candidate => candidate.Id == request.SectionId.Value && candidate.IsActive)
                .Select(candidate => new { candidate.DivisionId })
                .FirstOrDefaultAsync(cancellationToken);
            if (section is null)
                return Error.Validation("Selected section is not available.");

            var sectionDivision = await db.Divisions.AsNoTracking()
                .Where(division => division.Id == section.DivisionId)
                .Select(division => new { division.Id, division.InstituteId })
                .FirstOrDefaultAsync(cancellationToken);
            if (sectionDivision is null || sectionDivision.InstituteId != instituteId.Value)
                return Error.Validation("Selected section is not available for the employee institute.");
            if (request.DivisionId.HasValue && request.DivisionId.Value != sectionDivision.Id)
                return Error.Validation("Selected section does not belong to the selected division.");
        }

        if (request.GradeId.HasValue)
        {
            var grade = await db.Grades.AsNoTracking()
                .Where(candidate => candidate.Id == request.GradeId.Value && candidate.IsActive)
                .Select(candidate => new { candidate.StaffCategory })
                .FirstOrDefaultAsync(cancellationToken);
            if (grade is null)
                return Error.Validation("Selected grade is not available.");
            if (!string.IsNullOrWhiteSpace(request.StaffCategory) &&
                !string.IsNullOrWhiteSpace(grade.StaffCategory) &&
                !string.Equals(grade.StaffCategory, request.StaffCategory.Trim(), StringComparison.OrdinalIgnoreCase))
                return Error.Validation("Selected grade does not belong to the selected staff category.");
        }

        return null;
    }

    private static Guid? ResolveInstituteId(UpsertEmployeeRequest request, HttpContext context)
    {
        var scopedInstituteId = CurrentInstituteId(context);
        return scopedInstituteId ?? request.InstituteId;
    }

    private static string NormalizeStatus(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();

    private static string? SerializeLeadershipRoles(IReadOnlyList<string>? roles)
    {
        var normalizedRoles = roles?
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalizedRoles is { Length: > 0 }
            ? string.Join(LeadershipRoleDelimiter, normalizedRoles)
            : null;
    }

    private static string[] ParseLeadershipRoles(string? roles) =>
        string.IsNullOrWhiteSpace(roles)
            ? []
            : roles.Split(LeadershipRoleSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(role => role.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static string NormalizeStatusToken(string value) =>
        value.Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');

    private static string[] SplitStatusTokens(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeStatusToken)
                .Where(token => token.Length > 0)
                .ToArray();

    private static string ToProfileStatus(string token) => token switch
    {
        "pending" => "pending-hr-approval",
        _ => token
    };

    private static bool IsServiceStatusToken(string token) =>
        token is "active" or "inactive" or "on-leave" or "retired";

    private static async Task<Dictionary<Guid, ProfileImageReferenceResponse>> GetProfileImageReferencesAsync(
        SpmeDbContext db,
        IEnumerable<(Guid EmployeeId, Guid? FileId)> employees,
        CancellationToken cancellationToken)
    {
        var employeeFiles = employees
            .Where(item => item.FileId.HasValue)
            .ToDictionary(item => item.EmployeeId, item => item.FileId!.Value);
        if (employeeFiles.Count == 0)
            return [];

        var fileIds = employeeFiles.Values.Distinct().ToArray();
        var files = await db.FileRecords.AsNoTracking()
            .Where(file => fileIds.Contains(file.Id) && !file.IsDeleted)
            .ToDictionaryAsync(file => file.Id, cancellationToken);

        return employeeFiles
            .Where(item => files.ContainsKey(item.Value))
            .ToDictionary(
                item => item.Key,
                item => MapProfileImageReference(item.Key, files[item.Value]));
    }

    private static ProfileImageReferenceResponse MapProfileImageReference(Guid employeeId, FileRecord file) => new(
        file.Id,
        $"/api/v2/employees/{employeeId}/profile-image",
        $"/api/v2/employees/{employeeId}/profile-image/access",
        file.ContentType,
        FormatImageEtag(file.Checksum));

    private static string FormatImageEtag(string checksum) => $"\"sha256-{checksum}\"";

    private static async Task<FileRecord?> GetAccessibleProfileImageAsync(
        Guid employeeId,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var employee = await ApplyInstituteScope(db.Employees.AsNoTracking(), context)
            .Where(candidate => candidate.Id == employeeId)
            .Select(candidate => new { candidate.ProfileImageFileId })
            .FirstOrDefaultAsync(cancellationToken);
        if (employee?.ProfileImageFileId is null)
            return null;

        return await db.FileRecords.AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == employee.ProfileImageFileId.Value && !candidate.IsDeleted,
                cancellationToken);
    }

    private static bool CanReplaceProfileImage(HttpContext context, Guid employeeId)
    {
        if (context.User.IsInRole(SpmeRoles.PlatformAdmin) ||
            context.User.IsInRole(SpmeRoles.HrAdmin) ||
            InstituteStaffAccess.HasStaffManagementWriteCompatibility(context.User))
            return true;

        var claim = context.User.FindFirst("employee_id")?.Value;
        return Guid.TryParse(claim, out var currentEmployeeId) && currentEmployeeId == employeeId;
    }

    private static async Task TryDeleteUploadedFileAsync(
        IFileStorageService storage,
        string storageKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeleteAsync(storageKey, cancellationToken);
        }
        catch (FileStorageUnavailableException)
        {
            // An age-based storage lifecycle rule removes rare upload orphans after compensation failure.
        }
    }

    private static async Task TryDeleteRetiredFileAsync(
        FileRecord file,
        SpmeDbContext db,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeleteAsync(file.StorageKey, cancellationToken);
            file.MarkStorageDeleted(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (FileStorageUnavailableException)
        {
            // The bounded cleanup worker retries deleted records whose storage deletion is incomplete.
        }
    }

    private static async Task<Dictionary<Guid, (string Code, string Name)>> GetGradeLookupsAsync(
        SpmeDbContext db,
        IEnumerable<Guid?> gradeIds,
        CancellationToken cancellationToken)
    {
        var ids = gradeIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        return await db.Grades.AsNoTracking()
            .Where(grade => ids.Contains(grade.Id))
            .ToDictionaryAsync(grade => grade.Id, grade => (grade.Code, grade.Name), cancellationToken);
    }

    private static async Task<Dictionary<Guid, string>> GetDivisionNamesAsync(
        SpmeDbContext db,
        IEnumerable<Guid?> divisionIds,
        CancellationToken cancellationToken)
    {
        var ids = divisionIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        return await db.Divisions.AsNoTracking()
            .Where(division => ids.Contains(division.Id))
            .ToDictionaryAsync(division => division.Id, division => division.Name, cancellationToken);
    }

    private static async Task<Dictionary<Guid, string>> GetSectionNamesAsync(
        SpmeDbContext db,
        IEnumerable<Guid?> sectionIds,
        CancellationToken cancellationToken)
    {
        var ids = sectionIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        return await db.Sections.AsNoTracking()
            .Where(section => ids.Contains(section.Id))
            .ToDictionaryAsync(section => section.Id, section => section.Name, cancellationToken);
    }

    private static Guid? CurrentInstituteId(HttpContext context)
    {
        var value = context.User.FindFirst("institute_id")?.Value;
        return Guid.TryParse(value, out var instituteId) ? instituteId : null;
    }

    private static Error? ValidateSpouse(UpsertEmployeeSpouseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Error.Validation("Spouse name is required.");

        return null;
    }

    private static Error? ValidateChild(UpsertEmployeeChildRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Error.Validation("Child name is required.");
        if (request.DateOfBirth == default)
            return Error.Validation("Child date of birth is required.");
        if (string.IsNullOrWhiteSpace(request.Gender))
            return Error.Validation("Child gender is required.");
        if (!ChildGenders.All.Contains(request.Gender.Trim(), StringComparer.OrdinalIgnoreCase))
            return Error.Validation("Child gender must be male or female.");
        if (request.BirthCertificateFileId.HasValue)
            return Error.Validation("A birth certificate cannot be attached until secure ownership, institute, purpose, and malware-scan verification is available.");

        return null;
    }

    private static string NormalizeChildGender(string gender) => gender.Trim().ToLowerInvariant();

    private static Error? ValidateEducation(
        UpsertEducationRecordRequest request,
        out string certificateAwarded,
        out string qualificationLevel)
    {
        certificateAwarded = string.Empty;
        qualificationLevel = request.QualificationLevel?.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(request.InstitutionName) ||
            string.IsNullOrWhiteSpace(request.CourseStudied) ||
            string.IsNullOrWhiteSpace(request.CertificateAwarded))
            return Error.Validation("Institution, course studied, and certificate awarded are required.");

        if (!QualificationLevels.All.Contains(qualificationLevel))
            return Error.Validation("Qualification level is not supported.");
        if (!EducationCertificateCatalog.TryResolve(request.CertificateAwarded, out var certificate))
            return Error.Validation("Certificate awarded must be a supported certificate type.");
        if (!certificate.AllowsQualificationLevel(qualificationLevel))
            return Error.Validation("Certificate awarded does not match the selected qualification level.");
        if (request.DateCommenced.HasValue && request.DateCompleted.HasValue &&
            request.DateCompleted.Value.Date < request.DateCommenced.Value.Date)
            return Error.Validation("Education completion date cannot precede commencement date.");

        certificateAwarded = certificate.Code;
        return null;
    }

    private static EducationRecordResponse ToEducationResponse(EducationRecord record) => new(
        record.Id, record.EmployeeId, record.InstitutionName, record.CourseStudied,
        EducationCertificateCatalog.Canonicalize(record.CertificateAwarded), record.QualificationLevel, record.Grade, record.Specialization,
        record.ProfessionalQualifications, record.Affiliations, record.CertificateNumber,
        record.DateCommenced, record.DateCompleted, record.InstitutionRecognitionStatus,
        record.InstitutionRecognitionEvidenceFileId, record.RelevantFieldStatus,
        record.CertificateFileId, Csir.Spme.Application.Common.ConcurrencyToken.Format(record.RowVersion),
        record.CreatedAt, record.UpdatedAt);

    private static EmployeeSpouseResponse ToSpouseResponse(EmployeeSpouse spouse) =>
        new(
            spouse.Id,
            spouse.EmployeeId,
            spouse.Name,
            spouse.DateOfBirth,
            spouse.Phone,
            spouse.Email,
            spouse.Occupation,
            spouse.Employer,
            spouse.CreatedAt,
            spouse.UpdatedAt);

    private static EmployeeChildResponse ToChildResponse(EmployeeChild child) =>
        new(
            child.Id,
            child.EmployeeId,
            child.Name,
            child.DateOfBirth,
            child.Gender,
            child.BirthCertificateNumber,
            child.BirthCertificateFileId,
            child.CreatedAt,
            child.UpdatedAt);

    private static IResult GetEducationCertificateTypesAsync(string? qualificationLevel)
    {
        var items = EducationCertificateCatalog.ForQualificationLevel(qualificationLevel)
            .Select(item => new EducationCertificateTypeResponse(
                item.Code,
                item.Label,
                item.Name,
                item.QualificationLevel,
                item.IsOpenAward))
            .ToList();
        return TypedResults.Ok(new CollectionResponse<EducationCertificateTypeResponse>(items, items.Count));
    }

    private static async Task<IResult> GetGradesAsync(
        SpmeDbContext db,
        string? staffCategory,
        CancellationToken cancellationToken)
    {
        var query = db.Grades.AsNoTracking().Where(grade => grade.IsActive);
        if (!string.IsNullOrWhiteSpace(staffCategory))
            query = query.Where(grade => grade.StaffCategory == staffCategory);

        var items = await query.OrderBy(grade => grade.Rank)
            .Select(grade => new GradeResponse(
                grade.Id,
                grade.Code,
                grade.Name,
                grade.StaffCategory,
                grade.PromotionStream,
                grade.PromotionLevel,
                grade.IsPromotionGrade))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(new CollectionResponse<GradeResponse>(items, items.Count));
    }
}
