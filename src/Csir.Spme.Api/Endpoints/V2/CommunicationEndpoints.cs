using System.Security.Claims;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Realtime;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Comms;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class CommunicationEndpoints
{
    private const int PreviewRecipientLimit = 25;

    public static void MapCommunicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var memos = endpoints.MapGroup("/api/v2/memos")
            .WithGroupName("v2")
            .WithTags("Memos")
            .WithDescription("Institute-scoped internal memoranda with controlled drafting, publication, withdrawal, audience targeting, employee acknowledgement, and notification delivery. Platform administrators may address one or more institutes or named employees. Institute HR administrators may address their whole institute, organization groups, or selected people. Resources outside the caller's authorized institute or audience are represented as not found.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        memos.MapGet("", ListMemosAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadMemos)
            .WithName("Memos_List")
            .WithSummary("List accessible memoranda.")
            .WithDescription("Returns memoranda visible in the caller's institute scope. HR and platform administrators can review owned drafts plus published memoranda that target their institute, and may filter by lifecycle status. Platform administrators and unscoped HR administrators can optionally filter by institute. Employees receive only published memoranda whose configured audiences match their employee, organization, or role attributes.")
            .Produces<CollectionResponse<MemoResponse>>(StatusCodes.Status200OK);
        memos.MapPost("/preview", PreviewMemoAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageMemos)
            .WithName("Memos_Preview")
            .WithSummary("Preview a memorandum and its delivery audience.")
            .WithDescription("Validates title, body, and audience rules without saving a draft, then returns the SMS synopsis and the employees who would receive in-app, email, and SMS delivery if the memorandum were published. Named employees take precedence over institute filters; when no employee is selected, the selected institutes or caller's institute become the audience.")
            .Produces<MemoPreviewResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        memos.MapGet("/{id:guid}", GetMemoAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadMemos)
            .WithName("Memos_Get")
            .WithSummary("Get an accessible memorandum.")
            .WithDescription("Returns a memorandum, its SMS synopsis, and its audience definitions when it is within the caller's institute and visibility scope. A memo that does not exist, belongs to another institute, is unpublished for an employee, or does not match the employee's audience is returned as not found.")
            .Produces<MemoResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        memos.MapPost("", CreateMemoAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageMemos)
            .WithName("Memos_Create")
            .WithSummary("Create a draft memorandum.")
            .WithDescription("Creates a memorandum in the draft state. Institute HR and institute administrators default to every active employee in their institute and may instead name organization groups or selected people. Named employees take precedence over institute filters, and supplied audiences are validated against the caller's share scope before the draft is stored.")
            .Produces<MemoResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        memos.MapPatch("/{id:guid}", UpdateMemoAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageMemos)
            .WithName("Memos_Update")
            .WithSummary("Update a draft memorandum.")
            .WithDescription("Replaces the title, body, and audience definitions of an owned draft memorandum. Published and withdrawn memoranda cannot be edited, and every replacement audience is validated within the caller's share scope before the draft is saved.")
            .Produces<MemoResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        memos.MapDelete("/{id:guid}", DeleteMemoAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageMemos)
            .WithName("Memos_Delete")
            .WithSummary("Delete a draft memorandum.")
            .WithDescription("Permanently deletes an owned memorandum only while it remains in the draft state. Published or withdrawn memoranda are retained to preserve the communication record and must not be deleted through this operation.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        memos.MapPost("/{id:guid}/publish", PublishMemoAsync)
            .RequireAuthorization(AuthorizationPolicies.PublishMemos)
            .WithName("Memos_Publish")
            .WithSummary("Publish a draft memorandum.")
            .WithDescription("Publishes an owned draft memorandum, records the publishing user and timestamp, and delivers one in-app notification, one email, and one SMS synopsis to each matching active employee according to available contact details and announcement preferences. Repeating the operation after publication or attempting it from any non-draft lifecycle state is rejected.")
            .Produces<MemoResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        memos.MapPost("/{id:guid}/withdraw", WithdrawMemoAsync)
            .RequireAuthorization(AuthorizationPolicies.PublishMemos)
            .WithName("Memos_Withdraw")
            .WithSummary("Withdraw a published memorandum.")
            .WithDescription("Withdraws an owned published memorandum so it is no longer available to standard employee readers. The communication record remains available to authorized administrators, and only the domain-supported lifecycle transition is accepted.")
            .Produces<MemoResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        memos.MapPost("/{id:guid}/acknowledgements", AcknowledgeMemoAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadMemos)
            .WithName("Memos_Acknowledge")
            .WithSummary("Acknowledge a published memorandum.")
            .WithDescription("Records the authenticated employee's acknowledgement of an accessible published memorandum. The operation is idempotent: repeating it does not create duplicate acknowledgement records, and users without an employee identity cannot acknowledge memoranda.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var notifications = endpoints.MapGroup("/api/v2/notifications")
            .WithGroupName("v2")
            .WithTags("Notifications")
            .WithDescription("Authenticated users' private in-application notification inbox. Notifications are recipient-owned records; callers can list and update only their own delivery state, while message generation is performed by approved business workflows such as memo publication.")
            .RequireAuthorization(AuthorizationPolicies.ReadNotifications)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        notifications.MapGet("", GetNotificationsAsync)
            .WithName("Notifications_ListMine")
            .WithSummary("List the authenticated user's notifications.")
            .WithDescription("Returns up to the 100 most recent notifications addressed to the authenticated user, ordered newest first. Set unreadOnly to true to retrieve only unread notifications; no user, employee, or institute identifier can be supplied to access another recipient's inbox.")
            .Produces<PageResponse<NotificationResponse>>(StatusCodes.Status200OK);
        notifications.MapGet("/{id:guid}", GetNotificationAsync)
            .WithName("Notifications_GetMine")
            .WithSummary("Get one notification from the authenticated user's inbox.")
            .WithDescription("Returns a notification only when it belongs to the authenticated user. Missing and out-of-scope notification identifiers receive the same non-disclosing not-found response.")
            .Produces<NotificationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        notifications.MapPost("/{id:guid}/read", MarkNotificationReadAsync)
            .WithName("Notifications_MarkRead")
            .WithSummary("Mark one notification as read.")
            .WithDescription("Marks the identified notification as read only when it belongs to the authenticated user. Requests for another user's notification use a non-disclosing not-found response, and repeated calls preserve the existing read state and timestamp.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
        notifications.MapPost("/read-all", MarkAllNotificationsReadAsync)
            .WithName("Notifications_MarkAllRead")
            .WithSummary("Mark all personal notifications as read.")
            .WithDescription("Marks every unread notification in the authenticated user's private inbox as read. The operation is idempotent and returns no content when the inbox is already fully read; it never changes notifications addressed to other users.")
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> ListMemosAsync(
        HttpContext context,
        SpmeDbContext db,
        string? status,
        string? instituteId,
        CancellationToken ct)
    {
        var callerInstituteId = CurrentInstituteId(context);
        if (!callerInstituteId.HasValue && !CanShareAcrossInstitutes(context))
            return EndpointProblems.FromError(Error.Forbidden("An institute scope is required."));

        Guid? requestedInstituteId = null;
        if (!string.IsNullOrWhiteSpace(instituteId))
        {
            if (!Guid.TryParse(instituteId, out var parsedInstituteId))
                return EndpointProblems.FromError(Error.Validation("Institute filter must identify an active institute."));
            if (!CanAccessInstitute(context, parsedInstituteId))
                return EndpointProblems.FromError(Error.Forbidden("Memo audience is outside your institute scope."));
            requestedInstituteId = parsedInstituteId;
        }

        var query = db.Memos.AsNoTracking();
        if (!IsHrViewer(context))
        {
            query = query.Where(memo => memo.Status == MemoStatuses.Published);
        }
        else if (!string.IsNullOrWhiteSpace(status))
        {
            if (!DomainValues.Contains(MemoStatuses.All, status))
                return EndpointProblems.FromError(Error.Validation("Unsupported memo status."));
            query = query.Where(memo => memo.Status == status);
        }

        if (callerInstituteId.HasValue && !CanShareAcrossInstitutes(context))
        {
            var scopedInstituteId = callerInstituteId.Value;
            query = query.Where(memo =>
                memo.InstituteId == scopedInstituteId ||
                (memo.Status == MemoStatuses.Published &&
                 db.MemoAudiences.Any(audience =>
                     audience.MemoId == memo.Id && audience.InstituteId == scopedInstituteId)));
        }
        else if (requestedInstituteId.HasValue)
        {
            var filterInstituteId = requestedInstituteId.Value;
            query = query.Where(memo =>
                memo.InstituteId == filterInstituteId ||
                db.MemoAudiences.Any(audience =>
                    audience.MemoId == memo.Id && audience.InstituteId == filterInstituteId));
        }

        var memos = db.Database.IsSqlite()
            ? (await query.ToListAsync(ct)).OrderByDescending(memo => memo.CreatedAt).ToList()
            : await query.OrderByDescending(memo => memo.CreatedAt).ToListAsync(ct);
        if (!IsHrViewer(context))
        {
            var reader = await GetMemoReaderAsync(context, db, ct);
            var memoIds = memos.Select(memo => memo.Id).ToArray();
            var audiences = await db.MemoAudiences.AsNoTracking()
                .Where(audience => memoIds.Contains(audience.MemoId))
                .ToListAsync(ct);
            memos = reader is null
                ? []
                : memos.Where(memo => MemoAudienceMatcher.Matches(
                    audiences.Where(audience => audience.MemoId == memo.Id),
                    reader.Employee.Id,
                    reader.Employee.InstituteId,
                    reader.Employment?.DivisionId,
                    reader.Employment?.SectionId,
                    reader.RoleNames)).ToList();
        }

        return TypedResults.Ok(new CollectionResponse<MemoResponse>(MapMemos(memos, await LoadAudiencesAsync(memos, db, ct)), memos.Count));
    }

    private static async Task<IResult> GetMemoAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var memo = await db.Memos.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id, ct);
        if (memo is null || !await CanReadMemoAsync(context, memo, db, ct))
            return EndpointProblems.FromError(Error.NotFound("Memo not found."));
        return TypedResults.Ok(MapMemos([memo], await LoadAudiencesAsync([memo], db, ct)).Single());
    }

    private static async Task<IResult> PreviewMemoAsync(
        CreateMemoRequest request,
        HttpContext context,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var prepared = await PrepareDraftAsync(request.Title, request.Body, request.Audiences, context, db, ct);
        if (prepared.IsFailure)
            return EndpointProblems.FromError(prepared.Error!);

        var recipients = await ResolveRecipientsAsync(prepared.Value!.Audiences, db, ct);
        return TypedResults.Ok(MapPreview(request.Title.Trim(), request.Body.Trim(), prepared.Value.Audiences, recipients));
    }

    private static async Task<IResult> CreateMemoAsync(
        CreateMemoRequest request,
        HttpContext context,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var prepared = await PrepareDraftAsync(request.Title, request.Body, request.Audiences, context, db, ct);
        if (prepared.IsFailure)
            return EndpointProblems.FromError(prepared.Error!);

        var draft = prepared.Value!;
        db.Memos.Add(draft.Memo);
        db.MemoAudiences.AddRange(draft.Audiences);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v2/memos/{draft.Memo.Id}", MapMemos([draft.Memo], draft.Audiences).Single());
    }

    private static async Task<IResult> UpdateMemoAsync(
        Guid id,
        UpdateMemoRequest request,
        HttpContext context,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var memo = await db.Memos.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);
        if (memo is null || !CanManageMemo(context, memo))
            return EndpointProblems.FromError(Error.NotFound("Memo not found."));
        var updated = memo.UpdateDraft(request.Title, request.Body);
        if (updated.IsFailure)
            return EndpointProblems.FromError(updated.Error!);

        var audiences = await BuildAudiencesAsync(memo.Id, memo.InstituteId, request.Audiences, context, db, ct);
        if (audiences.IsFailure)
            return EndpointProblems.FromError(audiences.Error!);

        db.MemoAudiences.RemoveRange(db.MemoAudiences.Where(audience => audience.MemoId == memo.Id));
        db.MemoAudiences.AddRange(audiences.Value!);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(MapMemos([memo], audiences.Value!).Single());
    }

    private static async Task<IResult> DeleteMemoAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var memo = await db.Memos.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);
        if (memo is null || !CanManageMemo(context, memo))
            return EndpointProblems.FromError(Error.NotFound("Memo not found."));
        if (memo.Status != MemoStatuses.Draft)
            return EndpointProblems.FromError(Error.StateTransition("Only draft memos can be deleted."));
        db.Memos.Remove(memo);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> PublishMemoAsync(
        Guid id,
        HttpContext context,
        SpmeDbContext db,
        IHubContext<HrRealtimeHub> realtime,
        IWorkflowNotificationOutbox notifications,
        CancellationToken ct)
    {
        var memo = await db.Memos.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);
        if (memo is null || !CanManageMemo(context, memo))
            return EndpointProblems.FromError(Error.NotFound("Memo not found."));
        var userId = CurrentUserId(context);
        if (!userId.HasValue)
            return EndpointProblems.Unauthorized();
        var published = memo.Publish(userId.Value, DateTimeOffset.UtcNow);
        if (published.IsFailure)
            return EndpointProblems.FromError(published.Error!);

        var audiences = await db.MemoAudiences.AsNoTracking()
            .Where(audience => audience.MemoId == memo.Id)
            .ToListAsync(ct);
        var recipients = await ResolveRecipientsAsync(audiences, db, ct);
        var createdNotifications = await FanOutInAppNotificationsAsync(memo, recipients, db, ct);
        await notifications.StageMemoPublishedAsync(
            memo.Id,
            memo.InstituteId,
            memo.Title,
            MemoAudienceMatcher.EmailBody(memo.Body),
            MemoAudienceMatcher.SmsSynopsis(memo.Title, memo.Body),
            recipients.Select(recipient => new MemoChannelRecipient(
                recipient.User?.Id,
                recipient.EmailAddress,
                recipient.Phone,
                recipient.SendEmail,
                recipient.SendSms)).ToList(),
            ct);
        await db.SaveChangesAsync(ct);

        var instituteIds = audiences
            .Select(audience => audience.InstituteId)
            .Where(institute => institute.HasValue)
            .Select(institute => institute!.Value)
            .Append(memo.InstituteId)
            .Distinct();
        foreach (var institute in instituteIds)
        {
            await realtime.Clients.Group(HrRealtimeGroups.Institute(institute.ToString()))
                .SendAsync("resourceChanged",
                    new ResourceChangedMessage("memos", "published", memo.Id.ToString(), institute, DateTimeOffset.UtcNow),
                    ct);
        }

        foreach (var notification in createdNotifications)
        {
            await realtime.Clients.Group(HrRealtimeGroups.User(notification.RecipientUserId.ToString()))
                .SendAsync("notificationCreated",
                    new NotificationResponse(notification.Id, notification.Title, notification.Body,
                        notification.ActionLink, notification.IsRead, notification.ReadAt, notification.CreatedAt),
                    ct);
        }

        return TypedResults.Ok(MapMemos([memo], audiences).Single());
    }

    private static async Task<IResult> WithdrawMemoAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var memo = await db.Memos.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);
        if (memo is null || !CanManageMemo(context, memo))
            return EndpointProblems.FromError(Error.NotFound("Memo not found."));
        var withdrawn = memo.Withdraw();
        if (withdrawn.IsFailure)
            return EndpointProblems.FromError(withdrawn.Error!);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(MapMemos([memo], await LoadAudiencesAsync([memo], db, ct)).Single());
    }

    private static async Task<IResult> AcknowledgeMemoAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var employeeId = CurrentEmployeeId(context);
        if (!employeeId.HasValue)
            return EndpointProblems.FromError(Error.Forbidden("An employee identity is required."));
        var memo = await db.Memos.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.Status == MemoStatuses.Published, ct);
        if (memo is null || !await CanReadMemoAsync(context, memo, db, ct))
            return EndpointProblems.FromError(Error.NotFound("Memo not found."));
        if (!await db.MemoAcknowledgements.AnyAsync(item => item.MemoId == id && item.EmployeeId == employeeId.Value, ct))
        {
            db.MemoAcknowledgements.Add(new MemoAcknowledgement(id, employeeId.Value));
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetNotificationsAsync(HttpContext context, SpmeDbContext db, bool? unreadOnly, CancellationToken ct)
    {
        var userId = CurrentUserId(context);
        if (!userId.HasValue)
            return EndpointProblems.Unauthorized();
        var query = db.Notifications.AsNoTracking().Where(item => item.RecipientUserId == userId.Value);
        if (unreadOnly == true)
            query = query.Where(item => !item.IsRead);
        var rows = db.Database.IsSqlite()
            ? (await query.ToListAsync(ct)).OrderByDescending(item => item.CreatedAt).Take(100).ToList()
            : await query.OrderByDescending(item => item.CreatedAt).Take(100).ToListAsync(ct);
        var items = rows
            .Select(item => new NotificationResponse(item.Id, item.Title, item.Body, item.ActionLink, item.IsRead, item.ReadAt, item.CreatedAt))
            .ToList();
        return TypedResults.Ok(new PageResponse<NotificationResponse>(items, items.Count, 1, 100));
    }

    private static async Task<IResult> GetNotificationAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var userId = CurrentUserId(context);
        var notification = userId.HasValue
            ? await db.Notifications.AsNoTracking()
                .Where(item => item.Id == id && item.RecipientUserId == userId.Value)
                .Select(item => new NotificationResponse(item.Id, item.Title, item.Body, item.ActionLink, item.IsRead, item.ReadAt, item.CreatedAt))
                .FirstOrDefaultAsync(ct)
            : null;
        return notification is null
            ? EndpointProblems.FromError(Error.NotFound("Notification not found."))
            : TypedResults.Ok(notification);
    }

    private static async Task<IResult> MarkNotificationReadAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var userId = CurrentUserId(context);
        var notification = userId.HasValue
            ? await db.Notifications.FirstOrDefaultAsync(item => item.Id == id && item.RecipientUserId == userId.Value, ct)
            : null;
        if (notification is null)
            return EndpointProblems.FromError(Error.NotFound("Notification not found."));
        notification.MarkRead(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> MarkAllNotificationsReadAsync(HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var userId = CurrentUserId(context);
        if (!userId.HasValue)
            return EndpointProblems.Unauthorized();
        var notifications = await db.Notifications
            .Where(item => item.RecipientUserId == userId.Value && !item.IsRead)
            .ToListAsync(ct);
        foreach (var notification in notifications)
            notification.MarkRead(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Result<PreparedMemoDraft>> PrepareDraftAsync(
        string title,
        string body,
        IReadOnlyList<MemoAudienceInput>? inputs,
        HttpContext context,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var owningInstitute = await ResolveOwningInstituteAsync(inputs, context, db, ct);
        if (owningInstitute.IsFailure)
            return Result<PreparedMemoDraft>.Failure(owningInstitute.Error!);

        var created = Memo.Create(owningInstitute.Value, title, body);
        if (created.IsFailure)
            return Result<PreparedMemoDraft>.Failure(created.Error!);

        var audiences = await BuildAudiencesAsync(created.Value!.Id, owningInstitute.Value, inputs, context, db, ct);
        if (audiences.IsFailure)
            return Result<PreparedMemoDraft>.Failure(audiences.Error!);

        return Result<PreparedMemoDraft>.Success(new PreparedMemoDraft(created.Value, audiences.Value!));
    }

    private static async Task<Result<Guid>> ResolveOwningInstituteAsync(
        IReadOnlyList<MemoAudienceInput>? inputs,
        HttpContext context,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var callerInstituteId = CurrentInstituteId(context);
        if (callerInstituteId.HasValue && !CanShareAcrossInstitutes(context))
            return Result<Guid>.Success(callerInstituteId.Value);

        var requested = inputs is { Count: > 0 } ? inputs : [];
        foreach (var input in requested)
        {
            if (input.InstituteId.HasValue && CanAccessInstitute(context, input.InstituteId.Value))
                return Result<Guid>.Success(input.InstituteId.Value);
            if (input.EmployeeId.HasValue)
            {
                var employee = await db.Employees.AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.Id == input.EmployeeId && candidate.ProfileStatus == "active", ct);
                if (employee is not null && CanAccessInstitute(context, employee.InstituteId))
                    return Result<Guid>.Success(employee.InstituteId);
            }

            if (input.DivisionId.HasValue)
            {
                var division = await db.Divisions.AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.Id == input.DivisionId && candidate.IsActive, ct);
                if (division is not null && CanAccessInstitute(context, division.InstituteId))
                    return Result<Guid>.Success(division.InstituteId);
            }
        }

        return Result<Guid>.Failure(Error.Validation(
            "Select at least one institute or one or more employees before creating a memorandum."));
    }

    private static async Task<Result<List<MemoAudience>>> BuildAudiencesAsync(
        Guid memoId,
        Guid memoInstituteId,
        IReadOnlyList<MemoAudienceInput>? inputs,
        HttpContext context,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var supplied = inputs is { Count: > 0 } ? inputs : [];
        var selectedEmployees = supplied
            .Where(input => input.AudienceType == MemoAudienceTypes.Employee && input.EmployeeId.HasValue)
            .GroupBy(input => input.EmployeeId!.Value)
            .Select(group => group.First())
            .ToList();
        IReadOnlyList<MemoAudienceInput> requested = selectedEmployees.Count > 0
            ? selectedEmployees
            : supplied.Count > 0
                ? supplied
            : CanShareAcrossInstitutes(context)
                ? []
                : [new MemoAudienceInput(MemoAudienceTypes.AllEmployees, memoInstituteId, null, null, null, null)];

        if (requested.Count == 0)
            return Result<List<MemoAudience>>.Failure(Error.Validation(
                "Select at least one institute or one or more employees before creating a memorandum."));

        var result = new List<MemoAudience>();
        foreach (var input in requested)
        {
            if (!MemoAudienceTypes.All.Contains(input.AudienceType, StringComparer.Ordinal))
                return Result<List<MemoAudience>>.Failure(Error.Validation("Unsupported memo audience type."));

            if (input.AudienceType is MemoAudienceTypes.AllEmployees or MemoAudienceTypes.Institute)
            {
                var instituteId = input.InstituteId ?? memoInstituteId;
                if (!CanAccessInstitute(context, instituteId))
                    return Result<List<MemoAudience>>.Failure(Error.Forbidden("Memo audience is outside your institute scope."));
                if (!CanShareAcrossInstitutes(context) && instituteId != memoInstituteId)
                    return Result<List<MemoAudience>>.Failure(Error.Forbidden("Memo audience is outside your institute scope."));
                result.Add(new MemoAudience(memoId, input.AudienceType, instituteId));
                continue;
            }

            if (input.AudienceType == MemoAudienceTypes.Division)
            {
                var division = await db.Divisions.AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.Id == input.DivisionId && candidate.IsActive, ct);
                if (division is null || !CanAccessInstitute(context, division.InstituteId))
                    return Result<List<MemoAudience>>.Failure(Error.NotFound("Memo division audience not found."));
                if (!CanShareAcrossInstitutes(context) && division.InstituteId != memoInstituteId)
                    return Result<List<MemoAudience>>.Failure(Error.NotFound("Memo division audience not found."));
                result.Add(new MemoAudience(memoId, input.AudienceType, division.InstituteId, division.Id));
                continue;
            }

            if (input.AudienceType == MemoAudienceTypes.Section)
            {
                var section = await db.Sections.AsNoTracking()
                    .Join(db.Divisions, section => section.DivisionId, division => division.Id, (section, division) => new { section, division })
                    .FirstOrDefaultAsync(candidate =>
                        candidate.section.Id == input.SectionId && candidate.section.IsActive && candidate.division.IsActive, ct);
                if (section is null || !CanAccessInstitute(context, section.division.InstituteId))
                    return Result<List<MemoAudience>>.Failure(Error.NotFound("Memo section audience not found."));
                if (!CanShareAcrossInstitutes(context) && section.division.InstituteId != memoInstituteId)
                    return Result<List<MemoAudience>>.Failure(Error.NotFound("Memo section audience not found."));
                result.Add(new MemoAudience(
                    memoId, input.AudienceType, section.division.InstituteId, section.section.DivisionId, section.section.Id));
                continue;
            }

            if (input.AudienceType == MemoAudienceTypes.Employee)
            {
                var employee = await db.Employees.AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.Id == input.EmployeeId && candidate.ProfileStatus == "active", ct);
                if (employee is null || !CanAccessInstitute(context, employee.InstituteId))
                    return Result<List<MemoAudience>>.Failure(Error.NotFound("Memo employee audience not found."));
                if (!CanShareAcrossInstitutes(context) && employee.InstituteId != memoInstituteId)
                    return Result<List<MemoAudience>>.Failure(Error.NotFound("Memo employee audience not found."));
                result.Add(new MemoAudience(memoId, input.AudienceType, employee.InstituteId, employeeId: employee.Id));
                continue;
            }

            if (string.IsNullOrWhiteSpace(input.RoleCode))
                return Result<List<MemoAudience>>.Failure(Error.Validation("Role code is required for a role audience."));
            var roleInstituteId = input.InstituteId ?? memoInstituteId;
            if (!CanAccessInstitute(context, roleInstituteId) ||
                (!CanShareAcrossInstitutes(context) && roleInstituteId != memoInstituteId))
            {
                return Result<List<MemoAudience>>.Failure(Error.Forbidden("Memo audience is outside your institute scope."));
            }

            result.Add(new MemoAudience(memoId, input.AudienceType, roleInstituteId, roleCode: input.RoleCode.Trim()));
        }

        return Result<List<MemoAudience>>.Success(result);
    }

    private static async Task<List<MemoDeliveryRecipient>> ResolveRecipientsAsync(
        IReadOnlyList<MemoAudience> audiences,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var instituteIds = audiences
            .Select(audience => audience.InstituteId)
            .Where(instituteId => instituteId.HasValue)
            .Select(instituteId => instituteId!.Value)
            .Distinct()
            .ToArray();
        var namedEmployeeIds = audiences
            .Where(audience => audience.AudienceType == MemoAudienceTypes.Employee && audience.EmployeeId.HasValue)
            .Select(audience => audience.EmployeeId!.Value)
            .Distinct()
            .ToArray();

        var employees = await db.Employees.AsNoTracking()
            .Where(employee =>
                employee.ProfileStatus == "active" &&
                (instituteIds.Contains(employee.InstituteId) || namedEmployeeIds.Contains(employee.Id)))
            .ToListAsync(ct);
        var employeeIds = employees.Select(employee => employee.Id).ToArray();
        var employment = await db.EmploymentRecords.AsNoTracking()
            .Where(record => employeeIds.Contains(record.EmployeeId) && record.IsCurrent)
            .ToListAsync(ct);
        var users = await db.Users
            .Where(user => user.EmployeeId.HasValue &&
                           employeeIds.Contains(user.EmployeeId.Value) &&
                           user.AccountStatus == "active")
            .ToListAsync(ct);
        var userIds = users.Select(user => user.Id).ToArray();
        var roleAssignments = await (
            from userRole in db.UserRoles
            join role in db.Roles on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, RoleName = role.Name! }).ToListAsync(ct);
        var preferences = await db.NotificationPreferences
            .Where(preference => userIds.Contains(preference.UserId))
            .ToListAsync(ct);
        var allInstituteIds = instituteIds
            .Concat(employees.Select(employee => employee.InstituteId))
            .Distinct()
            .ToArray();
        var institutes = await db.Institutes.AsNoTracking()
            .Where(institute => allInstituteIds.Contains(institute.Id))
            .ToDictionaryAsync(institute => institute.Id, ct);

        var recipients = new List<MemoDeliveryRecipient>();
        foreach (var employee in employees)
        {
            var record = employment.FirstOrDefault(item => item.EmployeeId == employee.Id);
            var user = users.FirstOrDefault(item => item.EmployeeId == employee.Id);
            var roleNames = user is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : roleAssignments.Where(item => item.UserId == user.Id)
                    .Select(item => item.RoleName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!MemoAudienceMatcher.Matches(
                    audiences,
                    employee.Id,
                    employee.InstituteId,
                    record?.DivisionId,
                    record?.SectionId,
                    roleNames))
            {
                continue;
            }

            var preference = user is null ? null : preferences.FirstOrDefault(item => item.UserId == user.Id);
            var email = FirstContact(user?.Email, employee.PrimaryEmail);
            var phone = FirstContact(user?.PhoneNumber, employee.Phone);
            recipients.Add(new MemoDeliveryRecipient(
                employee,
                user,
                institutes.GetValueOrDefault(employee.InstituteId)?.Name ?? "Institute",
                email,
                phone,
                user is not null,
                (preference is null || preference.SystemAnnouncements) && !string.IsNullOrWhiteSpace(email),
                !string.IsNullOrWhiteSpace(phone)));
        }

        return recipients;
    }

    private static async Task<List<Notification>> FanOutInAppNotificationsAsync(
        Memo memo,
        IReadOnlyList<MemoDeliveryRecipient> recipients,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var created = new List<Notification>();
        var actionLink = $"/memos/{memo.Id}";
        var body = MemoAudienceMatcher.InAppBody(memo.Body);
        foreach (var recipient in recipients.Where(item => item.InApp && item.User is not null))
        {
            var userId = recipient.User!.Id;
            if (await db.Notifications.AnyAsync(item => item.RecipientUserId == userId && item.ActionLink == actionLink, ct) ||
                db.Notifications.Local.Any(item => item.RecipientUserId == userId && item.ActionLink == actionLink))
            {
                continue;
            }

            var notification = new Notification(userId, memo.Title, body, actionLink);
            db.Notifications.Add(notification);
            created.Add(notification);
        }

        return created;
    }

    private static MemoPreviewResponse MapPreview(
        string title,
        string body,
        IReadOnlyList<MemoAudience> audiences,
        IReadOnlyList<MemoDeliveryRecipient> recipients) =>
        new(
            title,
            body,
            MemoAudienceMatcher.SmsSynopsis(title, body),
            audiences.Select(MapAudience).ToList(),
            recipients.Count,
            recipients.Count(item => item.InApp),
            recipients.Count(item => item.SendEmail),
            recipients.Count(item => item.SendSms),
            recipients.Take(PreviewRecipientLimit).Select(item => new MemoPreviewRecipientResponse(
                item.Employee.Id,
                item.Employee.StaffId,
                DisplayName(item.Employee),
                item.InstituteName,
                item.InApp,
                item.SendEmail,
                item.SendSms)).ToList());

    private static async Task<List<MemoAudience>> LoadAudiencesAsync(
        IReadOnlyList<Memo> memos,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var ids = memos.Select(memo => memo.Id).ToArray();
        return await db.MemoAudiences.AsNoTracking()
            .Where(audience => ids.Contains(audience.MemoId))
            .ToListAsync(ct);
    }

    private static List<MemoResponse> MapMemos(IReadOnlyList<Memo> memos, IReadOnlyList<MemoAudience> audiences) =>
        memos.Select(memo => new MemoResponse(
            memo.Id,
            memo.InstituteId,
            memo.Title,
            memo.Body,
            MemoAudienceMatcher.SmsSynopsis(memo.Title, memo.Body),
            memo.Status,
            memo.PublishedAt,
            audiences.Where(audience => audience.MemoId == memo.Id).Select(MapAudience).ToList(),
            $"\"{memo.UpdatedAt.UtcTicks}\"")).ToList();

    private static MemoAudienceResponse MapAudience(MemoAudience audience) =>
        new(audience.AudienceType, audience.InstituteId, audience.DivisionId, audience.SectionId, audience.EmployeeId, audience.RoleCode);

    private static async Task<bool> CanReadMemoAsync(
        HttpContext context,
        Memo memo,
        SpmeDbContext db,
        CancellationToken ct)
    {
        if (CanManageMemo(context, memo))
            return true;

        if (IsHrViewer(context))
        {
            var callerInstituteId = CurrentInstituteId(context);
            if (callerInstituteId.HasValue && memo.InstituteId == callerInstituteId.Value)
                return true;
            if (callerInstituteId.HasValue &&
                memo.Status == MemoStatuses.Published &&
                await db.MemoAudiences.AsNoTracking().AnyAsync(audience =>
                    audience.MemoId == memo.Id && audience.InstituteId == callerInstituteId.Value, ct))
            {
                return true;
            }

            return false;
        }

        if (memo.Status != MemoStatuses.Published)
            return false;
        if (!CanAccessMemo(context, memo) &&
            !await db.MemoAudiences.AsNoTracking().AnyAsync(audience =>
                audience.MemoId == memo.Id &&
                (audience.InstituteId == CurrentInstituteId(context) ||
                 audience.EmployeeId == CurrentEmployeeId(context)), ct))
        {
            return false;
        }

        var reader = await GetMemoReaderAsync(context, db, ct);
        if (reader is null)
            return false;
        var audiences = await db.MemoAudiences.AsNoTracking()
            .Where(audience => audience.MemoId == memo.Id)
            .ToListAsync(ct);
        return MemoAudienceMatcher.Matches(
            audiences,
            reader.Employee.Id,
            reader.Employee.InstituteId,
            reader.Employment?.DivisionId,
            reader.Employment?.SectionId,
            reader.RoleNames);
    }

    private static async Task<MemoReader?> GetMemoReaderAsync(
        HttpContext context,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var employeeId = CurrentEmployeeId(context);
        var userId = CurrentUserId(context);
        if (!employeeId.HasValue || !userId.HasValue)
            return null;

        var employee = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == employeeId.Value &&
                candidate.ProfileStatus == "active", ct);
        if (employee is null)
            return null;

        var callerInstituteId = CurrentInstituteId(context);
        if (callerInstituteId.HasValue && employee.InstituteId != callerInstituteId.Value)
            return null;

        var employment = await db.EmploymentRecords.AsNoTracking()
            .FirstOrDefaultAsync(record => record.EmployeeId == employee.Id && record.IsCurrent, ct);
        var roleNames = await (
            from userRole in db.UserRoles
            join role in db.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == userId.Value
            select role.Name!).ToHashSetAsync(ct);
        foreach (var role in context.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value))
            roleNames.Add(role);
        return new MemoReader(employee, employment, roleNames);
    }

    private static bool CanManageMemo(HttpContext context, Memo memo) =>
        IsHrWriter(context) && (CanShareAcrossInstitutes(context) || CurrentInstituteId(context) == memo.InstituteId);

    private static bool CanAccessMemo(HttpContext context, Memo memo) =>
        CanShareAcrossInstitutes(context) || CurrentInstituteId(context) == memo.InstituteId;

    private static bool CanAccessInstitute(HttpContext context, Guid instituteId) =>
        CanShareAcrossInstitutes(context) || CurrentInstituteId(context) == instituteId;

    private static bool CanShareAcrossInstitutes(HttpContext context) =>
        IsPlatform(context) || (context.User.IsInRole(SpmeRoles.HrAdmin) && !CurrentInstituteId(context).HasValue);

    private static bool IsPlatform(HttpContext context) => context.User.IsInRole(SpmeRoles.PlatformAdmin);

    /// <summary>V2 memo authors/publishers only. Legacy StaffUser remains read-compatible, never auto-promoted.</summary>
    private static bool IsHrWriter(HttpContext context) =>
        IsPlatform(context) ||
        context.User.IsInRole(SpmeRoles.InstituteAdmin) ||
        context.User.IsInRole(SpmeRoles.HrAdmin);

    /// <summary>Institute HR viewers include preserved StaffUser / legacy staff-management roles.</summary>
    private static bool IsHrViewer(HttpContext context) =>
        IsHrWriter(context) || InstituteStaffAccess.HasStaffManagementReadCompatibility(context.User);

    private static Guid? CurrentInstituteId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirstValue("institute_id"), out var id) ? id : null;

    private static Guid? CurrentEmployeeId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirstValue("employee_id"), out var id) ? id : null;

    private static Guid? CurrentUserId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static string DisplayName(Employee employee) =>
        string.Join(' ', new[] { employee.OtherNames, employee.Surname }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static string? FirstContact(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private sealed record PreparedMemoDraft(Memo Memo, List<MemoAudience> Audiences);

    private sealed record MemoDeliveryRecipient(
        Employee Employee,
        User? User,
        string InstituteName,
        string? EmailAddress,
        string? Phone,
        bool InApp,
        bool SendEmail,
        bool SendSms);

    private sealed record MemoReader(
        Employee Employee,
        EmploymentRecord? Employment,
        IReadOnlySet<string> RoleNames);
}
