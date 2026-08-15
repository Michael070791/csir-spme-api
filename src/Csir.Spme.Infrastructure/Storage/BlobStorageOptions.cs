namespace Csir.Spme.Infrastructure.Storage;

public sealed class BlobStorageOptions
{
    public const string SectionName = "Storage";

    public string ContainerName { get; set; } = "spme-private";

    public Uri? ServiceUri { get; set; }
    public Uri? ExternalServiceUri { get; set; }
    public TimeSpan ReadUrlLifetime { get; set; } = TimeSpan.FromMinutes(5);
    public bool CreateContainer { get; set; }
    public string? ManagedIdentityClientId { get; set; }
}
