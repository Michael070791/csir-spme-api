using Csir.Spme.Domain.Hr;

namespace Csir.Spme.Infrastructure.Communications;

public static class AppraisalPdf
{
    public const int PhysicalPageCount = 15;

    private const float Left = 42f;
    private const float ContentWidth = 528f;

    public static byte[] Build(AppraisalPdfForm form)
    {
        var document = new AppraisalPdfDocument(LoadOfficialLogo());
        AddCover(document.AddPage());
        AddPartOne(document.AddPage(), form);
        AddPlanningFirstPage(document.AddPage(), form);
        AddPlanningSecondPage(document.AddPage(), form);
        AddMidyearFirstPage(document.AddPage(), form);
        AddMidyearSecondPage(document.AddPage(), form);
        AddMidyearThirdPage(document.AddPage(), form);
        AddYearEndFirstPage(document.AddPage(), form);
        AddYearEndSecondPage(document.AddPage(), form);
        AddPartFourFirstPage(document.AddPage(), form);
        AddPartFourSecondPage(document.AddPage(), form);
        AddPartFourThirdPage(document.AddPage(), form);
        AddPartFive(document.AddPage(), form);
        AddPartSixFirstPage(document.AddPage());
        AddPartSixSecondPage(document.AddPage());
        return document.Build(AppraisalFormTemplate.CanonicalContentChecksum);
    }

    private static void AddCover(AppraisalPdfPage page)
    {
        page.Rectangle(new PdfRectangle(33f, 16f, 546f, 760f), 0.8f);
        Center(page, "COUNCIL FOR SCIENTIFIC AND", 49f, 13f, true, PdfColor.Navy);
        Center(page, "INDUSTRIAL RESEARCH", 69f, 13f, true, PdfColor.Navy);
        Center(page, "(CSIR)", 91f, 12f, true, PdfColor.Pink);
        page.Image(224f, 238f, 164f, 164f);
        Center(page, "CSIR", 528f, 14f, true, PdfColor.Navy);
        Center(page, "PERFORMANCE APPRAISAL", 553f, 16f, true, PdfColor.Navy);
        Center(page, "MANAGEMENT FORM", 581f, 16f, true, PdfColor.Navy);
    }

    private static void AddPartOne(AppraisalPdfPage page, AppraisalPdfForm form)
    {
        page.Text("STRICTLY CONFIDENTIAL", Left, 14f, 7f, true, alignment: PdfTextAlignment.Right,
            availableWidth: ContentWidth);
        Center(page, "CSIR PERFORMANCE MANAGEMENT", 31f, 9f, true, PdfColor.Navy);
        Center(page, "(STAFF PERFORMANCE PLANNING, REVIEW AND APPRAISAL FORM)", 49f, 8.5f, true, PdfColor.Navy);
        LabelValue(page, "APPRAISAL PERIOD: From:", Date(form.PeriodStart), Left, 79f, 250f);
        LabelValue(page, "To:", Date(form.PeriodEnd), 362f, 79f, 208f);
        page.Text("(Indicate the period of Appraisal)", Left, 94f, 6.8f, italic: true, color: PdfColor.Muted);
        page.Text("PART I", Left, 116f, 9f, true, color: PdfColor.Pink);
        page.Text("SECTION A: APPRAISEE PERSONAL DATA", Left, 139f, 8f, true, color: PdfColor.Navy);

        LabelValue(page, "Title:", TitleSelection(form.Employee.Title), Left, 162f, ContentWidth);
        LabelValue(page, "Surname:", form.Employee.Surname, Left, 188f, 260f);
        LabelValue(page, "First Name:", form.Employee.FirstName, 322f, 188f, 248f);
        LabelValue(page, "Other Name(s):", form.Employee.OtherNames, Left, 214f, ContentWidth);
        LabelValue(page, "Present Grade:", form.Employee.PresentGrade, Left, 240f, 290f);
        LabelValue(page, "Salary Grade/Step:", form.Employee.SalaryGradeStep, 354f, 240f, 216f);
        LabelValue(page, "Date of Present Grade:", Date(form.Employee.DateOfPresentGrade), Left, 266f, ContentWidth);
        LabelValue(page, "Institute:", form.Employee.Institute, Left, 292f, ContentWidth);
        LabelValue(page, "Division/Unit:", form.Employee.DivisionUnit, Left, 318f, ContentWidth);
        LabelValue(page, "Date of first Appointment:", Date(form.Employee.DateOfFirstAppointment), Left, 344f, ContentWidth);

        page.Text("Training received during the year under review", Left, 374f, 7.5f, true);
        var trainingRows = form.TrainingReceived.Count == 0
            ? new[] { new[] { string.Empty, string.Empty, string.Empty } }
            : form.TrainingReceived.Select(training => new[]
            {
                training.Institution,
                Date(training.Date),
                training.Programme
            }).ToArray();
        DrawTable(page, Left, 391f, ContentWidth, [0.42f, 0.18f, 0.40f],
            ["Institution", "Date", "Programme"], trainingRows, 22f, 74f, 6.5f, 6.5f);

        page.Text("SECTION B: APPRAISER (HEAD) INFORMATION", Left, 504f, 8f, true, color: PdfColor.Navy);
        LabelValue(page, "Title:", form.Appraiser.Title, Left, 530f, ContentWidth);
        LabelValue(page, "Surname:", form.Appraiser.Surname, Left, 556f, 260f);
        LabelValue(page, "First Name:", form.Appraiser.FirstName, 322f, 556f, 248f);
        LabelValue(page, "Other Names:", form.Appraiser.OtherNames, Left, 582f, ContentWidth);
        LabelValue(page, "Position of Appraiser:", form.Appraiser.Position, Left, 608f, ContentWidth);
        AddFooter(page, 1);
    }

