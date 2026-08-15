namespace Csir.Spme.Application.Promotions;

public sealed class PromotionUploadOptions
{
    public const string SectionName = "PromotionUploadOptions";
    public long MaximumFileBytes { get; set; } = 209_715_200;
    public int UploadSessionMinutes { get; set; } = 60;
    public string? DevelopmentScanResult { get; set; }
}
