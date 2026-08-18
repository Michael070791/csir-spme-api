using Csir.Spme.Application.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class EmployeeSelfWorkService
{
    internal const decimal DaysPerPromotionYear = 365.2425m;

    internal static async Task<EmployeeSelfWorkResponse?> BuildAsync(
        Guid employeeId,
        SpmeDbContext db,
        CancellationToken cancellationToken)
    {
        var currentEmployment = await db.EmploymentRecords.AsNoTracking()
            .SingleOrDefaultAsync(record => record.EmployeeId == employeeId && record.IsCurrent, cancellationToken);
        if (currentEmployment is null)
            return null;

        var selfReportedRecords = await db.EmployeeGradePromotionDates.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);
        var selfReported = selfReportedRecords.ToDictionary(item => item.GradeId, item => item.PromotionDate);

        var employmentRecords = await db.EmploymentRecords.AsNoTracking()
            .Where(record => record.EmployeeId == employeeId && record.GradeId.HasValue)
            .OrderByDescending(record => record.EffectiveFrom)
            .ToListAsync(cancellationToken);
        var employmentByGrade = employmentRecords
            .GroupBy(record => record.GradeId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(record => RecordedPromotionDate(
                    record.PromotionDate,
                    record.EffectiveFrom,
                    record.AppointmentDate ?? currentEmployment.AppointmentDate)).FirstOrDefault());

        Grade? currentGrade = null;
        if (currentEmployment.GradeId is Guid currentGradeId)
            currentGrade = await db.Grades.AsNoTracking().SingleOrDefaultAsync(item => item.Id == currentGradeId, cancellationToken);

        var ladder = await LoadGradeLadderAsync(db, currentEmployment.StaffCategory, currentGrade, cancellationToken);
        var gradePromotions = ladder
            .Select(grade =>
            {
                var isCurrent = currentEmployment.GradeId == grade.Id;
                var promotionDate = ResolvePromotionDate(grade.Id, selfReported, employmentByGrade);
                if (isCurrent)
                {
                    promotionDate = RecordedPromotionDate(
                        promotionDate,
                        currentEmployment.EffectiveFrom,
                        currentEmployment.AppointmentDate);
                }
                return new EmployeeSelfWorkGradePromotionResponse(
                    grade.Id,
                    grade.Code,
                    grade.Name,
                    isCurrent,
                    promotionDate);
            })
            .ToList();

        var divisionName = currentEmployment.DivisionId is Guid divisionId
            ? await db.Divisions.AsNoTracking()
                .Where(division => division.Id == divisionId)
                .Select(division => division.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var sectionName = currentEmployment.SectionId is Guid sectionId
            ? await db.Sections.AsNoTracking()
                .Where(section => section.Id == sectionId)
                .Select(section => section.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var instituteName = await db.Institutes.AsNoTracking()
            .Where(institute => institute.Id == currentEmployment.InstituteId)
            .Select(institute => institute.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var yearsInCurrentGrade = YearsInCurrentGrade(
            currentEmployment.GradeId,
            selfReported,
            employmentRecords,
            RecordedPromotionDate(
                currentEmployment.PromotionDate,
                currentEmployment.EffectiveFrom,
                currentEmployment.AppointmentDate),
            currentEmployment.AppointmentDate,
            currentEmployment.EffectiveFrom,
            DateTimeOffset.UtcNow);

        var updatedAt = selfReportedRecords.Count > 0
            ? selfReportedRecords.Max(item => item.UpdatedAt)
            : currentEmployment.UpdatedAt;

        return new EmployeeSelfWorkResponse(
            currentEmployment.AppointmentDate?.Date,
            new EmployeeSelfWorkCurrentGradeResponse(
                currentGrade?.Id,
                currentGrade?.Code,
                currentGrade?.Name ?? currentEmployment.JobTitle),
            decimal.Round(yearsInCurrentGrade, 2),
            currentEmployment.StaffCategory,
            currentEmployment.JobTitle,
            instituteName,
            divisionName,
            sectionName,
            currentEmployment.Location,
            currentEmployment.AreaOfSpecialization,
            currentEmployment.ResearchInterests,
            gradePromotions,
            updatedAt);
    }

    internal static async Task<Error?> ValidateUpdateAsync(
        Guid employeeId,
        UpdateEmployeeSelfWorkRequest request,
        SpmeDbContext db,
        CancellationToken cancellationToken)
    {
        var currentEmployment = await db.EmploymentRecords.AsNoTracking()
            .SingleOrDefaultAsync(record => record.EmployeeId == employeeId && record.IsCurrent, cancellationToken);
        if (currentEmployment is null)
            return Error.Validation("A current employment record is required before work history can be saved.");

        if (request.GradePromotions is { Count: > 0 })
        {
            Grade? currentGrade = null;
            if (currentEmployment.GradeId is Guid currentGradeId)
                currentGrade = await db.Grades.AsNoTracking().SingleOrDefaultAsync(item => item.Id == currentGradeId, cancellationToken);

            var allowedGradeIds = (await LoadGradeLadderAsync(db, currentEmployment.StaffCategory, currentGrade, cancellationToken))
                .Select(item => item.Id)
                .ToHashSet();

            var today = DateTime.UtcNow.Date;
            foreach (var item in request.GradePromotions)
            {
                if (!allowedGradeIds.Contains(item.GradeId))
                    return Error.Validation("One or more grades are outside your authorized promotion history.");

                if (item.PromotionDate is null)
                    continue;

                var promotionDate = item.PromotionDate.Value.Date;
                if (promotionDate > today)
                    return Error.Validation("Promotion dates cannot be in the future.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ResearchInterests) &&
            !string.Equals(currentEmployment.StaffCategory, StaffCategories.SeniorMember, StringComparison.OrdinalIgnoreCase))
            return Error.Validation("Research interests can be recorded only for senior members.");

        return null;
    }

    internal static async Task ApplyUpdateAsync(
        Guid employeeId,
        UpdateEmployeeSelfWorkRequest request,
        SpmeDbContext db,
        CancellationToken cancellationToken)
    {
        if (request.GradePromotions is { Count: > 0 })
        {
            var existing = await db.EmployeeGradePromotionDates
                .Where(item => item.EmployeeId == employeeId)
                .ToDictionaryAsync(item => item.GradeId, cancellationToken);

            foreach (var item in request.GradePromotions)
            {
                if (item.PromotionDate is null)
                {
                    if (existing.TryGetValue(item.GradeId, out var stored))
                        db.EmployeeGradePromotionDates.Remove(stored);
                    continue;
                }

                var promotionDate = item.PromotionDate.Value.Date;
                if (existing.TryGetValue(item.GradeId, out var storedDate))
                    storedDate.Update(promotionDate);
                else
                    db.EmployeeGradePromotionDates.Add(new EmployeeGradePromotionDate(employeeId, item.GradeId, promotionDate));
            }
        }

        var currentEmployment = await db.EmploymentRecords
            .SingleOrDefaultAsync(record => record.EmployeeId == employeeId && record.IsCurrent, cancellationToken);
        if (currentEmployment is null)
            return;

        if (request.GradePromotions is { Count: > 0 } && currentEmployment.GradeId is Guid currentGradeId)
        {
            var currentDate = request.GradePromotions
                .SingleOrDefault(item => item.GradeId == currentGradeId)?.PromotionDate?.Date;
            currentEmployment.UpdateSelfPromotionDate(currentDate);
        }

        var researchInterests = string.Equals(
            currentEmployment.StaffCategory,
            StaffCategories.SeniorMember,
            StringComparison.OrdinalIgnoreCase)
            ? request.ResearchInterests ?? currentEmployment.ResearchInterests
            : currentEmployment.ResearchInterests;
        currentEmployment.UpdateSelfWorkDetails(
            request.Location ?? currentEmployment.Location,
            request.AreaOfSpecialization ?? currentEmployment.AreaOfSpecialization,
            researchInterests);

        await db.SaveChangesAsync(cancellationToken);
    }

    internal static decimal YearsInCurrentGrade(
        Guid? currentGradeId,
        IReadOnlyDictionary<Guid, DateTime> selfReported,
        IReadOnlyCollection<EmploymentRecord> employmentRecords,
        DateTime? employmentPromotionDate,
        DateTime? appointmentDate,
        DateTime employmentEffectiveFrom,
        DateTimeOffset now)
    {
        var tenureStart = ResolveTenureStart(
            currentGradeId,
            selfReported,
            employmentRecords,
            employmentPromotionDate,
            appointmentDate,
            employmentEffectiveFrom);
        if (tenureStart is null)
            return 0m;

        var elapsed = now - new DateTimeOffset(DateTime.SpecifyKind(tenureStart.Value, DateTimeKind.Utc));
        return Math.Max(0m, (decimal)elapsed.TotalDays / DaysPerPromotionYear);
    }

    internal static DateTime? ResolveTenureStart(
        Guid? currentGradeId,
        IReadOnlyDictionary<Guid, DateTime> selfReported,
        IReadOnlyCollection<EmploymentRecord> employmentRecords,
        DateTime? employmentPromotionDate,
        DateTime? appointmentDate,
        DateTime employmentEffectiveFrom)
    {
        if (currentGradeId is Guid gradeId && selfReported.TryGetValue(gradeId, out var selfDate))
        {
            var recordedSelf = RecordedPromotionDate(selfDate, employmentEffectiveFrom, appointmentDate);
            if (recordedSelf.HasValue)
                return recordedSelf;
        }

        var recordedPromotions = employmentRecords
            .Select(record => RecordedPromotionDate(record.PromotionDate, record.EffectiveFrom, appointmentDate))
            .Where(date => date.HasValue)
            .Select(date => date!.Value)
            .Concat(selfReported.Values
                .Select(date => RecordedPromotionDate(date, employmentEffectiveFrom, appointmentDate))
                .Where(date => date.HasValue)
                .Select(date => date!.Value))
            .ToList();
        if (employmentPromotionDate.HasValue)
            recordedPromotions.Add(employmentPromotionDate.Value.Date);

        if (recordedPromotions.Count > 0)
            return recordedPromotions.Max();

        return appointmentDate?.Date;
    }

    internal static DateTime? RecordedPromotionDate(
        DateTime? promotionDate,
        DateTime effectiveFrom,
        DateTime? appointmentDate)
    {
        if (promotionDate is not { } promotion)
            return null;

        var date = promotion.Date;
        if (date == effectiveFrom.Date && appointmentDate is { } appointment && appointment.Date < date)
            return null;

        return date;
    }

    private static DateTime? ResolvePromotionDate(
        Guid gradeId,
        IReadOnlyDictionary<Guid, DateTime> selfReported,
        IReadOnlyDictionary<Guid, DateTime?> employmentByGrade)
    {
        if (selfReported.TryGetValue(gradeId, out var selfDate))
            return selfDate.Date;
        if (employmentByGrade.TryGetValue(gradeId, out var employmentDate))
            return employmentDate?.Date;
        return null;
    }

    private static async Task<IReadOnlyList<Grade>> LoadGradeLadderAsync(
        SpmeDbContext db,
        string? staffCategory,
        Grade? currentGrade,
        CancellationToken cancellationToken)
    {
        if (currentGrade is null || string.IsNullOrWhiteSpace(staffCategory))
            return [];

        var query = db.Grades.AsNoTracking()
            .Where(grade => grade.IsActive &&
                            grade.IsPromotionGrade &&
                            grade.StaffCategory == staffCategory &&
                            grade.Rank <= currentGrade.Rank);

        if (!string.IsNullOrWhiteSpace(currentGrade.PromotionStream))
            query = query.Where(grade => grade.PromotionStream == currentGrade.PromotionStream);

        return await query
            .OrderBy(grade => grade.Rank)
            .ToListAsync(cancellationToken);
    }
}