    private static void AddPlanningFirstPage(AppraisalPdfPage page, AppraisalPdfForm form)
    {
        Center(page, "PART II", 22f, 9f, true, PdfColor.Pink);
        Center(page, "PERFORMANCE PLANNING STAGE", 43f, 10f, true, PdfColor.Navy);
        page.WrappedText(
            "The Planning stage involves targets agreed on between the Appraisee and Appraiser at the beginning of the year or appraisal cycle.",
            new PdfRectangle(Left, 68f, ContentWidth, 42f), 7.5f, alignment: PdfTextAlignment.Center);

        var firstTargets = form.Targets.Take(3).ToArray();
        DrawPlanningTable(page, 120f, 620f, firstTargets, true);
        AddFooter(page, 2);
    }

    private static void AddPlanningSecondPage(AppraisalPdfPage page, AppraisalPdfForm form)
    {
        var remainingTargets = form.Targets.Skip(3).ToArray();
        DrawPlanningTable(page, 24f, 410f, remainingTargets, false);
        page.Text("Key Competencies Required:", Left, 456f, 8f, true, color: PdfColor.Navy);
        page.WrappedText(
            "(this is the behavioural / professional competencies the employee is expected to demonstrate/exhibit in achieving the set Targets) e.g. Personal Attributes; Professional Skills/Activities/ etc.",
            new PdfRectangle(Left, 474f, ContentWidth, 46f), 6.7f, italic: true, color: PdfColor.Muted);
        var competencies = form.KeyCompetencies.Count == 0
            ? string.Empty
            : string.Join("\n", form.KeyCompetencies.Select(value => $"- {value}"));
        page.WrappedText(competencies, new PdfRectangle(Left, 526f, ContentWidth, 108f), 7f);
        DrawDualSignatures(page, 670f, form.PlanningEmployeeSignature, "Employee Signature",
            form.PlanningSupervisorSignature, "Immediate Supervisor's Signature");
        AddFooter(page, 3);
    }

    private static void AddMidyearFirstPage(AppraisalPdfPage page, AppraisalPdfForm form)
    {
        Center(page, "PERFORMANCE /MID-YEAR PROGRESS REVIEW", 22f, 9f, true, PdfColor.Navy);
        page.WrappedText(
            "Appraiser and Appraisee meet mid-year to review and discuss the progress of work in relation to Targets set. The Supervisor reviews and makes any adjustments with regard to the activities, training programmes, timelines etc. where necessary, to achieve the desired outcome within the period. Appraiser and Appraisee should also discuss the extent to which behavioural standards or competencies are demonstrated, provided or lacking. Progress made and agreements reached after discussion should be recorded in the table below.",
            new PdfRectangle(Left, 49f, ContentWidth, 100f), 6.8f);
        Center(page, "Mid-Year Progress Review", 157f, 8f, true, PdfColor.Navy);
        DrawTargetProgressTable(page, 179f, 560f, form.Targets.Take(3).ToArray(), true);
        AddFooter(page, 4);
    }

    private static void AddMidyearSecondPage(AppraisalPdfPage page, AppraisalPdfForm form)
    {
        DrawTargetProgressTable(page, 22f, 312f, form.Targets.Skip(3).ToArray(), false);
        DrawCompetencyProgressTable(page, 353f, 386f, form.CompetencyProgress.Take(3).ToArray(), true);
        AddFooter(page, 5);
    }

    private static void AddMidyearThirdPage(AppraisalPdfPage page, AppraisalPdfForm form)
    {
        DrawCompetencyProgressTable(page, 22f, 342f, form.CompetencyProgress.Skip(3).ToArray(), false);
        page.Text("TRAINING NEED", Left, 395f, 8f, true, color: PdfColor.Navy);
        page.Text("Indicate and justify training need / required within the period", Left, 418f, 6.8f, italic: true);
        page.Rectangle(new PdfRectangle(Left, 440f, ContentWidth, 151f), 0.45f);
        page.WrappedText(form.TrainingNeed, new PdfRectangle(Left + 7f, 449f, ContentWidth - 14f, 132f), 7f);
        DrawDualSignatures(page, 646f, form.MidyearEmployeeSignature, "Employee Signature",
            form.MidyearSupervisorSignature, "Immediate Supervisor's Signature");
        AddFooter(page, 6);
    }

