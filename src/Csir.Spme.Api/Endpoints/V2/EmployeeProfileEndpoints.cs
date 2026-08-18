using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Hr;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class EmployeeProfileEndpoints
{
    private static readonly HashSet<string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg"
    };

    private static readonly HashSet<string> DegreeContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png",
        "image/jpeg"
    };

    public static void MapEmployeeProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var profile = endpoints.MapGroup("/api/v2/employees/{employeeId:guid}")
            .WithGroupName("v2")
            .WithTags("Human Resources")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        profile.MapGet("/self-contact", GetSelfContactAsync)
            .WithName("EmployeeProfile_GetSelfContact")
            .WithSummary("Get the authenticated employee's self-service contact details.")
            .WithDescription("Returns residential address, primary email, pending email, and phone for the authenticated employee only. Other employee identifiers, including those in the same institute, receive a non-disclosing not-found response.")
            .Produces<EmployeeSelfContactResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        profile.MapPatch("/self-contact", UpdateSelfContactAsync)
            .WithName("EmployeeProfile_UpdateSelfContact")
            .WithSummary("Update address, email, and phone for the authenticated employee.")
            .WithDescription("Updates owner-only residential address, phone, and email. Email changes that require confirmation are stored as pending until verified. Callers cannot update another employee's contact details, and institute scope remains enforced.")
            .Produces<EmployeeSelfContactResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        profile.MapGet("/self-work", GetSelfWorkAsync)
            .WithName("EmployeeProfile_GetSelfWork")
            .WithSummary("Get the authenticated employee's work assignment and grade history.")
            .WithDescription("Returns appointment date, current grade, years in the present grade, institute, division, section, location, area of specialization, and research interests for senior members, plus promotion dates in the employee's ladder. Other employee identifiers receive a non-disclosing not-found response. Personal contact details stay on self-contact.")
            .Produces<EmployeeSelfWorkResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        profile.MapPatch("/self-work", UpdateSelfWorkAsync)
            .WithName("EmployeeProfile_UpdateSelfWork")
            .WithSummary("Update self-service work assignment and promotion dates.")
            .WithDescription("Staff may update location, area of specialization, research interests (senior members only), and promotion dates for grades in their ladder. Institute, division, section, job title, staff category, and first appointment remain HR-controlled. Callers cannot update another employee's work details.")
            .Produces<EmployeeSelfWorkResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        profile.MapGet("/documents", ListDocumentsAsync)
            .WithName("EmployeeProfile_ListDocuments")
            .WithSummary("List labeled profile documents for an accessible employee.")
            .WithDescription("Returns labeled profile document metadata for an owned or HR-accessible employee, including scan status and completeness without storage paths or permanent object URLs. Missing and out-of-scope employees use the same not-found response.")
            .Produces<CollectionResponse<EmployeeProfileDocumentResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        profile.MapPost("/document-upload-sessions", CreateDocumentUploadSessionAsync)
            .WithName("EmployeeProfile_CreateDocumentUploadSession")
            .WithSummary("Create a direct upload session for a labeled profile document.")
            .WithDescription("Creates a resumable direct-to-storage upload session for a labeled profile document after validating document type, file name, content type, declared size, checksum, requirement ownership, and quota. Large files are not buffered inside the API process.")
            .Produces<EmployeeProfileDocumentUploadSessionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        profile.MapGet("/documents/{documentId:guid}/content", DownloadDocumentAsync)
            .WithName("EmployeeProfile_DownloadDocument")
            .WithSummary("Download a clean profile document.")
            .WithDescription("Streams a virus-scanned profile document through the API after confirming ownership or authorized HR access. Unscanned, failed, missing, and out-of-scope documents are represented as not found and never expose storage locations.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    public static async Task<IResult> TryCompleteProfileDocumentUploadAsync(
        Guid uploadSessionId,
        SpmeDbContext db,
        IDirectFileUploadService uploads,
        IFileStorageService storage,
        IPromotionMalwareScanner scanner,
        IOptions<ProfileDocumentOptions> optionsAccessor,
        IAuditService audit,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryIdentity(context, out var userId, out var employeeId))
            return TypedResults.NotFound();

        var session = await db.EmployeeDocumentUploadSessions.SingleOrDefaultAsync(x =>
            x.Id == uploadSessionId && x.EmployeeId == employeeId && x.InitiatedByUserId == userId, ct);
        if (session is null)
            return TypedResults.NotFound();

        var inspected = await uploads.InspectAsync(session.StorageKey, ct);
        if (inspected is null || inspected.SizeBytes != session.DeclaredSizeBytes ||
            inspected.ContentType is not null &&
            !string.Equals(inspected.ContentType, session.ContentType, StringComparison.OrdinalIgnoreCase))
            return EndpointProblems.FromError(Error.Validation(
                "Uploaded content does not match the declared size and SHA-256 checksum."));

        await using var uploadedStream = await storage.DownloadAsync(session.StorageKey, ct);
        if (uploadedStream is null)
            return EndpointProblems.FromError(Error.Validation("The uploaded content could not be verified."));

        var signature = new byte[8];
        var signatureLength = await uploadedStream.ReadAsync(signature, ct);
        if (uploadedStream.CanSeek) uploadedStream.Position = 0;
        else return EndpointProblems.FromError(Error.Validation("The uploaded content cannot be securely inspected."));

        var actualSha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(uploadedStream, ct));
        if (!string.Equals(actualSha256, session.DeclaredSha256, StringComparison.OrdinalIgnoreCase) ||
            !ProfileDocumentRules.SignatureMatches(session.ContentType, signature.AsSpan(0, signatureLength)))
            return EndpointProblems.FromError(Error.Validation(
                "Uploaded content does not match the declared type or SHA-256 checksum."));

        var file = new FileRecord(session.StorageKey, session.FileName, session.ContentType,
            inspected.SizeBytes, session.DeclaredSha256, "employee-profile-document", session.InstituteId, "confidential");
        db.FileRecords.Add(file);

        var scan = await scanner.ScanAsync(session.StorageKey, ct);
        if (string.Equals(optionsAccessor.Value.DevelopmentScanResult, "clean", StringComparison.OrdinalIgnoreCase))
            scan = "clean";
        file.MarkScanStatus(scan);

        var existing = await db.EmployeeDocuments
            .Where(x => x.EmployeeId == session.EmployeeId &&
                        x.DocumentType == session.DocumentType &&
                        x.Status == ProfileDocumentConstants.StatusActive &&
                        x.LinkedChildId == session.LinkedChildId)
            .ToListAsync(ct);
        foreach (var item in existing)
            item.MarkSuperseded();

        var document = new EmployeeDocument(
            session.EmployeeId,
            session.InstituteId,
            session.DocumentType,
            file.Id,
            userId,
            session.LinkedChildId);
        db.EmployeeDocuments.Add(document);

        if (session.DocumentType == ProfileDocumentConstants.DependantBirthCertificate && session.LinkedChildId.HasValue)
        {
            var child = await db.EmployeeChildren.SingleOrDefaultAsync(x =>
                x.Id == session.LinkedChildId.Value && x.EmployeeId == session.EmployeeId, ct);
            if (child is not null && scan == "clean")
                child.Update(child.Name, child.DateOfBirth, child.Gender, child.BirthCertificateNumber, file.Id);
        }

        var completed = session.Complete(file.Id, document.Id, DateTimeOffset.UtcNow);
        if (completed.IsFailure)
            return EndpointProblems.FromError(completed.Error!);

        if (scan == "clean")
            await LinkEducationCertificatesAsync(db, session.EmployeeId, session.DocumentType, file.Id, ct);

        await audit.RecordAsync("employee-profile-document.upload-completed", "EmployeeDocument",
            document.Id.ToString(), null, $"type={session.DocumentType};scan={scan}", ct);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(MapDocument(document, file));
    }

    private static async Task<Results<Ok<EmployeeSelfContactResponse>, ProblemHttpResult>> GetSelfContactAsync(
        Guid employeeId,
        SpmeDbContext db,
        UserManager<User> userManager,
        HttpContext context,
        CancellationToken ct)
    {
        if (!IsCurrentEmployee(context, employeeId))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == employeeId, ct);
        if (employee is null)
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        string? pendingEmail = null;
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userId, out var parsedUserId))
        {
            var user = await userManager.FindByIdAsync(parsedUserId.ToString());
            pendingEmail = user?.PendingEmail;
        }

        return TypedResults.Ok(new EmployeeSelfContactResponse(
            employee.Id,
            employee.PrimaryEmail,
            pendingEmail,
            employee.Phone,
            employee.Address,
            employee.UpdatedAt));
    }

    private static async Task<Results<Ok<EmployeeSelfContactResponse>, ProblemHttpResult>> UpdateSelfContactAsync(
        Guid employeeId,
        UpdateEmployeeSelfContactRequest request,
        SpmeDbContext db,
        UserManager<User> userManager,
        IEmailService emailService,
        IConfiguration configuration,
        IAuditService audit,
        HttpContext context,
        CancellationToken ct)
    {
        if (!IsCurrentEmployee(context, employeeId))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == employeeId, ct);
        if (employee is null)
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        if (request.PrimaryEmail is not null)
        {
            var normalized = request.PrimaryEmail.Trim();
            var duplicate = await db.Employees.AsNoTracking().AnyAsync(candidate =>
                candidate.Id != employeeId &&
                candidate.NormalizedPrimaryEmail == normalized.ToUpperInvariant(), ct);
            if (duplicate)
                return EndpointProblems.FromError(Error.Conflict("That email address is already in use."));
        }

        employee.UpdateSelfContact(request.PrimaryEmail, request.Phone, request.ResidentialAddress);

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (request.PrimaryEmail is not null && Guid.TryParse(userId, out var parsedUserId))
        {
            var user = await userManager.FindByIdAsync(parsedUserId.ToString());
            if (user is not null &&
                !string.Equals(request.PrimaryEmail.Trim(), user.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await userManager.FindByEmailAsync(request.PrimaryEmail.Trim());
                var pendingForAnother = await userManager.Users.AnyAsync(candidate =>
                    candidate.Id != user.Id && candidate.PendingEmail == request.PrimaryEmail.Trim(), ct);
                if ((existing is not null && existing.Id != user.Id) || pendingForAnother)
                    return EndpointProblems.FromError(Error.Conflict("That email address is already in use."));

                user.RequestEmailChange(request.PrimaryEmail.Trim());
                var token = await userManager.GenerateChangeEmailTokenAsync(user, request.PrimaryEmail.Trim());
                var verifyUrl = BuildEmailChangeUrl(configuration, token);
                await emailService.SendAsync(request.PrimaryEmail.Trim(), "Confirm your CSIR SPME email address",
                    $"Confirm your email change using this secure link: {verifyUrl}", ct: ct);
                await userManager.UpdateAsync(user);
            }
        }

        await db.SaveChangesAsync(ct);
        await audit.RecordAndSaveAsync("employee.self-contact-updated", "Employee", employee.Id.ToString(),
            null, "self-service-contact", ct);

        string? pendingEmail = null;
        if (Guid.TryParse(userId, out parsedUserId))
            pendingEmail = (await userManager.FindByIdAsync(parsedUserId.ToString()))?.PendingEmail;

        return TypedResults.Ok(new EmployeeSelfContactResponse(
            employee.Id,
            employee.PrimaryEmail,
            pendingEmail,
            employee.Phone,
            employee.Address,
            employee.UpdatedAt));
    }

    private static async Task<Results<Ok<EmployeeSelfWorkResponse>, ProblemHttpResult>> GetSelfWorkAsync(
        Guid employeeId,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken ct)
    {
        if (!IsCurrentEmployee(context, employeeId))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var response = await EmployeeSelfWorkService.BuildAsync(employeeId, db, ct);
        if (response is null)
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<EmployeeSelfWorkResponse>, ProblemHttpResult>> UpdateSelfWorkAsync(
        Guid employeeId,
        UpdateEmployeeSelfWorkRequest request,
        SpmeDbContext db,
        IAuditService audit,
        HttpContext context,
        CancellationToken ct)
    {
        if (!IsCurrentEmployee(context, employeeId))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var employee = await db.Employees.AsNoTracking().AnyAsync(x => x.Id == employeeId, ct);
        if (!employee)
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var validationError = await EmployeeSelfWorkService.ValidateUpdateAsync(employeeId, request, db, ct);
        if (validationError is not null)
            return EndpointProblems.FromError(validationError);

        await EmployeeSelfWorkService.ApplyUpdateAsync(employeeId, request, db, ct);
        await audit.RecordAndSaveAsync("employee.self-work-updated", "Employee", employeeId.ToString(),
            null, $"grades={request.GradePromotions?.Count ?? 0}", ct);

        var response = await EmployeeSelfWorkService.BuildAsync(employeeId, db, ct);
        if (response is null)
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<CollectionResponse<EmployeeProfileDocumentResponse>>, ProblemHttpResult>> ListDocumentsAsync(
        Guid employeeId,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken ct)
    {
        if (!await CanReadEmployeeProfileAsync(employeeId, db, context, ct))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var documents = await (from document in db.EmployeeDocuments.AsNoTracking()
                               join file in db.FileRecords.AsNoTracking() on document.FileId equals file.Id
                               where document.EmployeeId == employeeId &&
                                     document.Status == ProfileDocumentConstants.StatusActive &&
                                     !file.IsDeleted
                               orderby document.DocumentType, document.CreatedAt descending
                               select MapDocument(document, file)).ToListAsync(ct);

        return TypedResults.Ok(new CollectionResponse<EmployeeProfileDocumentResponse>(documents, documents.Count));
    }

    private static async Task<IResult> CreateDocumentUploadSessionAsync(
        Guid employeeId,
        CreateEmployeeProfileDocumentUploadRequest request,
        SpmeDbContext db,
        IDirectFileUploadService uploads,
        IOptions<ProfileDocumentOptions> optionsAccessor,
        IAuditService audit,
        HttpContext context,
        CancellationToken ct)
    {
        if (!IsCurrentEmployee(context, employeeId))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == employeeId, ct);
        if (employee is null)
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var validation = ProfileDocumentRules.ValidateUploadRequest(
            request.DocumentType, request.FileName, request.ContentType, request.ByteLength,
            request.Sha256Checksum, request.LinkedChildId, optionsAccessor.Value.MaximumFileBytes);
        if (validation is not null)
            return EndpointProblems.FromError(validation);

        if (request.DocumentType == ProfileDocumentConstants.DependantBirthCertificate)
        {
            if (!request.LinkedChildId.HasValue)
                return EndpointProblems.FromError(Error.Validation("A dependant birth certificate requires a linked child."));
            var childExists = await db.EmployeeChildren.AsNoTracking().AnyAsync(x =>
                x.Id == request.LinkedChildId.Value && x.EmployeeId == employeeId, ct);
            if (!childExists)
                return EndpointProblems.FromError(Error.NotFound("Child record not found."));
        }
        else if (request.LinkedChildId.HasValue)
            return EndpointProblems.FromError(Error.Validation("Only dependant birth certificates may reference a child."));

        if (!TryIdentity(context, out var userId, out _))
            return EndpointProblems.FromError(Error.NotFound("Employee not found."));

        var options = optionsAccessor.Value;
        var expires = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(options.UploadSessionMinutes, 5, 1440));
        var fileName = Path.GetFileName(request.FileName);
        var storageKey =
            $"employee-profile/{employee.InstituteId:N}/{employeeId:N}/{request.DocumentType}/{Guid.NewGuid():N}/{fileName}";
        var access = await uploads.CreateWriteAccessAsync(storageKey, request.ContentType, request.ByteLength,
            request.Sha256Checksum, expires, ct);
        if (access is null)
            return EndpointProblems.FromError(Error.DependencyUnavailable("Direct upload storage is not configured."));

        var session = new EmployeeDocumentUploadSession(
            employeeId,
            employee.InstituteId,
            userId,
            request.DocumentType,
            request.LinkedChildId,
            storageKey,
            fileName,
            request.ContentType,
            request.ByteLength,
            request.Sha256Checksum.ToLowerInvariant(),
            expires);
        db.EmployeeDocumentUploadSessions.Add(session);
        await audit.RecordAsync("employee-profile-document.upload-session-created", "EmployeeDocumentUploadSession",
            session.Id.ToString(), null, $"employee={employeeId};type={request.DocumentType}", ct);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created(
            $"/api/v2/files/upload-sessions/{session.Id:D}",
            new EmployeeProfileDocumentUploadSessionResponse(
                session.Id, access.UploadUri, access.ExpiresAt, access.RequiredHeaders));
    }

    private static async Task<IResult> DownloadDocumentAsync(
        Guid employeeId,
        Guid documentId,
        SpmeDbContext db,
        IFileStorageService storage,
        HttpContext context,
        CancellationToken ct)
    {
        if (!await CanReadEmployeeProfileAsync(employeeId, db, context, ct))
            return EndpointProblems.FromError(Error.NotFound("Document not found."));

        var document = await (from item in db.EmployeeDocuments.AsNoTracking()
                              join file in db.FileRecords.AsNoTracking() on item.FileId equals file.Id
                              where item.EmployeeId == employeeId && item.Id == documentId &&
                                    item.Status == ProfileDocumentConstants.StatusActive && !file.IsDeleted
                              select new { file.StorageKey, file.ContentType, file.OriginalFileName, file.ScanStatus })
            .SingleOrDefaultAsync(ct);
        if (document is null)
            return EndpointProblems.FromError(Error.NotFound("Document not found."));
        if (document.ScanStatus != "clean")
            return EndpointProblems.FromError(Error.Forbidden("The document is not available until security scanning completes."));

        var stream = await storage.DownloadAsync(document.StorageKey, ct);
        if (stream is null)
            return EndpointProblems.FromError(Error.NotFound("Document not found."));
        return Results.File(stream, document.ContentType, document.OriginalFileName, enableRangeProcessing: true);
    }

    private static async Task LinkEducationCertificatesAsync(
        SpmeDbContext db,
        Guid employeeId,
        string documentType,
        Guid fileId,
        CancellationToken ct)
    {
        var qualificationLevel = documentType switch
        {
            ProfileDocumentConstants.BscCertificate => "bachelor-or-equivalent",
            ProfileDocumentConstants.MscCertificate => "masters-or-equivalent",
            ProfileDocumentConstants.PhdCertificate => "doctorate-or-equivalent",
            _ => null
        };
        if (qualificationLevel is null)
            return;

        var records = await db.EducationRecords
            .Where(x => x.EmployeeId == employeeId && x.QualificationLevel == qualificationLevel)
            .ToListAsync(ct);
        foreach (var record in records)
            record.SetCertificateFileId(fileId);
    }

    internal static async Task<bool> HasCleanCertificateForEducationAsync(
        SpmeDbContext db,
        Guid employeeId,
        EducationRecord record,
        CancellationToken ct)
    {
        if (record.CertificateFileId is Guid fileId)
        {
            var linked = await db.FileRecords.AsNoTracking()
                .AnyAsync(x => x.Id == fileId && !x.IsDeleted && x.ScanStatus == "clean", ct);
            if (linked)
                return true;
        }

        var documentType = ProfileDocumentRules.MapQualificationToDocumentType(record.QualificationLevel);
        if (documentType is null)
            return true;

        return await (from document in db.EmployeeDocuments.AsNoTracking()
                      join file in db.FileRecords.AsNoTracking() on document.FileId equals file.Id
                      where document.EmployeeId == employeeId &&
                            document.DocumentType == documentType &&
                            document.Status == ProfileDocumentConstants.StatusActive &&
                            !file.IsDeleted &&
                            file.ScanStatus == "clean"
                      select document.Id).AnyAsync(ct);
    }

    private static EmployeeProfileDocumentResponse MapDocument(EmployeeDocument document, FileRecord file) =>
        new(
            document.Id,
            document.DocumentType,
            ProfileDocumentRules.Label(document.DocumentType),
            file.OriginalFileName,
            file.ContentType,
            file.SizeBytes,
            file.ScanStatus,
            file.ScanStatus == "clean",
            document.LinkedChildId,
            document.CreatedAt);

    private static async Task<bool> CanReadEmployeeProfileAsync(
        Guid employeeId,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken ct) =>
        HumanResourcesEndpoints.CanReadEmployeeRecord(context, employeeId) &&
        await db.Employees.AsNoTracking().AnyAsync(x => x.Id == employeeId, ct);

    private static bool IsCurrentEmployee(HttpContext context, Guid employeeId) =>
        Guid.TryParse(context.User.FindFirst("employee_id")?.Value, out var currentEmployeeId) &&
        currentEmployeeId == employeeId;

    private static bool TryIdentity(HttpContext context, out Guid userId, out Guid employeeId)
    {
        userId = Guid.Empty;
        employeeId = Guid.Empty;
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId))
            return false;
        return Guid.TryParse(context.User.FindFirst("employee_id")?.Value, out employeeId);
    }

    private static string BuildEmailChangeUrl(IConfiguration configuration, string token) =>
        $"{configuration["PortalUrls:StaffPortalUrl"]?.TrimEnd('/') ?? "https://www.portal.csirstrategicplan.org"}/settings?emailToken={Uri.EscapeDataString(token)}";
}

