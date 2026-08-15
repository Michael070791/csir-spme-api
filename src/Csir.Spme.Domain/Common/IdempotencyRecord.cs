namespace Csir.Spme.Domain.Common;

public sealed class IdempotencyRecord
{
    public Guid Id { get; private set; }
    public string Scope { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public short ResponseStatus { get; private set; }
    public string? ResponseBody { get; private set; }
    public string? ResponseContentType { get; private set; }
    public string? ResponseEtag { get; private set; }
    public string? ResponseLocation { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    private IdempotencyRecord() { }

    public IdempotencyRecord(string scope, string key, string requestHash, DateTimeOffset expiresAt)
    {
        Id = Guid.NewGuid();
        Scope = scope;
        IdempotencyKey = key;
        RequestHash = requestHash;
        ResponseStatus = -1;
        ExpiresAt = expiresAt;
    }

    public bool IsComplete => ResponseStatus >= 100;

    public void Complete(int status, string? body, string? contentType, string? etag, string? location)
    {
        ResponseStatus = checked((short)status);
        ResponseBody = body;
        ResponseContentType = contentType;
        ResponseEtag = etag;
        ResponseLocation = location;
    }
}