    private static void AddYearEndFirstPage(AppraisalPdfPage page, AppraisalPdfForm form)
    {
        Center(page, "PART III", 22f, 9f, true, PdfColor.Pink);
        Center(page, "END OF YEAR ASSESSMENT", 47f, 10f, true, PdfColor.Navy);
        Center(page, "(To be completed by Appraisee)", 72f, 7.5f, true, PdfColor.Navy);
        DrawYearEndTable(page, 101f, 566f, form.Targets.Take(3).ToArray(), true);
        DrawSingleSignature(page, 704f, form.YearEndEmployeeSubmissionSignature, "Employee Signature");
        AddFooter(page, 7);
    }

    private static void AddYearEndSecondPage(AppraisalPdfPage page, AppraisalPdfForm form)
    {
        DrawYearEndTable(page, 22f, 280f, form.Targets.Skip(3).ToArray(), false);
        Center(page, "(To be completed by Immediate Supervisor/Head of Division)", 326f, 7.5f, true, PdfColor.Navy);
        DrawTargetAssessmentTable(page, 351f, 294f, form.Targets);
        LabelValue(page, "Immediate Supervisor's Name:", form.YearEndSupervisorSignature.Name, Left, 670f,
            ContentWidth);
        DrawSingleSignature(page, 705f, form.YearEndSupervisorSignature, "Signature");
        AddFooter(page, 8);
    }

    private static void AddPartFourFirstPage(AppraisalPdfPage page, AppraisalPdfForm form)
    {
        Center(page, "PART IV", 22f, 9f, true, PdfColor.Pink);
        Center(page, "(To be completed by Immediate Supervisor/Head of Division)", 43f, 7.4f, true, PdfColor.Navy);
        Center(page, "PERFORMANCE STANDARD", 65f, 9f, true, PdfColor.Navy);
        page.Text("NOTE: Please refer to the guidelines before filling the assessment.", Left, 88f, 6.8f, true,
            color: PdfColor.Navy);
        page.Text("(TICK AS APPROPRIATE)", Left, 104f, 6.8f, true, color: PdfColor.Pink);
        DrawRatingTable(page, 128f, 611f, AppraisalFactors.Behavioral.Take(5).ToArray(),
            form.CompetencyRatings, 1, true,
            "ASSESSMENT FACTORS (A)\nNON-Core Competencies/Behavioral Attributes", null, false, false);
        AddFooter(page, 9);
    }

    private static void AddPartFourSecondPage(AppraisalPdfPage page, AppraisalPdfForm form)
    {
        DrawRatingTable(page, 22f, 272f, AppraisalFactors.Behavioral.Skip(5).ToArray(),
            form.CompetencyRatings, 6, false, null, form.BehavioralScore, true, false);
        DrawRatingTable(page, 323f, 416f, AppraisalFactors.Core.Take(6).ToArray(),
            form.CompetencyRatings, 1, true,
            "ASSESSMENT FACTORS (B)\nCore Competencies and Job Knowledge/Professional Skills", null, false, false);
        AddFooter(page, 10);
    }

    private static void AddPartFourThirdPage(AppraisalPdfPage page, AppraisalPdfForm form)
    {
        DrawRatingTable(page, 22f, 228f, AppraisalFactors.Core.Skip(6).ToArray(),
            form.CompetencyRatings, 7, false, null, form.CoreScore, true, true, form.TotalScore);
        page.Text("NB: Total Score (N) = total applicable score / total applicable values X (total number of values)",
            Left, 277f, 6.8f, true);
        page.WrappedText(
            "- Total Applicable Score is the sum of the total performance grading (N) for the applicable variables under each of the core and non-core competencies.\n- Total Applicable values is the count of applicable variables.\n- Total Number of Values is the total number of Assessment Factors (labelled A or B) in the appraisal form (in this case, 10).\n\nThis formula ensures that the overall score is scaled to fit the total possible score of 50, regardless of the number of applicable variables.",
            new PdfRectangle(Left + 16f, 301f, ContentWidth - 16f, 112f), 6.2f);

        page.Text("COMMENTS BY SUPERVISOR ON APPRAISEE", Left, 438f, 7.5f, true, color: PdfColor.Navy);
        page.Rectangle(new PdfRectangle(Left, 458f, ContentWidth, 69f), 0.4f);
        page.WrappedText(form.SupervisorComments, new PdfRectangle(Left + 6f, 464f, ContentWidth - 12f, 57f), 6.8f);
        DrawSingleSignature(page, 544f, form.YearEndSupervisorSignature,
            "Signature of Supervisor or Head of Division/Unit");

        page.Text("COMMENTS BY APPRAISEE ON SUPERVISOR'S ASSESSMENT", Left, 612f, 7.5f, true,
            color: PdfColor.Navy);
        page.Rectangle(new PdfRectangle(Left, 632f, ContentWidth, 48f), 0.4f);
        page.WrappedText(form.EmployeeComments, new PdfRectangle(Left + 6f, 638f, ContentWidth - 12f, 36f), 6.8f);
        DrawSingleSignature(page, 697f, form.YearEndEmployeeSignature, "Signature of Employee");
        AddFooter(page, 11);
    }

