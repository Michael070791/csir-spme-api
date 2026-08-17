using System.Text;
using Csir.Spme.Application.Common.Interfaces;

namespace Csir.Spme.Infrastructure.Communications;

public static class StaffQuarterlyReportPdf
{
    public static byte[] BuildSimpleReport(string title, IReadOnlyList<string> lines)
    {
        var content = new List<string> { "CSIR SPME", title, string.Empty };
        content.AddRange(lines);
        var wrapped = content.SelectMany(Wrap).ToList();
        return BuildFromLines(wrapped);
    }

    public static byte[] Build(StaffQuarterlyReportNotification notification)
    {
        var lines = new List<string>
        {
            "CSIR SPME",
            "Staff quarterly report",
            string.Empty,
            $"Staff: {Text(notification.StaffDisplayName, "Staff member")}",
            $"Reviewer: {Text(notification.ReviewerDisplayName, "HOD")}",
            $"Quarter: {Text(notification.PeriodName, "Quarter")}",
            $"Title: {Text(notification.Title, "Untitled")}",
            string.Empty,
            "Research abstract",
            Text(notification.Abstract, "Not provided"),
            string.Empty,
            "Work completed",
            Text(notification.WorkSummary, "Not provided"),
            string.Empty,
            "Key results",
            Text(notification.KeyResults, "Not provided"),
            string.Empty,
            "Conclusion and next steps",
            Text(notification.ConclusionNextSteps, "Not provided"),
            string.Empty,
            "Projects",
            notification.Projects.Count == 0 ? "None" : string.Join(", ", notification.Projects),
            string.Empty,
            "Technologies",
            notification.Technologies.Count == 0 ? "None" : string.Join(", ", notification.Technologies),
            string.Empty
        };

        foreach (var project in notification.ProjectReports)
        {
            lines.AddRange(
            [
                $"FORM 1 - PROJECT INCEPTION: {project.Title}",
                $"PIN: {Text(project.Pin, "PIN not yet assigned")}",
                $"Project code: {Text(project.Code, "Not provided")}",
                $"Principal investigator: {Text(project.LeadName, "Not provided")}",
                $"Estimated duration: {Text(project.EstimatedDuration, "Not provided")}",
                $"Sponsors: {Text(project.SponsorName, "Not provided")}",
                $"Location: {Text(project.Location, "Not provided")}",
                "Objectives",
                Text(project.Objective, "Not provided"),
                "Background and justification",
                Text(project.Justification, "Not provided"),
                "Method",
                Text(project.Method, "Not provided"),
                "Expected beneficiaries",
                Text(project.ExpectedBeneficiaries, "Not provided"),
                "Potential technology",
                Text(project.PotentialTechnology, "Not provided"),
                "Commercialization",
                Text(project.Commercialization, "Not provided"),
                "Contribution to knowledge",
                Text(project.ContributionToKnowledge, "Not provided"),
                string.Empty,
                $"FORM 2 - RESEARCH IN PROGRESS: {project.Title}",
                "Summary of progress since last report",
                Text(project.ProgressSummary, "Not provided"),
                "Key results / outputs",
                Text(project.ProgressKeyResults, "Not provided"),
                "Challenges encountered",
                Text(project.Challenges, "Not provided"),
                "Activities planned for next quarter",
                Text(project.NextQuarterActivities, "Not provided"),
                "Way forward",
                Text(project.WayForward, "Not provided"),
                $"Conference papers produced: {project.ConferencePapersProduced}",
                $"IP-protected technologies: {project.IpTechnologiesProtected}",
                string.Empty
            ]);
        }

        lines.Add("Report image attachments");
        lines.Add(notification.ImageFileNames.Count == 0
            ? "None"
            : string.Join(", ", notification.ImageFileNames));

        var wrapped = lines.SelectMany(Wrap).ToList();
        return BuildFromLines(wrapped);
    }

    private static byte[] BuildFromLines(IReadOnlyList<string> wrapped)
    {
        const int linesPerPage = 46;
        var pageCount = Math.Max(1, (int)Math.Ceiling(wrapped.Count / (double)linesPerPage));
        var pages = Enumerable.Range(0, pageCount)
            .Select(page => wrapped.Skip(page * linesPerPage).Take(linesPerPage).ToList())
            .ToList();

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(' ', pages.Select((_, index) => $"{PageObjectNumber(index)} 0 R"))}] /Count {pages.Count} >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        foreach (var (pageLines, index) in pages.Select((value, index) => (value, index)))
        {
            var content = BuildPageContent(pageLines);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {ContentObjectNumber(index)} 0 R >>");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }

        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
            builder.Append($"{offset:D10} 00000 n \n");
        builder.Append($"trailer << /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static int PageObjectNumber(int pageIndex) => 4 + (pageIndex * 2);
    private static int ContentObjectNumber(int pageIndex) => PageObjectNumber(pageIndex) + 1;

    private static string BuildPageContent(IReadOnlyList<string> lines)
    {
        var commands = new StringBuilder("BT /F1 11 Tf 50 760 Td 14 TL");
        foreach (var line in lines)
            commands.Append($" ({Escape(line)}) '");
        commands.Append(" ET");
        return commands.ToString();
    }

    private static IEnumerable<string> Wrap(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield return string.Empty;
            yield break;
        }

        const int width = 92;
        var remaining = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (remaining.Length > width)
        {
            var split = remaining.LastIndexOf(' ', width);
            if (split < 40)
                split = width;
            yield return remaining[..split].TrimEnd();
            remaining = remaining[split..].TrimStart();
        }

        yield return remaining;
    }

    private static string Text(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        var plain = System.Text.RegularExpressions.Regex.Replace(value, "<[^>]+>", " ");
        plain = System.Net.WebUtility.HtmlDecode(plain).Trim();
        return string.IsNullOrWhiteSpace(plain) ? fallback : plain;
    }

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch is '(' or ')' or '\\')
                builder.Append('\\');
            builder.Append(ch is >= ' ' and <= '~' ? ch : '?');
        }

        return builder.ToString();
    }
}
