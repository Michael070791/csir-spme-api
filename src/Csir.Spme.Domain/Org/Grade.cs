using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Org;

public class Grade : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? StaffCategory { get; private set; }
    public string? PromotionStream { get; private set; }
    public short? PromotionLevel { get; private set; }
    public short Rank { get; private set; }
    public bool IsPromotionGrade { get; private set; }
    public bool IsActive { get; private set; }

    private Grade() { }

    public static Grade Create(
        string code,
        string name,
        string staffCategory,
        string promotionStream,
        short promotionLevel,
        short rank,
        bool isPromotionGrade = true)
    {
        return new Grade
        {
            Code = code.Trim(),
            Name = name.Trim(),
            StaffCategory = staffCategory.Trim(),
            PromotionStream = promotionStream.Trim(),
            PromotionLevel = promotionLevel,
            Rank = rank,
            IsPromotionGrade = isPromotionGrade,
            IsActive = true
        };
    }

    public void Deactivate() => IsActive = false;
}