    private static void AddPartFive(AppraisalPdfPage page, AppraisalPdfForm form)
    {
        Center(page, "PART V", 22f, 9f, true, PdfColor.Pink);
        Center(page, "(To be completed by Director of Institute)", 43f, 7.4f, true, PdfColor.Navy);
        page.Text("OVERALL ASSESSMENT: (REFER TO PART III)", Left, 74f, 8f, true, color: PdfColor.Navy);
        DrawScoreBandTable(page, 98f, form.TotalScore);

        page.Text("COMMENTS ON WORK ACCOMPLISHED BY OFFICER", Left, 239f, 7.5f, true,
            color: PdfColor.Navy);
        page.Rectangle(new PdfRectangle(Left, 260f, ContentWidth, 126f), 0.45f);
        page.WrappedText(form.DirectorAssessment.CommentsOnWork,
            new PdfRectangle(Left + 7f, 269f, ContentWidth - 14f, 108f), 7f);

        page.Text("RECOMMENDATIONS", Left, 414f, 8f, true, color: PdfColor.Navy);
        page.Text("The Officer is recommended for:", Left, 438f, 7f);
        Recommendation(page, 1, "Consideration for promotion to", form.DirectorAssessment.ConsiderPromotionTo, 466f);
        Recommendation(page, 2, "Performance bonus", form.DirectorAssessment.PerformanceBonus, 493f);
        Recommendation(page, 3, "Training in", form.DirectorAssessment.Training, 520f);
        Recommendation(page, 4, "Reassignment", form.DirectorAssessment.Reassignment, 547f);
        Recommendation(page, 5, "Reprimand/caution", form.DirectorAssessment.ReprimandOrCaution, 574f);
        Recommendation(page, 6, "Termination of appointment", form.DirectorAssessment.TerminationOfAppointment, 601f);
        DrawSingleSignature(page, 686f, form.DirectorSignature, "Signature of Director");
        AddFooter(page, 12);
    }

    private static void AddPartSixFirstPage(AppraisalPdfPage page)
    {
        Center(page, "PART VI", 278f, 9f, true, PdfColor.Pink);
        Center(page, "(APPENDIX)", 299f, 8f, true, PdfColor.Navy);
        page.Text("Guidelines for Filling the CSIR Performance Appraisal Form - Assessment PART IV", Left, 332f,
            7.5f, true, color: PdfColor.Navy);
        page.Text("1. Understanding the Assessment Structure:", Left + 16f, 360f, 7f, true);
        page.WrappedText(
            "- The assessment consists of 20 competency areas divided into Behavioral Competency and Core Competency, each with a maximum score of 50 marks.\n\n- The overall score is calculated out of 100%, with equal weightage given to both categories.\n\n- Ensure that you are aware of the specific competencies relevant to the role and category.",
            new PdfRectangle(Left + 16f, 385f, ContentWidth - 16f, 128f), 6.6f);
        DrawGuidanceTable(page, 548f, 191f, "Assessment of Non-Core Competence (A)",
            AppraisalFactors.BehavioralRatingGuidance.Take(1).ToArray(), true);
        AddFooter(page, 13);
    }

    private static void AddPartSixSecondPage(AppraisalPdfPage page)
    {
        DrawGuidanceTable(page, 22f, 278f, "Assessment of Non-Core Competence (A)",
            AppraisalFactors.BehavioralRatingGuidance.Skip(1).ToArray(), false);
        DrawGuidanceTable(page, 330f, 409f, "Assessment of Core Competence (B)",
            AppraisalFactors.CoreRatingGuidance, true);
        AddFooter(page, 14);
    }

