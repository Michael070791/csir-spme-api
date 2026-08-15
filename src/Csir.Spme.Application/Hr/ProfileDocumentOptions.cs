namespace Csir.Spme.Application.Hr;

public sealed class ProfileDocumentOptions
{
    public const string SectionName = "ProfileDocumentOptions";
    public long MaximumFileBytes { get; set; } = 52_428_800;
    public int UploadSessionMinutes { get; set; } = 60;
    public string? DevelopmentScanResult { get; set; }
}