internal static class ProfileDocumentRules
{
    public static Error? ValidateUploadRequest(
        string documentType,
        string fileName,
        string contentType,
        long byteLength,
        string sha256,
        Guid? linkedChildId,
        long maximumBytes)
    {
        var normalizedType = documentType.Trim().ToLowerInvariant();
        if (!SupportedDocumentTypes.Contains(normalizedType))
            return Error.Validation("The document type is not supported.");

        var normalizedFileName = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedFileName) || normalizedFileName.Length > 512 ||
            byteLength <= 0 || byteLength > maximumBytes ||
            !Regex.IsMatch(sha256 ?? string.Empty, "^[a-fA-F0-9]{64}$"))
            return Error.Validation("The document metadata, type, size, or SHA-256 checksum is invalid.");

        var allowed = AllowedContentTypes(normalizedType);
        if (!allowed.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            return Error.Validation("The document content type is not supported for this document slot.");

        if (normalizedType == ProfileDocumentConstants.DependantBirthCertificate && !linkedChildId.HasValue)
            return Error.Validation("A dependant birth certificate requires a linked child.");

        return null;
    }

    public static IReadOnlyCollection<string> SupportedDocumentTypes { get; } =
    [
        ProfileDocumentConstants.NationalId,
        ProfileDocumentConstants.BirthCertificate,
        ProfileDocumentConstants.BscCertificate,
        ProfileDocumentConstants.MscCertificate,
        ProfileDocumentConstants.PhdCertificate,
        ProfileDocumentConstants.DependantBirthCertificate
    ];

    public static IReadOnlyCollection<string> AllowedContentTypes(string documentType) =>
        documentType switch
        {
            ProfileDocumentConstants.NationalId or ProfileDocumentConstants.BirthCertificate or
                ProfileDocumentConstants.DependantBirthCertificate => ["image/png", "image/jpeg"],
            ProfileDocumentConstants.BscCertificate or ProfileDocumentConstants.MscCertificate or
                ProfileDocumentConstants.PhdCertificate => ["application/pdf", "image/png", "image/jpeg"],
            _ => []
        };

    public static string Label(string documentType) => documentType switch
    {
        ProfileDocumentConstants.NationalId => "National ID",
        ProfileDocumentConstants.BirthCertificate => "Birth certificate",
        ProfileDocumentConstants.BscCertificate => "BSc certificate",
        ProfileDocumentConstants.MscCertificate => "MSc certificate",
        ProfileDocumentConstants.PhdCertificate => "PhD certificate",
        ProfileDocumentConstants.DependantBirthCertificate => "Dependant birth certificate",
        _ => documentType
    };

    public static string? MapQualificationToDocumentType(string qualificationLevel) =>
        qualificationLevel.Trim().ToLowerInvariant() switch
        {
            "bachelor-or-equivalent" => ProfileDocumentConstants.BscCertificate,
            "masters-or-equivalent" => ProfileDocumentConstants.MscCertificate,
            "doctorate-or-equivalent" => ProfileDocumentConstants.PhdCertificate,
            _ => null
        };

    public static bool SignatureMatches(string contentType, ReadOnlySpan<byte> signature) =>
        contentType.ToLowerInvariant() switch
        {
            "application/pdf" => signature.StartsWith("%PDF"u8),
            "image/png" => signature.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
            "image/jpeg" => signature.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
            _ => false
        };
}