    private static void DrawPlanningTable(
        AppraisalPdfPage page,
        float top,
        float height,
        IReadOnlyList<AppraisalPdfTarget> targets,
        bool includeHeader)
    {
        var rows = targets.Count == 0
            ? new[] { new[] { string.Empty, string.Empty, string.Empty } }
            : targets.Select(target => new[]
            {
                $"{target.DisplayOrder}. {target.CoreArea}",
                $"{target.DisplayOrder}. {target.Target}\nTimeline: {target.Timeline}",
                $"{target.DisplayOrder}. {target.ResourcesRequired}"
            }).ToArray();
        DrawTable(page, Left, top, ContentWidth, [0.28f, 0.40f, 0.32f],
            includeHeader
                ?
                [
                    "CORE AREAS\n(This should be drawn from the Job Descriptions of the employee)",
                    "TARGETS\n(Expected Results: should be SMART (specific, measurable, achievable, realistic & time-bound) e.g., Complete Activity 'X' by time 'T')",
                    "RESOURCES REQUIRED\n(Agree on resources and supervision required to achieve Targets set)"
                ]
                : null,
            rows, includeHeader ? 88f : 0f, height, 6f, 6.4f);
    }

    private static void DrawTargetProgressTable(
        AppraisalPdfPage page,
        float top,
        float height,
        IReadOnlyList<AppraisalPdfTarget> targets,
        bool includeHeader)
    {
        var rows = targets.Count == 0
            ? new[] { new[] { string.Empty, string.Empty, string.Empty, string.Empty } }
            : targets.Select(target => new[]
            {
                target.DisplayOrder.ToString(),
                target.Target,
                target.MidyearProgress ?? string.Empty,
                target.MidyearRemarks ?? string.Empty
            }).ToArray();
        DrawTable(page, Left, top, ContentWidth, [0.07f, 0.27f, 0.39f, 0.27f],
            includeHeader ? ["NO.", "TARGET", "PROGRESS REVIEW", "REMARKS"] : null,
            rows, includeHeader ? 34f : 0f, height, 6.3f, 6.2f);
    }

    private static void DrawCompetencyProgressTable(
        AppraisalPdfPage page,
        float top,
        float height,
        IReadOnlyList<AppraisalPdfCompetencyProgress> competencies,
        bool includeHeader)
    {
        var rows = competencies.Count == 0
            ? new[] { new[] { string.Empty, string.Empty, string.Empty, string.Empty } }
            : competencies.Select(item => new[]
            {
                item.DisplayOrder.ToString(),
                item.Competency,
                item.ProgressReview ?? string.Empty,
                item.Remarks ?? string.Empty
            }).ToArray();
        DrawTable(page, Left, top, ContentWidth, [0.07f, 0.38f, 0.29f, 0.26f],
            includeHeader ? ["NO.", "COMPETENCY", "PROGRESS REVIEW", "REMARKS"] : null,
            rows, includeHeader ? 34f : 0f, height, 6.3f, 6.1f);
    }

    private static void DrawYearEndTable(
        AppraisalPdfPage page,
        float top,
        float height,
        IReadOnlyList<AppraisalPdfTarget> targets,
        bool includeHeader)
    {
        var rows = targets.Count == 0
            ? new[] { new[] { string.Empty, string.Empty, string.Empty, string.Empty } }
            : targets.Select(target => new[]
            {
                target.DisplayOrder.ToString(),
                target.Target,
                target.WorkAccomplished ?? string.Empty,
                $"{target.WorkCompletedPercentage:0}%\n{target.ExtentAndConstraints}"
            }).ToArray();
        DrawTable(page, Left, top, ContentWidth, [0.06f, 0.29f, 0.27f, 0.38f],
            includeHeader
                ? ["NO.", "TARGETS AS AGREED WITH SUPERVISOR", "WORK ACCOMPLISHED", "STATE EXTENT OF WORK DONE OR NOT DONE WITH REASONS / CONSTRAINTS"]
                : null,
            rows, includeHeader ? 58f : 0f, height, 5.9f, 6.1f);
    }

    private static void DrawTargetAssessmentTable(
        AppraisalPdfPage page,
        float top,
        float height,
        IReadOnlyList<AppraisalPdfTarget> targets)
    {
        var rows = targets.Count == 0
            ? new[] { new[] { string.Empty, string.Empty, string.Empty, string.Empty } }
            : targets.Select(target => new[]
            {
                target.DisplayOrder.ToString(),
                target.Target,
                AssessmentLabel(target.PerformanceAssessment),
                target.PerformanceComments ?? string.Empty
            }).ToArray();
        DrawTable(page, Left, top, ContentWidth, [0.06f, 0.34f, 0.25f, 0.35f],
            ["NO.", "TARGETS AS AGREED WITH APPRAISEE", "PERFORMANCE ASSESSMENT", "COMMENTS / GENERAL REMARKS"],
            rows, 48f, height, 5.9f, 6f);
    }

