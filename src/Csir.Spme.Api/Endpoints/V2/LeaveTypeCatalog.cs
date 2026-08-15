using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class LeaveTypeCatalog
{
    private const string DocumentTitle = "REVISED CONDITIONS OF SERVICE, SNR STAFF -- FINAL DRAFT.pdf";
    private const string ChapterV = "Chapter V";
    private static readonly HashSet<string> SecureDocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        LeaveTypes.Compassionate,
        LeaveTypes.Examination,
        LeaveTypes.Maternity,
        LeaveTypes.LeaveOfAbsence,
        LeaveTypes.Sick,
        "study",
        "resettlement"
    };

    private static readonly IReadOnlyList<LeaveTypeMetadataResponse> Items =
    [
        new(
            LeaveTypes.Annual,
            "Annual Leave",
            "Annual earned leave for senior staff, taken within the calendar leave year unless formally deferred.",
            "earned",
            "working-days",
            new LeaveEntitlementResponse(
                null,
                42m,
                "working-days",
                "Leave year runs from January to December.",
                null,
                "Exceptional written deferral may allow up to two years of leave entitlement.",
                "Excludes Saturdays, Sundays, and public holidays."),
            new LeaveDeductionResponse(true, false, false, "This is the employee's earned annual leave balance."),
            EligibilityAll(null, "Senior staff officer covered by the Conditions of Service.", []),
            new LeaveRequestWindowResponse(
                AdvanceNotice(null, "not-specified", "not-specified", "The policy does not prescribe a minimum number of days for an annual leave application; a completed leave application and the Director's written permission are required before proceeding.", null),
                null,
                "Must normally be taken within the calendar year.",
                true,
                ["Leave dates are determined by the Director of the Institute.", "Appointees after 30 June have proportionate leave credited to the following year's leave."]),
            ["Approved leave application form", "Handover notes before proceeding on leave"],
            "Director's written permission is required before proceeding on leave.",
            Source("Section 34 and Section 35", ["34(1)", "34(6)", "34(9)", "35(1)", "35(2)", "35(3)"]),
            true,
            "active"),

        new(
            LeaveTypes.Part,
            "Casual Leave",
            "Short leave for urgent personal matters, treated as part of annual leave.",
            "earned",
            "working-days",
            new LeaveEntitlementResponse(
                null,
                5m,
                "working-days",
                "Maximum of five working days in a year after annual leave is exhausted.",
                null,
                "Exceptional additional days count against future annual leave.",
                "Also known in the policy as part of leave for urgent personal matters."),
            new LeaveDeductionResponse(true, true, false, "Deducted from earned leave; exceptional excess is debited against future annual leave."),
            EligibilityAll(null, "Senior staff officer with earned or future annual leave entitlement.", ["Urgent personal matter required."]),
            new LeaveRequestWindowResponse(
                AdvanceNotice(null, "not-specified", "not-specified", "Written permission must be sought and granted before absence; the policy does not prescribe a minimum number of days.", "In an emergency, verbal permission may be documented in writing as soon as the officer returns to work."),
                "Before absence, except emergencies may be regularized in writing on return.",
                null,
                true,
                ["Permission should be sought and granted in writing.", "Verbal emergency permission must be documented after return."]),
            ["Written permission or written confirmation after emergency verbal permission"],
            "Director may grant permission in writing.",
            Source("Section 34 and Section 36", ["34(2)", "36(1)", "36(2)", "36(3)"]),
            true,
            "active"),

        new(
            LeaveTypes.Compassionate,
            "Compassionate Leave",
            "Leave granted in exceptional circumstances to an officer in distress.",
            "event",
            "working-days",
            new LeaveEntitlementResponse(
                null,
                5m,
                "working-days",
                null,
                null,
                null,
                "Available only after annual and casual leave entitlements are exhausted."),
            new LeaveDeductionResponse(false, false, false, "Policy requires annual and casual leave to be exhausted before compassionate leave is granted."),
            EligibilityAll(null, "Senior staff officer in distress.", ["Exceptional circumstances required.", "Annual leave and casual leave must already be exhausted."]),
            new LeaveRequestWindowResponse(
                AdvanceNotice(null, "not-specified", "event-driven", "No minimum advance-notice period is specified; the Director may grant leave when the exceptional distress circumstance arises.", null),
                null,
                null,
                false,
                ["Requested when the distress circumstance arises."]),
            ["Employee distress explanation or supporting evidence where required by HR"],
            "Director may grant up to five days.",
            Source("Section 37", ["37"]),
            true,
            "active"),

        new(
            LeaveTypes.Examination,
            "Examination Leave",
            "Leave of absence to sit for approved examinations.",
            "event",
            "not-specified",
            new LeaveEntitlementResponse(null, null, "not-specified", null, null, null, "Duration is not specified in the source clause."),
            new LeaveDeductionResponse(false, false, false, "The source clause does not state that examination leave deducts from annual leave."),
            EligibilityAll(null, "Senior staff officer sitting approved examinations.", ["Examination must be approved."]),
            new LeaveRequestWindowResponse(
                AdvanceNotice(null, "not-specified", "not-specified", "An application is required for the Director's consideration, but the policy does not prescribe a minimum number of days before the examination.", null),
                "On application before the approved examination.",
                null,
                false,
                ["Request timing should align with the examination schedule."]),
            ["Approved examination evidence"],
            "Director may grant leave on application.",
            Source("Section 38", ["38"]),
            true,
            "active"),

        new(
            LeaveTypes.Maternity,
            "Maternity Leave",
            "Full-pay maternity leave for a pregnant officer, with limited pre-confinement leave supported by medical certification.",
            "medical",
            "months",
            new LeaveEntitlementResponse(
                null,
                3m,
                "months",
                null,
                null,
                null,
                "Not more than six weeks may be taken before confinement."),
            new LeaveDeductionResponse(false, false, false, "Maternity leave is additional to annual leave entitlement or earned leave."),
            new LeaveEligibilityResponse(["female"], null, "Pregnant senior staff officer.", ["Pregnancy and expected confinement must be certified for pre-confinement leave."]),
            new LeaveRequestWindowResponse(
                AdvanceNotice(null, "not-specified", "not-specified", "The policy does not prescribe a minimum application lead time; medical certification is required when pre-confinement leave is expected within six weeks.", null),
                "Not more than six weeks before expected confinement.",
                null,
                false,
                ["Nursing mothers may be absent for two hours each working day for up to nine months after delivery."]),
            ["Certificate from a recognized medical officer when confinement is expected within six weeks"],
            "Pregnant officer has a right to maternity leave when policy conditions are met.",
            Source("Section 39", ["39(1)", "39(2)", "39(3)", "39(4)"]),
            true,
            "active"),

        new(
            LeaveTypes.LeaveOfAbsence,
            "Leave Without Pay",
            "Unpaid leave of absence for eligible officers, subject to service, notice, approval, resumption, salary, promotion, accommodation, pension, and loan conditions.",
            "unpaid",
            "years",
            new LeaveEntitlementResponse(
                null,
                2m,
                "years",
                null,
                "Renewable for up to another one year after the first one-year grant.",
                "Must not exceed two years; absence beyond two years requires resignation.",
                "Director may approve up to one year; Director-General approves beyond one year up to two years."),
            new LeaveDeductionResponse(false, false, true, "Leave without pay is unpaid and may affect increments, promotion eligibility, accommodation rent, pension premiums, and loans."),
            EligibilityAll(
                24,
                "Senior staff officer with at least two years continuous service, unless emergency waiver applies.",
                ["Emergency waiver may apply for illness of spouse or child.", "Officer must serve twice the duration on return before further leave without pay.", "Study-leave bond must be completed.", "Council loans must be settled or arranged."]),
            new LeaveRequestWindowResponse(
                AdvanceNotice(3m, "months", "specified", "The approved application form must be submitted to the Director of Institute or Director of Administration at least three clear months before the proposed commencement date.", "The policy permits an emergency waiver of the minimum-service eligibility rule but does not state whether it changes this notice period."),
                "At least three clear months before the proposed commencement date.",
                "Three months notice is required before resignation while on leave without pay.",
                false,
                ["Failure to resume within fourteen continuous days after expiry without reasonable explanation is treated as vacation of post."]),
            ["Approved application form", "Comprehensive handover notes", "Loan settlement or suitable repayment arrangement where applicable"],
            "Director of Institute or Director of Administration approves up to one year; Director-General approves beyond one year up to two years.",
            Source("Section 40", ["40(1)", "40(2)", "40(3)", "40(4)", "40(5)", "40(6)", "40(7)", "40(8)", "40(9)", "40(10)"]),
            true,
            "active"),

        new(
            "study",
            "Study Leave",
            "Study leave with full or partial sponsorship under Council-administered training regulations.",
            "study",
            "mixed",
            new LeaveEntitlementResponse(
                null,
                5m,
                "years",
                null,
                "M.Sc/M.Phil and PhD paths may receive a one-year extension when supported by a favorable supervisor recommendation.",
                "Programme extensions, course changes, content changes, or termination require prior HRD Committee approval.",
                "Tenure: two years for M.Sc/M.Phil, three years for full-time PhD, and up to five years for part-time PhD."),
            new LeaveDeductionResponse(false, false, false, "Accumulated leave is forfeited when an officer goes on study leave; sponsorship category determines salary and fee treatment."),
            EligibilityAll(24, "Senior staff officer who is medically fit and fully satisfies admission requirements.", ["Training must be at a recognized institution or establishment.", "Training should normally be local and within CSIR priority areas."]),
            new LeaveRequestWindowResponse(
                AdvanceNotice(null, "not-specified", "not-specified", "The policy does not prescribe a minimum application lead time; applications must satisfy the Human Resource Development Committee's fellowship requirements.", null),
                null,
                null,
                false,
                ["Progress reports are required half-yearly for courses exceeding six months.", "Completion report is required for courses up to six months."]),
            ["Admission evidence", "Medical fitness evidence", "Bond documentation where applicable", "Progress reports", "Completion report"],
            "Human Resource Development Committee considers and approves study leave applications.",
            Source("Section 34 and Section 41", ["34(8)", "41(1)", "41(2)", "41(3)", "41(4)", "41(5)", "41(6)", "41(10)"]),
            false,
            "requires-hr-approval"),

        new(
            "resettlement",
            "Resettlement Leave",
            "Consecutive leave after returning from study abroad of at least one year.",
            "resettlement",
            "calendar-days",
            new LeaveEntitlementResponse(
                14m,
                14m,
                "calendar-days",
                null,
                null,
                null,
                "Starts from the day following disembarkation."),
            new LeaveDeductionResponse(false, false, false, "The source clause does not state that resettlement leave deducts from annual leave."),
            EligibilityAll(null, "Officer returning from abroad after a course of study of not less than one year.", ["Officer should report for duty before taking resettlement leave."]),
            new LeaveRequestWindowResponse(
                AdvanceNotice(null, "calendar-days", "event-driven", "No advance application period is specified; the officer should first report for duty, and the leave starts on the day after disembarkation.", null),
                "After reporting for duty following return from study abroad.",
                "Starts the day after disembarkation.",
                false,
                ["Available only after qualifying study abroad."]),
            ["Evidence of return from qualifying study abroad"],
            "Granted on arrival after qualifying study abroad, after the officer reports for duty.",
            Source("Section 42", ["42"]),
            false,
            "requires-hr-approval"),

        new(
            LeaveTypes.Sick,
            "Sick Leave",
            "Medical absence due to ill-health, supported by medical reports and salary-stage limits.",
            "medical",
            "months",
            new LeaveEntitlementResponse(
                null,
                12m,
                "months",
                null,
                null,
                "Further dispensation after the second six-month period is without salary unless otherwise approved.",
                "Up to six months on full pay, followed by up to six months on half salary."),
            new LeaveDeductionResponse(false, false, true, "Sick leave affects salary after the first six months and may become unpaid after twelve months."),
            EligibilityAll(null, "Officer absent from duty on account of ill-health.", ["Medical Officer report is required after fourteen continuous days of illness.", "Monthly medical reports are required during continued absence."]),
            new LeaveRequestWindowResponse(
                AdvanceNotice(null, "not-specified", "event-driven", "No advance application period applies because illness triggers the absence; a Medical Officer report must be furnished after fourteen continuous days of absence.", null),
                "When illness causes absence from duty.",
                "Medical report required after fourteen continuous days.",
                false,
                ["Failure to resume contrary to medical advice may lead to vacation of post after fourteen working days."]),
            ["Medical Officer report", "Monthly Medical Officer reports during continued absence"],
            "Director may allow certified sick leave under the ill-health procedure.",
            new LeaveSourceClauseResponse(DocumentTitle, "Chapter VI", "Section 45", ["45(a)", "45(b)", "45(c)", "45(d)", "45(g)"]),
            true,
            "active"),

        new(
            LeaveTypes.Paternity,
            "Paternity Leave",
            "Paternity leave placeholder retained for API compatibility pending an approved policy source.",
            "policy-pending",
            "not-specified",
            new LeaveEntitlementResponse(null, null, "not-specified", null, null, null, "No paternity entitlement is defined in the source PDF."),
            new LeaveDeductionResponse(false, false, false, "Deduction cannot be determined until an approved paternity policy source exists."),
            new LeaveEligibilityResponse(["male"], null, "Not requestable until an approved policy source defines eligibility.", ["Policy source pending."]),
            new LeaveRequestWindowResponse(
                AdvanceNotice(null, "not-specified", "policy-source-pending", "No advance-notice requirement can be stated until an approved paternity policy source exists.", null),
                null,
                null,
                false,
                ["No request window is defined until policy approval."]),
            [],
            "No approval path is enabled until policy confirmation.",
            new LeaveSourceClauseResponse(DocumentTitle, "Policy source pending", "Not defined in supplied PDF", []),
            false,
            "policy-source-pending")
    ];

    public static IReadOnlyList<LeaveTypeMetadataResponse> List(
        bool? requestable,
        string? gender,
        string? category,
        string? policyStatus)
    {
        IEnumerable<LeaveTypeMetadataResponse> query = Items.Select(WithSecureDocumentAvailability);
        if (requestable.HasValue)
        {
            query = query.Where(item => item.IsRequestable == requestable.Value);
        }

        if (!string.IsNullOrWhiteSpace(gender))
        {
            var normalizedGender = gender.Trim().ToLowerInvariant();
            query = query.Where(item =>
                item.Eligibility.AllowedGenders.Contains("all", StringComparer.OrdinalIgnoreCase) ||
                item.Eligibility.AllowedGenders.Contains(normalizedGender, StringComparer.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(item => string.Equals(item.Category, category.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(policyStatus))
        {
            query = query.Where(item => string.Equals(item.PolicyStatus, policyStatus.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderBy(item => item.Name).ToList();
    }

    public static LeaveTypeMetadataResponse? Find(string codeOrAlias)
    {
        var normalized = codeOrAlias.Trim().ToLowerInvariant();
        var item = Items.FirstOrDefault(item => item.Code == normalized) ??
            normalized switch
            {
                "casual" => Items.First(item => item.Code == LeaveTypes.Part),
                "leave-without-pay" => Items.First(item => item.Code == LeaveTypes.LeaveOfAbsence),
                _ => null
            };
        return item is null ? null : WithSecureDocumentAvailability(item);
    }

    private static LeaveTypeMetadataResponse WithSecureDocumentAvailability(LeaveTypeMetadataResponse item) =>
        !SecureDocumentTypes.Contains(item.Code) ? item : item with
        {
            IsRequestable = false,
            PolicyStatus = "secure-documents-unavailable"
        };

    private static LeaveEligibilityResponse EligibilityAll(
        int? minimumServiceMonths,
        string? employmentCategoryNotes,
        IReadOnlyList<string> specialConstraints) =>
        new(["all"], minimumServiceMonths, employmentCategoryNotes, specialConstraints);

    private static LeaveSourceClauseResponse Source(string section, IReadOnlyList<string> clauses) =>
        new(DocumentTitle, ChapterV, section, clauses);

    private static LeaveAdvanceNoticeResponse AdvanceNotice(
        decimal? minimumDuration,
        string unit,
        string status,
        string requirement,
        string? exception) =>
        new(minimumDuration, unit, status, requirement, exception);
}
