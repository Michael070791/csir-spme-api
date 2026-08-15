using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Promotions;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Infrastructure.Persistence;

public partial class SpmeDbContext
{
    async Task<PromotionReportAggregate?> IPromotionReportRepository.FindAsync(
        Guid promotionSubmissionId,
        string reportType,
        CancellationToken ct)
    {
        return await (
            from report in PromotionSubmissionReports
            join submission in PromotionSubmissions
                on report.PromotionSubmissionId equals submission.Id
            join requirement in PromotionSubmissionRequirementSnapshots
                on report.RequirementSnapshotId equals requirement.Id
            where report.PromotionSubmissionId == promotionSubmissionId
                && report.ReportType == reportType
                && requirement.PromotionSubmissionId == promotionSubmissionId
                && requirement.RequirementType == PromotionConstants.RequirementReport
                && (requirement.ReportTemplateCode == reportType ||
                    (requirement.ReportTemplateCode == null && requirement.Code == reportType))
            select new PromotionReportAggregate(submission, requirement, report))
            .SingleOrDefaultAsync(ct);
    }
}