    private static void DrawRatingTable(
        AppraisalPdfPage page,
        float top,
        float height,
        IReadOnlyList<AppraisalFactor> factors,
        IReadOnlyList<AppraisalPdfCompetencyRating> ratings,
        int startNumber,
        bool includeHeader,
        string? heading,
        decimal? total,
        bool includeTotal,
        bool includeGrand,
        decimal? grandTotal = null)
    {
        var widths = new[] { 24f, 205f, 43f, 43f, 43f, 43f, 43f, 84f };
        var headerHeight = includeHeader ? 76f : 0f;
        var summaryRows = (includeTotal ? 1 : 0) + (includeGrand ? 1 : 0);
        var summaryHeight = summaryRows * 27f;
        var bodyHeight = Math.Max(1f, height - headerHeight - summaryHeight);
        var rowHeight = bodyHeight / Math.Max(1, factors.Count);
        var boundaries = ColumnBoundaries(Left, widths);
        page.Rectangle(new PdfRectangle(Left, top, ContentWidth, height), 0.55f);

        if (includeHeader)
        {
            page.Rectangle(new PdfRectangle(Left, top, ContentWidth, headerHeight), 0.45f,
                fill: PdfColor.HeaderFill);
            page.WrappedText(heading, new PdfRectangle(Left + 4f, top + 6f, widths[0] + widths[1] - 8f,
                headerHeight - 12f), 6.3f, true, alignment: PdfTextAlignment.Center);
            var ratingHeaders = new[]
            {
                "Exceptional\n(5)",
                "Exceeded\nExpectation\n(4)",
                "Met all\nExpectation\n(3)",
                "Below\nExpectation\n(2)",
                "Unacceptable\n(1)",
                "PERFORMANCE\nGRADING\n(N)"
            };
            for (var index = 0; index < ratingHeaders.Length; index++)
            {
                var column = index + 2;
                page.WrappedText(ratingHeaders[index],
                    new PdfRectangle(boundaries[column] + 2f, top + 7f, widths[column] - 4f,
                        headerHeight - 14f), 5.2f, true, alignment: PdfTextAlignment.Center,
                    minimumFontSize: 4.4f);
            }
            for (var column = 2; column < boundaries.Length - 1; column++)
                page.Line(boundaries[column], top, boundaries[column], top + height, 0.4f);
            page.Line(boundaries[1], top + headerHeight, boundaries[1], top + height, 0.4f);
            page.Line(Left, top + headerHeight, Left + ContentWidth, top + headerHeight, 0.45f);
        }
        else
        {
            for (var column = 1; column < boundaries.Length - 1; column++)
                page.Line(boundaries[column], top, boundaries[column], top + height, 0.4f);
        }

        var ratingLookup = ratings.ToDictionary(item => item.Code, item => item.Rating);
        for (var index = 0; index < factors.Count; index++)
        {
            var rowTop = top + headerHeight + index * rowHeight;
            page.Line(Left, rowTop, Left + ContentWidth, rowTop, 0.4f);
            page.WrappedText((startNumber + index).ToString(),
                new PdfRectangle(Left + 2f, rowTop + 3f, widths[0] - 4f, rowHeight - 6f), 5.8f,
                alignment: PdfTextAlignment.Center);
            page.WrappedText(factors[index].Label,
                new PdfRectangle(boundaries[1] + 4f, rowTop + 3f, widths[1] - 8f, rowHeight - 6f), 5.8f,
                minimumFontSize: 4.2f);
            ratingLookup.TryGetValue(factors[index].Code, out var rating);
            if (rating.HasValue)
            {
                var ratingColumn = 7 - rating.Value;
                page.Text("X", boundaries[ratingColumn], rowTop + Math.Max(5f, rowHeight / 2f - 5f), 8f,
                    true, alignment: PdfTextAlignment.Center, availableWidth: widths[ratingColumn]);
                page.Text(rating.Value.ToString(), boundaries[7], rowTop + Math.Max(5f, rowHeight / 2f - 5f),
                    7f, true, alignment: PdfTextAlignment.Center, availableWidth: widths[7]);
            }
            else
            {
                page.Text("N/A", boundaries[7], rowTop + Math.Max(5f, rowHeight / 2f - 5f), 6f, true,
                    alignment: PdfTextAlignment.Center, availableWidth: widths[7]);
            }
        }

        var summaryTop = top + headerHeight + factors.Count * rowHeight;
        if (includeTotal)
        {
            page.Line(Left, summaryTop, Left + ContentWidth, summaryTop, 0.45f);
            page.Text("TOTAL SCORE (50 Marks)", Left, summaryTop + 7f, 6.5f, true,
                alignment: PdfTextAlignment.Right, availableWidth: ContentWidth - widths[7] - 8f);
            page.Text(Score(total), boundaries[7], summaryTop + 7f, 7f, true,
                alignment: PdfTextAlignment.Center, availableWidth: widths[7]);
            summaryTop += 27f;
        }
        if (includeGrand)
        {
            page.Line(Left, summaryTop, Left + ContentWidth, summaryTop, 0.45f);
            page.Text("GRAND TOTAL (%) (A+B)", Left, summaryTop + 7f, 6.5f, true,
                alignment: PdfTextAlignment.Right, availableWidth: ContentWidth - widths[7] - 8f);
            page.Text(Score(grandTotal), boundaries[7], summaryTop + 7f, 7f, true,
                alignment: PdfTextAlignment.Center, availableWidth: widths[7]);
        }
    }

