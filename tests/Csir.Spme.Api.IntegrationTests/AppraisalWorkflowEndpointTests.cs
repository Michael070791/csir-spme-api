using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Communications;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class AppraisalWorkflowEndpointTests(SpmeApiFactory factory) : IClassFixture<SpmeApiFactory>
{
    [Fact]
    public async Task Full_Workflow_Enforces_Concurrency_Refusals_Privacy_And_Final_Document_Gating()
    {
        var seed = await SeedAsync();
        using var employee = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.EmployeeId, SpmePermissions.AppraisalsSelf);
        using var hod = Client(seed.HodUserId, SpmeRoles.HeadOfSection, seed.InstituteId,
            seed.HodEmployeeId, SpmePermissions.AppraisalsReview);
        using var director = Client(seed.DirectorUserId, SpmeRoles.InstituteDirector, seed.InstituteId,
            seed.DirectorEmployeeId, SpmePermissions.AppraisalsFinalApprove);
        using var hr = Client(seed.HrUserId, SpmeRoles.HrAdmin, seed.InstituteId, null,
            SpmePermissions.AppraisalsAdmin, SpmePermissions.AppraisalsFinalRead);

        var initial = await employee.GetAsync($"/api/v2/performance-appraisals/{seed.AppraisalId}");
        var initialBody = await initial.Content.ReadAsStringAsync();
        initial.StatusCode.Should().Be(HttpStatusCode.OK, "because the API returned: {0}", initialBody);
        var initialEtag = Etag(initial);
        var initialDetail = await Detail(initial);
        initialDetail.Summary.AvailableActions.Should().Contain(["save-planning", "submit-planning"]);
        initialDetail.Summary.CurrentStage.Should().Be(AppraisalStatuses.Planning);
        initialDetail.Employee.PresentGrade.Should().Be("Research Scientist");
        initialDetail.Appraiser.Surname.Should().Be("Reviewer");

        (await employee.GetAsync($"/api/v2/performance-appraisals/{seed.AppraisalId}/document"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var planning = new SaveAppraisalPlanningRequest(
            [new AppraisalTrainingEntry("CSIR College of Science", new DateTime(2026, 2, 12), "Research leadership")],
            [new AppraisalTargetInput(null, "Research delivery", "Publish one peer-reviewed paper", "Laboratory and library access", "By 30 November 2026")],
            ["Scientific communication"]);
        var savedPlanning = await Patch(employee, $"/api/v2/performance-appraisals/{seed.AppraisalId}/planning", planning, initialEtag);
        savedPlanning.StatusCode.Should().Be(HttpStatusCode.OK);
        var targetId = (await Detail(savedPlanning)).Planning!.Targets.Single().Id!.Value;
        var planningEtag = Etag(savedPlanning);

        var staleSave = await Patch(employee, $"/api/v2/performance-appraisals/{seed.AppraisalId}/planning", planning, initialEtag);
        staleSave.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var planningKey = Guid.NewGuid().ToString("N");
        var submittedPlanning = await Post(employee, $"/api/v2/performance-appraisals/{seed.AppraisalId}/submit-planning",
            new AppraisalAttestationRequest(true), planningEtag, planningKey);
        submittedPlanning.StatusCode.Should().Be(HttpStatusCode.OK);
        var submittedPlanningEtag = Etag(submittedPlanning);
        var replayedPlanning = await Post(employee, $"/api/v2/performance-appraisals/{seed.AppraisalId}/submit-planning",
            new AppraisalAttestationRequest(true), planningEtag, planningKey);
        replayedPlanning.StatusCode.Should().Be(HttpStatusCode.OK);
        replayedPlanning.Headers.GetValues("Idempotent-Replayed").Should().ContainSingle("true");

        (await director.GetAsync($"/api/v2/performance-appraisals/{seed.AppraisalId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await hr.GetAsync($"/api/v2/performance-appraisals/{seed.AppraisalId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var confirmedPlanning = await Post(hod, $"/api/v2/performance-appraisals/{seed.AppraisalId}/confirm-planning",
            new AppraisalAttestationRequest(true), submittedPlanningEtag);
        confirmedPlanning.StatusCode.Should().Be(HttpStatusCode.OK);
        var midyearEtag = Etag(confirmedPlanning);

        var immutablePlanning = await Patch(employee, $"/api/v2/performance-appraisals/{seed.AppraisalId}/planning", planning, midyearEtag);
        immutablePlanning.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var midyear = new SaveAppraisalMidyearRequest(
            [new AppraisalTargetReview(targetId, "The manuscript is drafted and under internal review.")],
            [new AppraisalCompetencyReview("Scientific communication", "Presented findings at the institute seminar.")],
            "Advanced statistical analysis");
        var savedMidyear = await Patch(employee, $"/api/v2/performance-appraisals/{seed.AppraisalId}/midyear", midyear, midyearEtag);
        savedMidyear.StatusCode.Should().Be(HttpStatusCode.OK);
        var submittedMidyear = await Post(employee, $"/api/v2/performance-appraisals/{seed.AppraisalId}/submit-midyear",
            new AppraisalAttestationRequest(true), Etag(savedMidyear));
        submittedMidyear.StatusCode.Should().Be(HttpStatusCode.OK);

        var hodReview = new SaveHodMidyearReviewRequest(
            [new AppraisalTargetRemark(targetId, "Progress is on schedule.")],
            [new AppraisalCompetencyRemark("Scientific communication", "Communication has improved.")],
            "Support the requested statistics training.",
            [],
            null);
        var savedHodReview = await Patch(hod, $"/api/v2/performance-appraisals/{seed.AppraisalId}/hod-midyear-review",
            hodReview, Etag(submittedMidyear));
        savedHodReview.StatusCode.Should().Be(HttpStatusCode.OK);
        var submittedHodReview = await Post(hod, $"/api/v2/performance-appraisals/{seed.AppraisalId}/submit-hod-midyear-review",
            new AppraisalHodSubmissionRequest(null), Etag(savedHodReview));
        submittedHodReview.StatusCode.Should().Be(HttpStatusCode.OK);

        const string confidentialMidyearReason = "The training recommendation does not reflect our discussion.";
        var declinedMidyear = await Post(employee, $"/api/v2/performance-appraisals/{seed.AppraisalId}/midyear-signature",
            new AppraisalStaffSignatureRequest(false, null, confidentialMidyearReason), Etag(submittedHodReview));
        declinedMidyear.StatusCode.Should().Be(HttpStatusCode.OK);
        (await director.GetAsync($"/api/v2/performance-appraisals/{seed.AppraisalId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var missingHodResponse = await Post(hod, $"/api/v2/performance-appraisals/{seed.AppraisalId}/submit-hod-midyear-review",
            new AppraisalHodSubmissionRequest(null), Etag(declinedMidyear));
        missingHodResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var resubmittedHodReview = await Post(hod, $"/api/v2/performance-appraisals/{seed.AppraisalId}/submit-hod-midyear-review",
            new AppraisalHodSubmissionRequest("The training recommendation was clarified after review."), Etag(declinedMidyear));
        resubmittedHodReview.StatusCode.Should().Be(HttpStatusCode.OK);
        var acceptedMidyear = await Post(employee, $"/api/v2/performance-appraisals/{seed.AppraisalId}/midyear-signature",
            new AppraisalStaffSignatureRequest(true, null, null), Etag(resubmittedHodReview));
        acceptedMidyear.StatusCode.Should().Be(HttpStatusCode.OK);

        var approvedMidyear = await Post(director, $"/api/v2/performance-appraisals/{seed.AppraisalId}/midyear-director-approve",
            new AppraisalMidyearDirectorApprovalRequest("Progress is satisfactory and the agreed support should continue."), Etag(acceptedMidyear));
        approvedMidyear.StatusCode.Should().Be(HttpStatusCode.OK);

        var yearEnd = new SaveAppraisalYearEndRequest(
            [new AppraisalTargetResult(targetId, "The paper was accepted for publication.", 100, "Completed within the agreed resources and timeline.")]);
        var savedYearEnd = await Patch(employee, $"/api/v2/performance-appraisals/{seed.AppraisalId}/year-end", yearEnd, Etag(approvedMidyear));
        savedYearEnd.StatusCode.Should().Be(HttpStatusCode.OK);
        var submittedYearEnd = await Post(employee, $"/api/v2/performance-appraisals/{seed.AppraisalId}/submit-year-end",
            new AppraisalAttestationRequest(true), Etag(savedYearEnd));
        submittedYearEnd.StatusCode.Should().Be(HttpStatusCode.OK);

        var ratings = AppraisalFactors.Behavioral.Concat(AppraisalFactors.Core)
            .Select(factor => new AppraisalCompetencyRating(factor.Code, 5)).ToList();
        var assessment = new SaveHodAppraisalAssessmentRequest(
            [new AppraisalTargetAssessment(targetId, 5, "The agreed target was fully achieved.")],
            ratings,
            "The appraisee delivered excellent work throughout the cycle.");
        var savedAssessment = await Patch(hod, $"/api/v2/performance-appraisals/{seed.AppraisalId}/hod-assessment",
            assessment, Etag(submittedYearEnd));
        var savedAssessmentBody = await savedAssessment.Content.ReadAsStringAsync();
        savedAssessment.StatusCode.Should().Be(HttpStatusCode.OK, "because the API returned: {0}", savedAssessmentBody);
        var assessmentDetail = await Detail(savedAssessment);
        assessmentDetail.Scores.BehavioralScore.Should().Be(50m);
        assessmentDetail.Scores.CoreScore.Should().Be(50m);
        assessmentDetail.Scores.TotalPercentage.Should().Be(100m);

        var submittedAssessment = await Post(hod, $"/api/v2/performance-appraisals/{seed.AppraisalId}/submit-hod-assessment",
            new AppraisalHodSubmissionRequest(null), Etag(savedAssessment));
        submittedAssessment.StatusCode.Should().Be(HttpStatusCode.OK);
        const string confidentialYearEndReason = "The assessment omits evidence supplied to my supervisor.";
        var declinedYearEnd = await Post(employee, $"/api/v2/performance-appraisals/{seed.AppraisalId}/staff-signature",
            new AppraisalStaffSignatureRequest(false, null, confidentialYearEndReason), Etag(submittedAssessment));
        declinedYearEnd.StatusCode.Should().Be(HttpStatusCode.OK);

        var roster = await hr.GetAsync($"/api/v2/appraisal-cycles/{seed.CycleId}/roster");
        roster.StatusCode.Should().Be(HttpStatusCode.OK);
        var rosterText = await roster.Content.ReadAsStringAsync();
        rosterText.Should().Contain("\"hasSignatureDisagreement\":true");
        rosterText.Should().NotContain(confidentialMidyearReason).And.NotContain(confidentialYearEndReason);
        (await hr.GetAsync($"/api/v2/performance-appraisals/{seed.AppraisalId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await director.GetAsync($"/api/v2/performance-appraisals/{seed.AppraisalId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var missingYearEndResponse = await Post(hod, $"/api/v2/performance-appraisals/{seed.AppraisalId}/submit-hod-assessment",
            new AppraisalHodSubmissionRequest(null), Etag(declinedYearEnd));
        missingYearEndResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var resubmittedAssessment = await Post(hod, $"/api/v2/performance-appraisals/{seed.AppraisalId}/submit-hod-assessment",
            new AppraisalHodSubmissionRequest("All supplied evidence was checked and the assessment was retained."), Etag(declinedYearEnd));
        resubmittedAssessment.StatusCode.Should().Be(HttpStatusCode.OK);
        var acceptedYearEnd = await Post(employee, $"/api/v2/performance-appraisals/{seed.AppraisalId}/staff-signature",
            new AppraisalStaffSignatureRequest(true, "No comment", null), Etag(resubmittedAssessment));
        acceptedYearEnd.StatusCode.Should().Be(HttpStatusCode.OK);

        var approval = new AppraisalDirectorApprovalRequest(
            "The employee achieved the agreed work programme.",
            "Research Scientist II",
            "Recommended",
            "Advanced data analysis",
            null,
            null,
            null);
        var approved = await Post(director, $"/api/v2/performance-appraisals/{seed.AppraisalId}/director-approve",
            approval, Etag(acceptedYearEnd));
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        var approvedDetail = await Detail(approved);
        approvedDetail.Summary.Status.Should().Be(AppraisalStatuses.Approved);
        approvedDetail.Summary.FinalDocumentAvailable.Should().BeTrue();
        approvedDetail.Summary.AvailableActions.Should().ContainSingle("download-final-document");

        var document = await employee.GetAsync($"/api/v2/performance-appraisals/{seed.AppraisalId}/document");
        document.StatusCode.Should().Be(HttpStatusCode.OK);
        document.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var documentBytes = await document.Content.ReadAsByteArrayAsync();
        var documentSource = Encoding.ASCII.GetString(documentBytes);
        var documentText = ExtractPdfText(documentBytes);
        documentBytes.Should().StartWith(Encoding.ASCII.GetBytes("%PDF-1.4"));
        documentSource.Should().Contain("/Subtype /Image")
            .And.Contain("/Width 602 /Height 602")
            .And.Contain($"source-template-sha256:{AppraisalFormTemplate.CanonicalContentChecksum}");
        Regex.Matches(documentSource, @"/Type /Page\b").Count.Should().Be(AppraisalPdf.PhysicalPageCount);
        Regex.Matches(documentSource, @"/MediaBox \[0 0 612 792\]").Count.Should().Be(AppraisalPdf.PhysicalPageCount);
        Regex.Matches(documentSource, @"\bre (?:S|B)\b").Count.Should().BeGreaterThan(25);
        AssertOrdered(documentText,
            "COUNCIL FOR SCIENTIFIC AND",
            "PERFORMANCE APPRAISAL",
            "MANAGEMENT FORM",
            "STRICTLY CONFIDENTIAL",
            "CSIR PERFORMANCE MANAGEMENT",
            "(STAFF PERFORMANCE PLANNING, REVIEW AND APPRAISAL FORM)",
            "PART I",
            "SECTION A: APPRAISEE PERSONAL DATA",
            "SECTION B: APPRAISER (HEAD) INFORMATION",
            "PART II",
            "PERFORMANCE PLANNING STAGE",
            "PERFORMANCE /MID-YEAR PROGRESS REVIEW",
            "PART III",
            "END OF YEAR ASSESSMENT",
            "PART IV",
            "PERFORMANCE STANDARD",
            "PART V",
            "OVERALL ASSESSMENT: (REFER TO PART III)",
            "PART VI",
            "(APPENDIX)");
        foreach (var factor in AppraisalFactors.Behavioral.Concat(AppraisalFactors.Core))
            documentText.Should().Contain(factor.Label);
        foreach (var guidance in AppraisalFactors.BehavioralRatingGuidance.Concat(AppraisalFactors.CoreRatingGuidance))
            documentText.Should().Contain(guidance.Explanation);
        documentText.Should().Contain("total applicable score / total applicable values X (total number of values)")
            .And.Contain("70% & above")
            .And.Contain("Exceptional / Outstanding")
            .And.Contain("60-69%")
            .And.Contain("Competent / very able and effective")
            .And.Contain("50-59%")
            .And.Contain("Fair / Average")
            .And.Contain("40-49%")
            .And.Contain("Below Average")
            .And.Contain("0-39%")
            .And.Contain("Poor")
            .And.Contain("Signature of Supervisor or Head of Division/Unit")
            .And.Contain("Signature of Employee")
            .And.Contain("Signature of Director")
            .And.Contain("Publish one peer-reviewed paper")
            .And.Contain("The employee achieved the agreed work programme.")
            .And.Contain("Research Scientist II");
        documentText.Should().NotContain("SECTION C: FINAL APPROVER INFORMATION")
            .And.NotContain("Workflow history")
            .And.NotContain("Amendment history")
            .And.NotContain("Verification section")
            .And.NotContain("LANYOH")
            .And.NotContain("NOAH")
            .And.NotContain("Wisconsin Int' Univ. College");

        var locked = await Patch(employee, $"/api/v2/performance-appraisals/{seed.AppraisalId}/year-end", yearEnd, Etag(approved));
        locked.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var hrFinal = await hr.GetAsync($"/api/v2/performance-appraisals/{seed.AppraisalId}");
        hrFinal.StatusCode.Should().Be(HttpStatusCode.OK);
        var hrFinalText = await hrFinal.Content.ReadAsStringAsync();
        hrFinalText.Should().NotContain(confidentialMidyearReason).And.NotContain(confidentialYearEndReason);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var auditActions = await db.AuditRecords.AsNoTracking()
            .Where(record => record.TargetType == "PerformanceAppraisal" && record.TargetId == seed.AppraisalId.ToString())
            .Select(record => record.Action)
            .ToListAsync();
        auditActions.Should().Contain("appraisal.midyear-signature-declined")
            .And.Contain("appraisal.year-end-signature-declined")
            .And.Contain("appraisal.final-approved");
        (await db.AppraisalHodSubmissions.CountAsync(item => item.PerformanceAppraisalId == seed.AppraisalId && item.Phase == AppraisalPhases.Midyear))
            .Should().Be(2);
        (await db.AppraisalHodSubmissions.CountAsync(item => item.PerformanceAppraisalId == seed.AppraisalId && item.Phase == AppraisalPhases.YearEnd))
            .Should().Be(2);
        var appraisalMessages = await db.CommunicationOutboxMessages.AsNoTracking()
            .Where(message => message.Category.StartsWith("appraisal-"))
            .ToListAsync();
        appraisalMessages.Select(message => message.Channel).Should().Contain(["event", "email", "sms"]);
        appraisalMessages.Select(message => message.Body).Should().OnlyContain(body =>
            !body.Contains(confidentialMidyearReason, StringComparison.Ordinal) &&
            !body.Contains(confidentialYearEndReason, StringComparison.Ordinal) &&
            !body.Contains("Exceptional/Outstanding", StringComparison.Ordinal));
        var notices = await db.Notifications.AsNoTracking()
            .Where(notification => notification.ActionLink == $"/appraisals/{seed.AppraisalId:D}")
            .ToListAsync();
        notices.Should().NotBeEmpty();
        notices.Should().OnlyContain(notification =>
            !notification.Body.Contains(confidentialMidyearReason, StringComparison.Ordinal) &&
            !notification.Body.Contains(confidentialYearEndReason, StringComparison.Ordinal));
    }

    private static string ExtractPdfText(byte[] document)
    {
        var source = Encoding.ASCII.GetString(document);
        var lines = Regex.Matches(source, @"\((?<text>(?:\\[()\\]|[^()])*)\)\s+(?:Tj|')").Select(match =>
            match.Groups["text"].Value
                .Replace("\\(", "(", StringComparison.Ordinal)
                .Replace("\\)", ")", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal));
        return string.Join(' ', lines);
    }

    private static void AssertOrdered(string content, params string[] markers)
    {
        var previous = -1;
        foreach (var marker in markers)
        {
            var current = content.IndexOf(marker, StringComparison.Ordinal);
            current.Should().BeGreaterThan(previous, $"because '{marker}' must retain its official position");
            previous = current;
        }
    }

    [Fact]
    public async Task Appraisal_Access_Requires_Permissions_Ownership_Assignment_And_Institute_Scope()
    {
        var seed = await SeedAsync();
        using var anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/api/v2/performance-appraisals/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var employeeWithoutPermission = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId, seed.EmployeeId);
        (await employeeWithoutPermission.GetAsync("/api/v2/performance-appraisals/me")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await employeeWithoutPermission.GetAsync($"/api/v2/performance-appraisals/{seed.AppraisalId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var otherEmployee = Client(Guid.NewGuid(), SpmeRoles.Employee, seed.InstituteId, Guid.NewGuid(),
            SpmePermissions.AppraisalsSelf);
        (await otherEmployee.GetAsync($"/api/v2/performance-appraisals/{seed.AppraisalId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var unassignedReviewer = Client(Guid.NewGuid(), SpmeRoles.HeadOfSection, seed.InstituteId, Guid.NewGuid(),
            SpmePermissions.AppraisalsReview);
        (await unassignedReviewer.GetAsync($"/api/v2/performance-appraisals/{seed.AppraisalId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var crossInstitute = Client(seed.EmployeeUserId, SpmeRoles.Employee, Guid.NewGuid(), seed.EmployeeId,
            SpmePermissions.AppraisalsSelf);
        (await crossInstitute.GetAsync($"/api/v2/performance-appraisals/{seed.AppraisalId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Seed> SeedAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var institute = new Institute($"AP-{suffix}", $"Appraisal institute {suffix}", "Institute");
        var employee = Employee(institute.Id, $"AP-E-{suffix}", "Appraisee", "Ama", $"appraisee.{suffix}@example.test", "+233241234567");
        var hodEmployee = Employee(institute.Id, $"AP-H-{suffix}", "Reviewer", "Kofi", $"reviewer.{suffix}@example.test", "+233241234568");
        var directorEmployee = Employee(institute.Id, $"AP-D-{suffix}", "Director", "Esi", $"director.{suffix}@example.test", "+233241234569");
        var employeeUser = User(employee, "appraisee");
        var hodUser = User(hodEmployee, "reviewer");
        var directorUser = User(directorEmployee, "director");
        var hrUser = new User($"hr.{suffix}@example.test", "HrAdmin") { Email = $"hr.{suffix}@example.test" };
        hrUser.AssignInstitute(institute.Id, "HrAdmin");
        hrUser.UpdateDisplayName("HR Administrator");

        var cycle = AppraisalCycle.Create(
            institute.Id,
            "2026 annual appraisal",
            2026,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31),
            new DateTime(2026, 1, 1),
            new DateTime(2026, 3, 31),
            new DateTime(2026, 4, 1),
            new DateTime(2026, 8, 31),
            new DateTime(2026, 9, 1),
            new DateTime(2026, 12, 31)).Value!;
        cycle.Open();
        cycle.Close();
        cycle.Reopen("Integration test exercises each official stage.");
        var appraisal = PerformanceAppraisal.Assign(
            institute.Id,
            employee.Id,
            cycle,
            hodUser.Id,
            directorUser.Id,
            new AppraisalEmployeeSnapshot("Dr", employee.Surname, "Ama", null, "Research Scientist", "SG 12/2",
                new DateTime(2024, 1, 1), institute.Name, "Research Division", new DateTime(2020, 1, 1)),
            new AppraisalAppraiserSnapshot("Dr", hodEmployee.Surname, "Kofi", null, "Head of Section"),
            new AppraisalAppraiserSnapshot("Dr", directorEmployee.Surname, "Esi", null, "Institute Director"),
            null);

        db.Institutes.Add(institute);
        db.Employees.AddRange(employee, hodEmployee, directorEmployee);
        db.Users.AddRange(employeeUser, hodUser, directorUser, hrUser);
        db.AppraisalCycles.Add(cycle);
        db.PerformanceAppraisals.Add(appraisal);
        await db.SaveChangesAsync();
        return new Seed(institute.Id, cycle.Id, appraisal.Id, employee.Id, employeeUser.Id,
            hodEmployee.Id, hodUser.Id, directorEmployee.Id, directorUser.Id, hrUser.Id);
    }

    private static Employee Employee(Guid instituteId, string staffId, string surname, string firstName, string email, string phone)
    {
        var employee = new Employee(instituteId, staffId, surname, "unspecified");
        employee.UpdateImportedProfile("Dr", firstName, null, "Ghanaian", null, null, email, phone, true);
        return employee;
    }

    private static User User(Employee employee, string label)
    {
        var user = new User($"{label}.{Guid.NewGuid():N}@example.test", "Employee")
        {
            Email = employee.PrimaryEmail,
            PhoneNumber = employee.Phone,
            EmailConfirmed = true,
            PhoneNumberConfirmed = true
        };
        user.LinkEmployee(employee.Id, employee.InstituteId);
        user.UpdateDisplayName($"{employee.OtherNames} {employee.Surname}");
        return user;
    }

    private HttpClient Client(Guid userId, string role, Guid instituteId, Guid? employeeId, params string[] permissions)
    {
        var jwt = factory.Services.GetRequiredService<IConfiguration>().GetSection("Jwt");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role),
            new("identity_type", role),
            new("institute_id", instituteId.ToString())
        };
        if (employeeId.HasValue) claims.Add(new Claim("employee_id", employeeId.Value.ToString()));
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var token = new JwtSecurityToken(
            jwt.GetValue<string>("Issuer") ?? "csir-spme-api",
            jwt.GetValue<string>("Audience") ?? "csir-spme-client",
            claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.GetValue<string>("Key")!)),
                SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static async Task<HttpResponseMessage> Patch(HttpClient client, string path, object body, string etag)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, path) { Content = JsonContent.Create(body) };
        request.Headers.IfMatch.ParseAdd(etag);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> Post(
        HttpClient client, string path, object body, string etag, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.IfMatch.ParseAdd(etag);
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }

    private static string Etag(HttpResponseMessage response) =>
        response.Headers.ETag?.Tag ?? throw new InvalidOperationException("The appraisal response did not include an ETag.");

    private static async Task<PerformanceAppraisalResponse> Detail(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<PerformanceAppraisalResponse>()
        ?? throw new InvalidOperationException("The appraisal response body was empty.");

    private sealed record Seed(
        Guid InstituteId,
        Guid CycleId,
        Guid AppraisalId,
        Guid EmployeeId,
        Guid EmployeeUserId,
        Guid HodEmployeeId,
        Guid HodUserId,
        Guid DirectorEmployeeId,
        Guid DirectorUserId,
        Guid HrUserId);
}
