using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Leave;

public class Holiday : BaseEntity
{
    public string ScopeType { get; private set; } = "csir-wide";
    public Guid? InstituteId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime HolidayDate { get; private set; }
    public bool IsFullDay { get; private set; } = true;
    public bool IsIslamic { get; private set; }
    public string? Notes { get; private set; }

    private Holiday() { }

    public static Result<Holiday> Create(
        string scopeType, Guid? instituteId, string name, DateTime holidayDate,
        bool isFullDay, bool isIslamic, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<Holiday>.Failure(Error.Validation("Holiday name is required."));

        if (scopeType is not ("csir-wide" or "institute"))
            return Result<Holiday>.Failure(Error.Validation("Holiday scope must be csir-wide or institute."));

        if (scopeType == "institute" && !instituteId.HasValue)
            return Result<Holiday>.Failure(Error.Validation("An institute is required for an institute holiday."));

        if (scopeType == "csir-wide")
            instituteId = null;

        return Result<Holiday>.Success(new Holiday
        {
            ScopeType = scopeType,
            InstituteId = instituteId,
            Name = name.Trim(),
            HolidayDate = holidayDate.Date,
            IsFullDay = isFullDay,
            IsIslamic = isIslamic,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        });
    }

    public Result<bool> Update(string name, DateTime holidayDate, bool isFullDay, bool isIslamic, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Holiday name is required."));

        Name = name.Trim();
        HolidayDate = holidayDate.Date;
        IsFullDay = isFullDay;
        IsIslamic = isIslamic;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        return Result.Success();
    }
}