    private static void DrawScoreBandTable(AppraisalPdfPage page, float top, decimal? totalScore)
    {
        var widths = new[] { 119f, 82f, 82f, 82f, 82f, 81f };
        var headers = new[] { "SCORE", "70% & above", "60-69%", "50-59%", "40-49%", "0-39%" };
        var descriptions = new[]
        {
            "DESCRIPTION",
            "Exceptional / Outstanding",
            "Competent / very able and effective",
            "Fair / Average",
            "Below Average",
            "Poor"
        };
        var actual = new[] { "Please indicate actual percentage score", string.Empty, string.Empty, string.Empty,
            string.Empty, string.Empty };
        if (totalScore.HasValue)
            actual[BandColumn(totalScore.Value)] = $"{totalScore:0.##}%";
        DrawTable(page, Left, top, ContentWidth, widths.Select(value => value / ContentWidth).ToArray(), headers,
            [descriptions, actual], 31f, 100f, 5.8f, 5.8f);
    }

    private static void DrawGuidanceTable(
        AppraisalPdfPage page,
        float top,
        float height,
        string heading,
        IReadOnlyList<AppraisalRatingGuidance> guidance,
        bool includeHeader)
    {
        var rows = guidance.Select(item => new[]
        {
            $"{item.Label} ({item.Rating})",
            item.Explanation
        }).ToArray();
        if (rows.Length == 0)
            rows = [new[] { string.Empty, string.Empty }];
        var headers = includeHeader ? new[] { $"{heading}\nRatings", "Explanations" } : null;
        DrawTable(page, Left, top, ContentWidth, [0.29f, 0.71f], headers, rows,
            includeHeader ? 48f : 0f, height, 6f, 5.7f);
    }

    private static void DrawTable(
        AppraisalPdfPage page,
        float x,
        float top,
        float width,
        IReadOnlyList<float> ratios,
        IReadOnlyList<string>? headers,
        IReadOnlyList<string[]> rows,
        float headerHeight,
        float height,
        float headerFontSize,
        float bodyFontSize)
    {
        var widths = ratios.Select(ratio => ratio * width).ToArray();
        widths[^1] += width - widths.Sum();
        var boundaries = ColumnBoundaries(x, widths);
        page.Rectangle(new PdfRectangle(x, top, width, height), 0.55f);
        for (var column = 1; column < boundaries.Length - 1; column++)
            page.Line(boundaries[column], top, boundaries[column], top + height, 0.4f);

        var bodyTop = top;
        if (headers is not null)
        {
            page.Rectangle(new PdfRectangle(x, top, width, headerHeight), 0.4f, fill: PdfColor.HeaderFill);
            for (var column = 0; column < headers.Count; column++)
                page.WrappedText(headers[column],
                    new PdfRectangle(boundaries[column] + 3f, top + 4f, widths[column] - 6f,
                        headerHeight - 8f), headerFontSize, true, alignment: PdfTextAlignment.Center,
                    minimumFontSize: 4.2f);
            bodyTop += headerHeight;
            page.Line(x, bodyTop, x + width, bodyTop, 0.45f);
        }

        var rowHeight = Math.Max(1f, (height - headerHeight) / Math.Max(1, rows.Count));
        for (var row = 0; row < rows.Count; row++)
        {
            var rowTop = bodyTop + row * rowHeight;
            if (row > 0)
                page.Line(x, rowTop, x + width, rowTop, 0.4f);
            for (var column = 0; column < widths.Length; column++)
            {
                var value = column < rows[row].Length ? rows[row][column] : string.Empty;
                page.WrappedText(value,
                    new PdfRectangle(boundaries[column] + 4f, rowTop + 4f, widths[column] - 8f,
                        rowHeight - 8f), bodyFontSize,
                    alignment: column == 0 && widths[column] < 60f ? PdfTextAlignment.Center : PdfTextAlignment.Left,
                    minimumFontSize: 3.8f);
            }
        }
    }

    private static void DrawDualSignatures(
        AppraisalPdfPage page,
        float top,
        AppraisalPdfSignature left,
        string leftLabel,
        AppraisalPdfSignature right,
        string rightLabel)
    {
        const float gap = 34f;
        var width = (ContentWidth - gap) / 2f;
        DrawSignature(page, Left, top, width, left, leftLabel);
        DrawSignature(page, Left + width + gap, top, width, right, rightLabel);
    }

    private static void DrawSingleSignature(
        AppraisalPdfPage page,
        float top,
        AppraisalPdfSignature signature,
        string label)
    {
        DrawSignature(page, Left, top, ContentWidth, signature, label);
    }

