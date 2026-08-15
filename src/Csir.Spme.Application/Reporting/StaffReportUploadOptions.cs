namespace Csir.Spme.Application.Reporting;

public sealed class StaffReportUploadOptions
{
    public const string SectionName = "StaffReportUploadOptions";
    public long ConceptNoteMaximumFileBytes { get; set; } = 62_914_560;
    public long ImageMaximumFileBytes { get; set; } = 20_971_520;
    public int MaximumImagesPerReport { get; set; } = 3;
    public int UploadSessionMinutes { get; set; } = 60;
    public string? DevelopmentScanResult { get; set; }
}
