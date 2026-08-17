using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Hr;

public class EmployeeGradePromotionDate : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public Guid GradeId { get; private set; }
    public DateTime PromotionDate { get; private set; }

    private EmployeeGradePromotionDate() { }

    public EmployeeGradePromotionDate(Guid employeeId, Guid gradeId, DateTime promotionDate)
    {
        EmployeeId = employeeId;
        GradeId = gradeId;
        PromotionDate = promotionDate.Date;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void Update(DateTime promotionDate)
    {
        PromotionDate = promotionDate.Date;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