    private static void DrawSignature(
        AppraisalPdfPage page,
        float x,
        float top,
        float width,
        AppraisalPdfSignature signature,
        string label)
    {
        var dateWidth = Math.Min(120f, width * 0.30f);
        var nameWidth = width - dateWidth - 18f;
        page.Text(signature.Name, x, top, 7f, true, availableWidth: nameWidth);
        page.Text(signature.SignedAt.HasValue ? Date(signature.SignedAt.Value.Date) : string.Empty,
            x + nameWidth + 18f, top, 7f, true, alignment: PdfTextAlignment.Right, availableWidth: dateWidth);
        page.Line(x, top + 18f, x + nameWidth, top + 18f, 0.45f);
        page.Line(x + nameWidth + 18f, top + 18f, x + width, top + 18f, 0.45f);
        page.Text(label, x, top + 23f, 5.8f, availableWidth: nameWidth);
        page.Text("Date", x + nameWidth + 18f, top + 23f, 5.8f, alignment: PdfTextAlignment.Right,
            availableWidth: dateWidth);
    }

    private static void Recommendation(AppraisalPdfPage page, int number, string label, string? value, float top)
    {
        var prefix = $"{number}. {label}:";
        page.Text(prefix, Left + 18f, top, 7f);
        var valueX = Left + 18f + AppraisalPdfPage.Measure(prefix, 7f) + 7f;
        page.Text(value, valueX, top, 7f, true, availableWidth: Left + ContentWidth - valueX);
        page.Line(valueX, top + 15f, Left + ContentWidth, top + 15f, 0.35f);
    }

    private static void LabelValue(
        AppraisalPdfPage page,
        string label,
        string? value,
        float x,
        float top,
        float width)
    {
        page.Text(label, x, top, 7f, true);
        var valueX = x + AppraisalPdfPage.Measure(label, 7f, true) + 7f;
        page.Text(value, valueX, top, 7f, availableWidth: Math.Max(0f, width - (valueX - x)));
        page.Line(valueX, top + 14f, x + width, top + 14f, 0.3f, PdfColor.Muted);
    }

    private static void Center(
        AppraisalPdfPage page,
        string text,
        float top,
        float size,
        bool bold,
        PdfColor color)
    {
        page.Text(text, Left, top, size, bold, alignment: PdfTextAlignment.Center,
            availableWidth: ContentWidth, color: color);
    }

    private static void AddFooter(AppraisalPdfPage page, int number)
    {
        page.Text(number.ToString(), Left, 770f, 6.5f, alignment: PdfTextAlignment.Center,
            availableWidth: ContentWidth, color: PdfColor.Muted);
    }

    private static float[] ColumnBoundaries(float x, IReadOnlyList<float> widths)
    {
        var boundaries = new float[widths.Count + 1];
        boundaries[0] = x;
        for (var index = 0; index < widths.Count; index++)
            boundaries[index + 1] = boundaries[index] + widths[index];
        return boundaries;
    }

    private static string TitleSelection(string? title)
    {
        var normalized = title?.Trim();
        var mr = string.Equals(normalized, "Mr", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(normalized, "Mr.", StringComparison.OrdinalIgnoreCase);
        var mrs = string.Equals(normalized, "Mrs", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(normalized, "Mrs.", StringComparison.OrdinalIgnoreCase);
        var ms = string.Equals(normalized, "Ms", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(normalized, "Ms.", StringComparison.OrdinalIgnoreCase);
        var other = mr || mrs || ms ? string.Empty : normalized;
        return $"{Check(mr)} Mr.    {Check(mrs)} Mrs.    {Check(ms)} Ms.    Other (Pls specify): {Check(!string.IsNullOrWhiteSpace(other))} {other}";
    }

    private static string Check(bool selected) => selected ? "X" : "[ ]";

    private static string AssessmentLabel(short? rating) => rating switch
    {
        5 => "Exceptional",
        4 => "Exceeded Expectation",
        3 => "Met all Expectation",
        2 => "Below Expectation",
        1 => "Unacceptable",
        _ => string.Empty
    };

    private static int BandColumn(decimal score) => score switch
    {
        >= 70m => 1,
        >= 60m => 2,
        >= 50m => 3,
        >= 40m => 4,
        _ => 5
    };

    private static string Date(DateTime? value) => value.HasValue ? value.Value.ToString("dd MMMM yyyy") : string.Empty;

    private static string Score(decimal? value) => value.HasValue ? value.Value.ToString("0.##") : string.Empty;

    private static byte[] LoadOfficialLogo()
    {
        const string resourceName =
            "Csir.Spme.Infrastructure.Communications.Templates.CSIR-performance-appraisal-logo.jpeg";
        using var stream = typeof(AppraisalPdf).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The official CSIR appraisal logo resource is missing.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
