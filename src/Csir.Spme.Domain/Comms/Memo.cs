using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Comms;

public class Memo : InstituteScopedEntity
{
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? AudienceJson { get; private set; }
    public string Status { get; private set; } = "draft";
    public DateTimeOffset? PublishedAt { get; private set; }
    public string? PublishedByUserId { get; private set; }

    private Memo() { }

    public static Result<Memo> Create(Guid instituteId, string title, string body)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            return Result<Memo>.Failure(Error.Validation("Memo title and body are required."));

        return Result<Memo>.Success(new Memo
        {
            InstituteId = instituteId,
            Title = title.Trim(),
            Body = body.Trim(),
            Status = "draft"
        });
    }

    public Result<bool> UpdateDraft(string title, string body)
    {
        if (Status != "draft")
            return Result.Failure(Error.StateTransition("Only draft memos can be edited."));
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            return Result.Failure(Error.Validation("Memo title and body are required."));

        Title = title.Trim();
        Body = body.Trim();
        return Result.Success();
    }

    public Result<bool> Publish(Guid userId, DateTimeOffset publishedAt)
    {
        if (Status != "draft")
            return Result.Failure(Error.StateTransition("Only draft memos can be published."));

        Status = "published";
        PublishedAt = publishedAt;
        PublishedByUserId = userId.ToString();
        return Result.Success();
    }

    public Result<bool> Withdraw()
    {
        if (Status != "published")
            return Result.Failure(Error.StateTransition("Only published memos can be withdrawn."));

        Status = "withdrawn";
        return Result.Success();
    }

    public void RestorePublished(Guid? userId, DateTimeOffset? publishedAt)
    {
        Status = "published";
        PublishedAt = publishedAt;
        PublishedByUserId = userId?.ToString();
    }
}
