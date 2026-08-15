using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Comms;

public class MemoAudience : BaseEntity
{
    public Guid MemoId { get; private set; }
    public string AudienceType { get; private set; } = "all-employees";
    public Guid? InstituteId { get; private set; }
    public Guid? DivisionId { get; private set; }
    public Guid? SectionId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public string? RoleCode { get; private set; }

    private MemoAudience() { }

    public MemoAudience(Guid memoId, string audienceType, Guid? instituteId = null,
        Guid? divisionId = null, Guid? sectionId = null, Guid? employeeId = null, string? roleCode = null)
    {
        MemoId = memoId;
        AudienceType = audienceType;
        InstituteId = instituteId;
        DivisionId = divisionId;
        SectionId = sectionId;
        EmployeeId = employeeId;
        RoleCode = roleCode;
    }
}
