using Csir.Spme.Application.Common.Interfaces;

namespace Csir.Spme.Infrastructure.Communications;

public static class SkeletalStaffServiceReportPdf
{
    public static byte[] Build(SkeletalStaffServiceReportContent content) =>
        StaffQuarterlyReportPdf.BuildSimpleReport(
            "Skeletal staff service report",
            content.Lines);

    public sealed record SkeletalStaffServiceReportContent(IReadOnlyList<string> Lines);
}
