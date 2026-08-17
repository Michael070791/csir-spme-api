using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Iam;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Infrastructure.Persistence;
using Csir.Spme.Infrastructure.Communications;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class IamEndpoints
{
    private const string LoginLockSettingKey = "iam.login-lock";

    public static void MapIamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/v2/auth")
            .WithGroupName("v2")
            .WithTags("Identity and Access");

        auth.MapPost("/sessions", LoginAsync)
            .RequireRateLimiting("login")
            .WithName("Auth_CreateSession")
            .WithSummary("Create an authenticated session.")
            .WithDescription("Issues a short-lived JWT access token and an HttpOnly refresh cookie for a provisioned Identity account. Failed sign-in does not disclose whether the username exists. Rate limited per client IP.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .AllowAnonymous();
        auth.MapPost("/sessions/refresh", RefreshSessionAsync)
            .RequireRateLimiting("token-refresh")
            .WithName("Auth_RefreshSession")
            .WithSummary("Rotate the secure refresh-token session.")
            .WithDescription("Rotates the HttpOnly refresh cookie into a new access token and refresh cookie. Reuse of a rotated refresh token revokes the entire token family.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .AllowAnonymous();
        auth.MapDelete("/sessions/current", LogoutCurrentSessionAsync)
            .WithName("Auth_DeleteCurrentSession")
            .WithSummary("Revoke the current refresh-token session.")
            .Produces(StatusCodes.Status204NoContent)
            .WithDescription("Always expires the SameSite refresh cookie and, when present, revokes the matching server-side session. The response is deliberately non-disclosing and safe when the access token is missing or expired.")
            .AllowAnonymous();
        auth.MapPost("/account-activations/challenges", CreateAccountActivationChallengeAsync)
            .RequireRateLimiting("account-activation")
            .WithName("Auth_CreateAccountActivationChallenge")
            .WithSummary("Request an employee account activation code.")
            .WithDescription("Queues a one-time code only for a unique staff account whose supplied contact matches a deliverable HR contact. Successful responses disclose the masked destination; failures explicitly identify not-found, contact, activation-status, and verification resend-limit outcomes.")
            .Produces<AccountActivationChallengeResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous();
        auth.MapPost("/account-activations/challenges/{challengeId:guid}/verify", VerifyAccountActivationChallengeAsync)
            .RequireRateLimiting("account-activation")
            .WithName("Auth_VerifyAccountActivationChallenge")
            .WithSummary("Verify an employee account activation code.")
            .WithDescription("Returns a single-use verification token. Incorrect codes use verification_failed; missing, expired, consumed, or already-verified challenges use verification_expired.")
            .Produces<VerifyAccountActivationChallengeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous();
        auth.MapPost("/account-activations/complete", CompleteAccountActivationAsync)
            .RequireRateLimiting("account-activation")
            .WithName("Auth_CompleteAccountActivation")
            .WithSummary("Set an Identity password after a verified activation challenge.")
            .WithDescription("Consumes a valid verification token and activates the linked staff account. Invalid or expired verification state uses verification_expired; password and account completion failures use validation_failed.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous();
        auth.MapPost("/login", LoginAsync)
            .RequireRateLimiting("login")
            .WithName("Auth_Login")
            .WithSummary("Create an authenticated session using the legacy login alias.")
            .WithDescription("Legacy alias for session creation. Prefer the sessions resource. Issues the same JWT access token and refresh cookie contract as the primary sign-in operation.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .AllowAnonymous();
        auth.MapPost("/password-resets", RequestPasswordResetAsync)
            .RequireRateLimiting("password-reset")
            .WithName("Auth_RequestPasswordReset")
            .WithSummary("Request a password reset email without disclosing account existence.")
            .WithDescription("Accepts an email address and always returns an accepted response so callers cannot enumerate accounts. The anonymous operation is rate limited, while eligible accounts receive a single-use reset link.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous();
        auth.MapPost("/password-resets/confirm", ConfirmPasswordResetAsync)
            .RequireRateLimiting("password-reset")
            .WithName("Auth_ConfirmPasswordReset")
            .WithSummary("Reset a password using a single-use Identity reset token.")
            .WithDescription("Consumes the request identifier and single-use reset token to set a matching new password. Invalid, expired, reused, or policy-invalid requests return an unprocessable validation problem, and anonymous attempts are rate limited.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous();
        auth.MapGet("/me", GetCurrentUserAsync)
            .WithName("Auth_GetCurrentUser")
            .WithSummary("Get the authenticated user context.")
            .WithDescription("Returns the account, roles, employee link, and institute scope derived from the authenticated bearer subject. Missing, malformed, or inactive account identity returns an unauthorized problem.")
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        var me = endpoints.MapGroup("/api/v2/me")
            .WithGroupName("v2")
            .WithTags("Settings")
            .WithDescription("Authenticated self-service profile and security settings. Every operation derives the user from the bearer token; client supplied user, employee, and institute identifiers are never accepted.")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        me.MapGet("", GetCurrentUserAsync)
            .WithName("Settings_GetMyProfile")
            .WithSummary("Get the authenticated user's profile settings.")
            .WithDescription("Returns the profile and security context for the bearer-token subject, including the display name and any pending verified email change. This self-service operation never accepts a user, employee, or institute identifier and cannot expose another account.")
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK);
        me.MapGet("/portal-profile", GetPortalProfileAsync)
            .WithName("Settings_GetMyPortalProfile")
            .WithSummary("Get the authenticated staff portal profile.")
            .WithDescription("Returns the minimal self-service projection required by the staff portal. Staff identity, employment, institute, contact-confirmation state, and effective permissions are derived from the authenticated account and server-side Identity claims. Employment includes staff category and the Conditions of Service job title used for promotion checking. It never accepts a user, employee, staff, or institute identifier and deliberately excludes phone numbers, dates of birth, addresses, and family data.")
            .Produces<PortalProfileResponse>(StatusCodes.Status200OK);
        me.MapPatch("", UpdateMyProfileAsync)
            .WithName("Settings_UpdateMyProfile")
            .WithSummary("Update display name and request a verified email change.")
            .WithDescription("Updates the display name immediately. A changed email remains pending until the verification link sent to the requested address is confirmed; the existing verified login and delivery email remains active until then.")
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
        me.MapPost("/email/confirm", ConfirmMyEmailAsync)
            .WithName("Settings_ConfirmMyEmail")
            .WithSummary("Confirm the authenticated user's pending email change.")
            .WithDescription("Applies a pending email address only when the authenticated account presents a valid, single-use Identity verification token. Invalid, expired, and missing pending-change requests return the same validation outcome without disclosing account information.")
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        me.MapPost("/password", ChangeMyPasswordAsync)
            .RequireRateLimiting("password-change")
            .WithName("Settings_ChangeMyPassword")
            .WithSummary("Change the authenticated user's password.")
            .WithDescription("Requires the current password and a confirmed new password that satisfies Identity policy. Successful changes rotate the security stamp, invalidating earlier bearer sessions.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        var preferences = endpoints.MapGroup("/api/v2/notification-preferences/me")
            .WithGroupName("v2")
            .WithTags("Settings")
            .WithDescription("Authenticated user's email-notification preferences. These controls affect email delivery only and do not remove private in-app notifications.")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        preferences.MapGet("", GetNotificationPreferencesAsync)
            .WithName("Settings_GetNotificationPreferences")
            .WithSummary("Get email-notification preferences, creating enabled defaults when absent.")
            .WithDescription("Returns the authenticated user's four email-delivery preferences and creates an all-enabled preference record the first time it is requested. In-app notifications are intentionally unaffected, and no recipient or institute selector is accepted.")
            .Produces<NotificationPreferenceResponse>(StatusCodes.Status200OK);
        preferences.MapPatch("", UpdateNotificationPreferencesAsync)
            .WithName("Settings_UpdateNotificationPreferences")
            .WithSummary("Replace the authenticated user's email-notification preferences.")
            .WithDescription("Replaces every email-delivery preference for the bearer-token subject in one atomic personal-settings update. The request cannot target another user, employee, or institute, and it does not delete in-app inbox notifications.")
            .Produces<NotificationPreferenceResponse>(StatusCodes.Status200OK);

        var mySessions = endpoints.MapGroup("/api/v2/users/me/sessions")
            .WithGroupName("v2")
            .WithTags("Settings")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        mySessions.MapGet("", GetMySessionsAsync)
            .WithName("Settings_ListMySessions")
            .WithSummary("List the authenticated user's active sessions.")
            .WithDescription("Lists at most fifty active sessions owned by the bearer-token subject, including bounded device metadata and a current-session marker without exposing refresh tokens.")
            .Produces<IReadOnlyList<UserSessionResponse>>(StatusCodes.Status200OK);
        mySessions.MapDelete("/{sessionId:guid}", RevokeMySessionAsync)
            .WithName("Settings_RevokeMySession")
            .WithSummary("Revoke an owned session and all of its refresh tokens.")
            .WithDescription("Revokes one active session owned by the bearer-token subject and invalidates every refresh token in that session without revealing another user's session existence.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var systemUsers = endpoints.MapGroup("/api/v2/system-users")
            .WithGroupName("v2")
            .WithTags("System Users")
            .RequireAuthorization(AuthorizationPolicies.ReadUsers)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        MapSystemUserReadEndpoints(systemUsers, "SystemUsers", "system user accounts");
        systemUsers.MapPatch("/{id:guid}/roles", UpdateSystemUserRolesAsync)
            .WithName("SystemUsers_UpdateRoles")
            .WithSummary("Update a system user's roles.")
            .WithDescription("Replaces roles for an accessible non-employee system account, revokes its active sessions when roles change, and records an audit event. Scoped administrators cannot manage out-of-scope users or assign PlatformAdmin; invalid roles return validation errors, while hidden accounts return not found.")
            .RequireAuthorization(AuthorizationPolicies.ManageScopedUsers)
            .Produces<SystemUserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        systemUsers.MapPatch("/{id:guid}/institute", UpdateSystemUserInstituteAsync)
            .WithName("SystemUsers_UpdateInstitute")
            .WithSummary("Assign or clear a system user's institute scope.")
            .WithDescription("Platform administrators may assign an active institute. Clearing institute is allowed only for PlatformAdmin identity accounts. HrAdmin, InstituteAdmin, and preserved staff-management accounts require a non-null institute.")
            .RequireAuthorization(AuthorizationPolicies.ManageUsers)
            .Produces<SystemUserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        systemUsers.MapDelete("/{id:guid}", DeleteSystemUserAsync)
            .WithName("SystemUsers_Delete")
            .WithSummary("Delete a system user account.")
            .WithDescription("Permanently deletes a non-employee system account and records the removal for audit. Manage-users authorization is required; self-deletion and deleting the last PlatformAdmin return conflicts, while an unknown or employee-linked account returns not found.")
            .RequireAuthorization(AuthorizationPolicies.ManageUsers)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        systemUsers.MapPost("/email-recipients", GetEmailRecipientsAsync)
            .WithName("SystemUsers_GetEmailRecipients")
            .WithSummary("Resolve system user email recipients.")
            .WithDescription("Resolves email-capable non-employee accounts from optional user, role, and status filters within the caller's permitted institute scope. Scoped-user management authorization is required, and inaccessible accounts are omitted rather than disclosed.")
            .RequireAuthorization(AuthorizationPolicies.ManageScopedUsers)
            .Produces<EmailRecipientsResponse>(StatusCodes.Status200OK);
        systemUsers.MapPost("/bulk-email", SendBulkEmailAsync)
            .WithName("SystemUsers_SendBulkEmail")
            .WithSummary("Send a bulk email to system users.")
            .WithDescription("Sends a personalized message to filtered, email-capable non-employee accounts within the caller's permitted institute scope and audits aggregate sent and skipped counts. Scoped-user management authorization is required; a missing subject or body returns validation failure.")
            .RequireAuthorization(AuthorizationPolicies.ManageScopedUsers)
            .Produces<BulkEmailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        systemUsers.MapPost("/hr-portal-links", SendHrPortalLinksAsync)
            .WithName("SystemUsers_SendHrPortalLinks")
            .WithSummary("Send HR portal reset links to HR system users.")
            .WithDescription("Requests password-reset links for filtered HR administration accounts, defaulting to the supported administrator roles when no roles are supplied. Manage-users authorization and caller scope apply; delivery failures are counted as skipped without exposing account details.")
            .RequireAuthorization(AuthorizationPolicies.ManageUsers)
            .Produces<SendHrPortalLinksResponse>(StatusCodes.Status200OK);
        systemUsers.MapGet("/login-lock", GetLoginLockAsync)
            .WithName("SystemUsers_GetLoginLock")
            .WithSummary("Get global login lock state.")
            .WithDescription("Returns whether password login is globally locked for non-PlatformAdmin accounts. Manage-users authorization is required, with unauthenticated and insufficiently privileged callers receiving the group-level authorization problems.")
            .RequireAuthorization(AuthorizationPolicies.ManageUsers)
            .Produces<LoginLockResponse>(StatusCodes.Status200OK);
        systemUsers.MapPut("/login-lock", UpdateLoginLockAsync)
            .WithName("SystemUsers_UpdateLoginLock")
            .WithSummary("Update global login lock state.")
            .WithDescription("Enables or disables the global password-login lock, persists the setting, and records an audit event. Manage-users authorization is required; the lock does not prevent PlatformAdmin sign-in.")
            .RequireAuthorization(AuthorizationPolicies.ManageUsers)
            .Produces<LoginLockResponse>(StatusCodes.Status200OK);

        var users = endpoints.MapGroup("/api/v2/users")
            .WithGroupName("v2")
            .WithTags("Identity and Access")
            .RequireAuthorization(AuthorizationPolicies.ReadUsers)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        MapSystemUserReadEndpoints(users, "Users", "user account compatibility aliases");

        var roles = endpoints.MapGroup("/api/v2/roles")
            .WithGroupName("v2")
            .WithTags("Identity and Access")
            .RequireAuthorization(AuthorizationPolicies.ReadRoles)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        roles.MapGet("", GetRolesAsync)
            .WithName("Roles_List")
            .WithSummary("List assignable roles.")
            .WithDescription("Returns all configured application roles ordered by code for callers authorized to read roles. Authentication and role-read permission are required; unauthorized or forbidden requests use the group-level problem responses.")
            .Produces<CollectionResponse<RoleResponse>>(StatusCodes.Status200OK);
    }

    private static void MapSystemUserReadEndpoints(RouteGroupBuilder users, string routeNamePrefix, string routeDescription)
    {
        users.MapGet("", GetSystemUsersAsync)
            .WithName($"{routeNamePrefix}_List")
            .WithSummary($"List {routeDescription}.")
            .WithDescription($"Returns a bounded, page-based list of {routeDescription} filtered by identity type, status, institute, role, or search text. Results are restricted to the caller's effective institute scope, and unauthorized or forbidden callers receive the route group's problem response.")
            .Produces<PageResponse<SystemUserResponse>>(StatusCodes.Status200OK);
        users.MapGet("/{id:guid}", GetSystemUserAsync)
            .WithName($"{routeNamePrefix}_Get")
            .WithSummary($"Get a {routeDescription.TrimEnd('s')}.")
            .WithDescription($"Returns one {routeDescription.TrimEnd('s')} only when it is visible in the caller's effective institute scope. Authentication and user-read permission are required, and missing or out-of-scope identifiers return the same not-found problem.")
            .Produces<SystemUserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>Creates a bearer-token session for a valid user account.</summary>
    private static async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        UserManager<User> userManager,
        SpmeDbContext db,
        IJwtTokenService jwtTokenService,
        HttpContext context,
        IAuditService audit,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var user = await FindSafeUserAsync(userManager, db, audit, request.Username, cancellationToken);
        if (user is null || user.AccountStatus is not ("active" or "password-reset-required"))
        {
            return EndpointProblems.Unauthorized();
        }

        if (await userManager.IsLockedOutAsync(user) || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            if (!await userManager.IsLockedOutAsync(user))
                await userManager.AccessFailedAsync(user);
            return EndpointProblems.Unauthorized();
        }

        if (user.AccountStatus == "password-reset-required")
        {
            return EndpointProblems.PasswordResetRequired(user.Email);
        }

        var roles = await userManager.GetRolesAsync(user);
        if (await IsLoginLockedAsync(db, cancellationToken) && !roles.Contains(SpmeRoles.PlatformAdmin))
        {
            return EndpointProblems.LoginLocked();
        }

        if (user.EmployeeId.HasValue)
            await ProvisionVerifiedLegacyLoginIdentifiersAsync(user, db, DateTimeOffset.UtcNow, cancellationToken);

        user.RecordLogin(DateTimeOffset.UtcNow);
        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.UpdateAsync(user);

        var refresh = IssueRefreshToken(user, context, configuration);
        db.UserSessions.Add(new UserSession(refresh.Record.SessionId, user.Id,
            request.DeviceName, request.Platform, DateTimeOffset.UtcNow));
        db.RefreshTokens.Add(refresh.Record);
        await db.SaveChangesAsync(cancellationToken);
        var token = await jwtTokenService.CreateAccessTokenAsync(user, refresh.Record.SessionId, cancellationToken);
        WriteRefreshCookie(context, refresh.RawToken, refresh.Record.ExpiresAt);
        await audit.RecordAndSaveAsync("auth.session-created", "UserSession", refresh.Record.SessionId.ToString(),
            null, "password-authenticated", cancellationToken);

        var response = MapLoginResponse(token, user, roles.ToArray(), refresh.Record);
        return TypedResults.Ok(response);
    }

    private static async Task<User?> FindSafeUserAsync(
        UserManager<User> userManager,
        SpmeDbContext db,
        IAuditService audit,
        string usernameOrEmail,
        CancellationToken ct)
    {
        var normalized = LoginIdentifierNormalizer.Normalize(usernameOrEmail);
        if (normalized is not null)
        {
            var identifiers = await db.UserLoginIdentifiers.AsNoTracking()
                .Where(identifier => identifier.IdentifierType == normalized.Value.Type &&
                    identifier.NormalizedValue == normalized.Value.Value &&
                    identifier.IsActive && identifier.IsVerified)
                .Select(identifier => identifier.UserId)
                .Distinct()
                .Take(2)
                .ToListAsync(ct);
            if (identifiers.Count == 1)
                return await userManager.FindByIdAsync(identifiers[0].ToString());
            if (identifiers.Count > 1)
                return null;

            var legacyUser = await ResolveUniqueLegacyEmployeeUserAsync(normalized.Value, db, ct);
            if (legacyUser is not null)
                return legacyUser;

            if (await HasLegacyIdentifierCollisionAsync(normalized.Value, db, ct))
                await audit.RecordAndSaveAsync("auth.legacy-login-identifier-collision", "LoginIdentifier",
                    HashToken(normalized.Value.Value), null, "login-blocked", ct);
            if (normalized.Value.Type == "phone")
                return null;
        }

        var raw = usernameOrEmail.Trim();
        var user = await userManager.FindByNameAsync(raw) ?? await userManager.FindByEmailAsync(raw);
        return user?.EmployeeId is null ? user : null;
    }

    private static async Task<User?> ResolveUniqueLegacyEmployeeUserAsync(
        (string Type, string Value) normalized,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var employeeIds = await FindEmployeeIdsAsync(normalized, db, ct);
        if (employeeIds.Count != 1)
            return null;

        var users = await db.Users
            .Where(user => user.EmployeeId == employeeIds[0] && user.AccountStatus == "active")
            .Take(2)
            .ToListAsync(ct);
        if (users.Count != 1 || string.IsNullOrWhiteSpace(users[0].PasswordHash))
            return null;

        var user = users[0];
        if (normalized.Type == "email" &&
            (!user.EmailConfirmed || LoginIdentifierNormalizer.Normalize(user.Email) != normalized))
            return null;
        if (normalized.Type == "phone" &&
            (!user.PhoneNumberConfirmed || LoginIdentifierNormalizer.Normalize(user.PhoneNumber) != normalized))
            return null;
        return user;
    }

    private static async Task<bool> HasLegacyIdentifierCollisionAsync(
        (string Type, string Value) normalized,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var employeeIds = await FindEmployeeIdsAsync(normalized, db, ct);
        if (employeeIds.Count != 1)
            return employeeIds.Count > 1;
        return await db.Users.CountAsync(user => user.EmployeeId == employeeIds[0], ct) != 1;
    }

    private static async Task<List<Guid>> FindEmployeeIdsAsync(
        (string Type, string Value) normalized,
        SpmeDbContext db,
        CancellationToken ct)
    {
        if (normalized.Type == "staff-id")
            return await db.Employees.AsNoTracking()
                .Where(employee => employee.NormalizedStaffId == normalized.Value)
                .Select(employee => employee.Id).Take(2).ToListAsync(ct);
        if (normalized.Type == "email")
            return await db.Employees.AsNoTracking()
                .Where(employee => employee.NormalizedPrimaryEmail == normalized.Value)
                .Select(employee => employee.Id).Take(2).ToListAsync(ct);
        return (await db.Employees.AsNoTracking()
                .Where(employee => employee.Phone != null)
                .Select(employee => new { employee.Id, employee.Phone }).ToListAsync(ct))
            .Where(employee => LoginIdentifierNormalizer.NormalizeGhanaPhone(employee.Phone) == normalized.Value)
            .Select(employee => employee.Id).Take(2).ToList();
    }

    private static async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> RefreshSessionAsync(
        HttpContext context,
        SpmeDbContext db,
        UserManager<User> userManager,
        IJwtTokenService jwtTokenService,
        IAuditService audit,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (!context.Request.Cookies.TryGetValue(RefreshCookieName, out var rawToken) ||
            string.IsNullOrWhiteSpace(rawToken))
            return EndpointProblems.Unauthorized();

        var tokenHash = HashToken(rawToken);
        var current = await db.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, ct);
        if (current is null)
            return EndpointProblems.Unauthorized();

        var now = DateTimeOffset.UtcNow;
        if (!current.IsActive(now))
        {
            if (current.ReplacedByTokenId.HasValue)
            {
                var family = await db.RefreshTokens
                    .Where(token => token.FamilyId == current.FamilyId && token.RevokedAt == null)
                    .ToListAsync(ct);
                foreach (var member in family)
                    member.Revoke("reuse-detected", now);

                var compromisedUser = await userManager.FindByIdAsync(current.UserId.ToString());
                if (compromisedUser is not null)
                    await userManager.UpdateSecurityStampAsync(compromisedUser);
                await db.SaveChangesAsync(ct);
                await audit.RecordAndSaveAsync("auth.refresh-token-reuse-detected", "UserSession", current.SessionId.ToString(),
                    null, "family-revoked", ct);
            }
            DeleteRefreshCookie(context);
            return EndpointProblems.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(current.UserId.ToString());
        if (user is null || user.AccountStatus != "active" ||
            !string.Equals(current.SecurityStamp, user.SecurityStamp, StringComparison.Ordinal))
        {
            current.Revoke("security-stamp-changed", now);
            await db.SaveChangesAsync(ct);
            DeleteRefreshCookie(context);
            return EndpointProblems.Unauthorized();
        }

        var replacement = IssueRefreshToken(user, context, configuration, current.FamilyId, current.SessionId);
        current.Rotate(replacement.Record.Id, now);
        db.RefreshTokens.Add(replacement.Record);
        var session = await db.UserSessions.SingleOrDefaultAsync(x => x.Id == current.SessionId && x.UserId == user.Id, ct);
        if (session is null)
        {
            session = new UserSession(current.SessionId, user.Id, InferDeviceName(current.UserAgent),
                InferPlatform(current.UserAgent), current.IssuedAt);
            db.UserSessions.Add(session);
        }
        session.Touch(now);
        await db.SaveChangesAsync(ct);
        WriteRefreshCookie(context, replacement.RawToken, replacement.Record.ExpiresAt);

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = await jwtTokenService.CreateAccessTokenAsync(user, current.SessionId, ct);
        await audit.RecordAndSaveAsync("auth.session-refreshed", "UserSession", current.SessionId.ToString(), null, "rotated", ct);
        return TypedResults.Ok(MapLoginResponse(accessToken, user, roles.ToArray(), replacement.Record));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> LogoutCurrentSessionAsync(
        HttpContext context,
        SpmeDbContext db,
        IAuditService audit,
        CancellationToken ct)
    {
        if (context.Request.Cookies.TryGetValue(RefreshCookieName, out var rawToken) &&
            !string.IsNullOrWhiteSpace(rawToken))
        {
            var hash = HashToken(rawToken);
            var token = await db.RefreshTokens.SingleOrDefaultAsync(candidate => candidate.TokenHash == hash, ct);
            if (token is not null)
            {
                var now = DateTimeOffset.UtcNow;
                var sessionTokens = await db.RefreshTokens
                    .Where(candidate => candidate.UserId == token.UserId && candidate.SessionId == token.SessionId && candidate.RevokedAt == null)
                    .ToListAsync(ct);
                foreach (var member in sessionTokens) member.Revoke("logout", now);
                var session = await db.UserSessions.SingleOrDefaultAsync(candidate => candidate.Id == token.SessionId, ct);
                session?.Revoke(now);
                await db.SaveChangesAsync(ct);
                await audit.RecordAndSaveAsync("auth.session-revoked", "UserSession", token.SessionId.ToString(), null, "logout", ct);
            }
        }

        DeleteRefreshCookie(context);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetMySessionsAsync(HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return EndpointProblems.Unauthorized();

        var currentSessionId = await ResolveCurrentSessionIdAsync(context, db, userId, ct);
        var query = db.UserSessions.AsNoTracking()
            .Where(x => x.UserId == userId && x.RevokedAt == null);
        List<UserSessionResponse> sessions;
        if (db.Database.IsSqlite())
        {
            sessions = (await query.ToListAsync(ct))
                .OrderByDescending(x => x.LastSeenAt)
                .Take(50)
                .Select(x => new UserSessionResponse(x.Id, x.DeviceName, x.Platform,
                    x.StartedAt, x.LastSeenAt, x.Id == currentSessionId))
                .ToList();
        }
        else
        {
            sessions = await query.OrderByDescending(x => x.LastSeenAt)
                .Take(50)
                .Select(x => new UserSessionResponse(x.Id, x.DeviceName, x.Platform,
                    x.StartedAt, x.LastSeenAt, x.Id == currentSessionId))
                .ToListAsync(ct);
        }
        context.Response.Headers.CacheControl = "no-store, max-age=0";
        return TypedResults.Ok(sessions);
    }

    private static async Task<IResult> RevokeMySessionAsync(
        Guid sessionId, HttpContext context, SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return EndpointProblems.Unauthorized();
        var session = await db.UserSessions.SingleOrDefaultAsync(
            x => x.Id == sessionId && x.UserId == userId && x.RevokedAt == null, ct);
        if (session is null) return EndpointProblems.FromError(Error.NotFound("Session not found."));

        var isCurrent = await ResolveCurrentSessionIdAsync(context, db, userId, ct) == sessionId;
        var now = DateTimeOffset.UtcNow;
        session.Revoke(now);
        var tokens = await db.RefreshTokens
            .Where(x => x.UserId == userId && x.SessionId == sessionId && x.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in tokens) token.Revoke("session-revoked", now);
        await db.SaveChangesAsync(ct);
        await audit.RecordAndSaveAsync("auth.session-revoked", "UserSession", sessionId.ToString(), null,
            "self-service", ct);

        if (isCurrent)
            DeleteRefreshCookie(context);
        return TypedResults.NoContent();
    }

    private static async Task<Guid?> ResolveCurrentSessionIdAsync(
        HttpContext context, SpmeDbContext db, Guid userId, CancellationToken ct)
    {
        if (!context.Request.Cookies.TryGetValue(RefreshCookieName, out var rawToken) || string.IsNullOrWhiteSpace(rawToken))
            return null;
        var hash = HashToken(rawToken);
        return await db.RefreshTokens.AsNoTracking()
            .Where(x => x.UserId == userId && x.TokenHash == hash && x.RevokedAt == null)
            .Select(x => (Guid?)x.SessionId).SingleOrDefaultAsync(ct);
    }

    private static string? InferDeviceName(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;
        if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) return "Microsoft Edge";
        if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)) return "Google Chrome";
        if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)) return "Mozilla Firefox";
        if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase)) return "Safari";
        return "Web browser";
    }

    private static string? InferPlatform(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;
        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)) return "windows";
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)) return "android";
        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)) return "ios";
        if (userAgent.Contains("Mac OS", StringComparison.OrdinalIgnoreCase)) return "macos";
        if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase)) return "linux";
        return "web";
    }

    private static LoginResponse MapLoginResponse(
        IssuedToken token,
        User user,
        IReadOnlyList<string> roles,
        RefreshToken refresh) => new(
            token.AccessToken,
            "Bearer",
            token.ExpiresInSeconds,
            token.ExpiresAt,
            MapUser(user),
            roles,
            refresh.SessionId,
            refresh.ExpiresAt);

    private static (RefreshToken Record, string RawToken) IssueRefreshToken(
        User user,
        HttpContext context,
        IConfiguration configuration,
        Guid? familyId = null,
        Guid? sessionId = null)
    {
        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        var expiryDays = Math.Clamp(configuration.GetValue("Jwt:RefreshTokenExpiryDays", 7), 1, 90);
        var record = new RefreshToken(
            user.Id,
            HashToken(rawToken),
            familyId ?? Guid.NewGuid(),
            sessionId ?? Guid.NewGuid(),
            user.SecurityStamp ?? throw new InvalidOperationException("The Identity security stamp is required."),
            DateTimeOffset.UtcNow.AddDays(expiryDays),
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString()[..Math.Min(context.Request.Headers.UserAgent.ToString().Length, 512)]);
        return (record, rawToken);
    }

    private const string RefreshCookieName = "spme_refresh";

    /// <summary>
    /// Local Vite/LAN access is HTTP. Secure cookies are dropped by browsers on http:// origins,
    /// which breaks refresh and looks like random 401s on staff saves. Use Secure only for HTTPS.
    /// </summary>
    private static CookieOptions RefreshCookieOptions(HttpContext context, DateTimeOffset? expiresAt = null) =>
        new()
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v2/auth/sessions",
            Expires = expiresAt,
            IsEssential = true
        };

    private static void WriteRefreshCookie(HttpContext context, string rawToken, DateTimeOffset expiresAt) =>
        context.Response.Cookies.Append(RefreshCookieName, rawToken, RefreshCookieOptions(context, expiresAt));

    private static void DeleteRefreshCookie(HttpContext context) =>
        context.Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions(context));

    private static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static async Task<IResult> CreateAccountActivationChallengeAsync(
        CreateAccountActivationChallengeRequest request,
        IAccountActivationService service,
        CancellationToken ct)
    {
        var result = await service.CreateChallengeAsync(request.Identifier, request.Contact, ct);
        if (!result.Succeeded)
            return AccountActivationProblem(result.StatusCode, result.ErrorCode!, result.Detail!, result.Errors);
        var value = result.Value!;
        return TypedResults.Accepted(
            (string?)null,
            new AccountActivationChallengeResponse(
                value.ChallengeId,
                value.ExpiresAt,
                "code_sent",
                value.DeliveryChannel,
                value.MaskedDestination,
                $"A verification code was sent by {value.DeliveryChannel} to {value.MaskedDestination}."));
    }

    private static async Task<IResult> VerifyAccountActivationChallengeAsync(
        Guid challengeId,
        VerifyAccountActivationChallengeRequest request,
        IAccountActivationService service,
        CancellationToken ct)
    {
        var result = await service.VerifyChallengeAsync(challengeId, request.Code, ct);
        if (!result.Succeeded)
            return AccountActivationProblem(result.StatusCode, result.ErrorCode!, result.Detail!, result.Errors);
        return TypedResults.Ok(new VerifyAccountActivationChallengeResponse(
            result.Value!.VerificationToken,
            result.Value.ExpiresAt,
            "Verification succeeded. Complete account activation before the challenge expires."));
    }

    private static async Task<IResult> CompleteAccountActivationAsync(
        CompleteAccountActivationRequest request,
        IAccountActivationService service,
        CancellationToken ct)
    {
        var result = await service.CompleteAsync(
            request.ChallengeId,
            request.VerificationToken,
            request.Password,
            request.ConfirmPassword,
            ct);
        return result.Succeeded
            ? TypedResults.NoContent()
            : AccountActivationProblem(result.StatusCode, result.ErrorCode!, result.Detail!, result.Errors);
    }

    private static ProblemHttpResult AccountActivationProblem(
        int statusCode,
        string code,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors) =>
        TypedResults.Problem(
            statusCode: statusCode,
            title: statusCode switch
            {
                StatusCodes.Status404NotFound => "We could not match this staff record.",
                StatusCodes.Status409Conflict => "Account activation is not available.",
                StatusCodes.Status429TooManyRequests => "Verification requests are temporarily limited.",
                _ => "Account activation could not be completed."
            },
            detail: detail,
            type: $"https://api.csir.example/problems/{code.Replace('_', '-')}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["errorCode"] = code,
                ["errors"] = errors
            });

    private static async Task ProvisionVerifiedLegacyLoginIdentifiersAsync(
        User user,
        SpmeDbContext db,
        DateTimeOffset verifiedAt,
        CancellationToken ct)
    {
        if (!user.EmployeeId.HasValue)
            return;

        var employee = await db.Employees.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == user.EmployeeId.Value, ct);
        if (employee is null)
            return;

        var staffId = LoginIdentifierNormalizer.Normalize(employee.StaffId);
        var employeeEmail = LoginIdentifierNormalizer.Normalize(employee.PrimaryEmail);
        var userEmail = LoginIdentifierNormalizer.Normalize(user.Email);
        var employeePhone = LoginIdentifierNormalizer.Normalize(employee.Phone);
        var userPhone = LoginIdentifierNormalizer.Normalize(user.PhoneNumber);

        var candidates = new List<(string Type, string Value)>();
        if (staffId is { Type: "staff-id" })
            candidates.Add(staffId.Value);
        if (user.EmailConfirmed && employeeEmail is { Type: "email" } && userEmail == employeeEmail)
            candidates.Add(employeeEmail.Value);
        if (user.PhoneNumberConfirmed && employeePhone is { Type: "phone" } && userPhone == employeePhone)
            candidates.Add(employeePhone.Value);

        foreach (var candidate in candidates)
        {
            if (await CountEmployeeIdentifierMatchesAsync(candidate.Type, candidate.Value, db, ct) != 1)
                continue;

            var activeOwner = await db.UserLoginIdentifiers.AsNoTracking()
                .Where(identifier => identifier.IdentifierType == candidate.Type &&
                    identifier.NormalizedValue == candidate.Value && identifier.IsActive)
                .Select(identifier => (Guid?)identifier.UserId)
                .SingleOrDefaultAsync(ct);
            if (activeOwner.HasValue)
                continue;

            db.UserLoginIdentifiers.Add(new UserLoginIdentifier(
                user.Id,
                employee.Id,
                candidate.Type,
                candidate.Value,
                "verified-legacy-password-login",
                verifiedAt));
        }
    }

    private static async Task<int> CountEmployeeIdentifierMatchesAsync(
        string type,
        string normalized,
        SpmeDbContext db,
        CancellationToken ct)
    {
        if (type == "staff-id")
            return await db.Employees.CountAsync(employee => employee.NormalizedStaffId == normalized, ct);
        if (type == "email")
            return await db.Employees.CountAsync(employee => employee.NormalizedPrimaryEmail == normalized, ct);
        return (await db.Employees.AsNoTracking().Where(employee => employee.Phone != null)
                .Select(employee => employee.Phone).ToListAsync(ct))
            .Count(phone => LoginIdentifierNormalizer.NormalizeGhanaPhone(phone) == normalized);
    }

    private static async Task<StatusCodeHttpResult> RequestPasswordResetAsync(
        RequestPasswordResetRequest request,
        IPasswordResetService passwordResetService,
        CancellationToken cancellationToken)
    {
        await passwordResetService.RequestAsync(request.Email, cancellationToken);
        return TypedResults.StatusCode(StatusCodes.Status202Accepted);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfirmPasswordResetAsync(
        ConfirmPasswordResetRequest request,
        IPasswordResetService passwordResetService,
        CancellationToken cancellationToken)
    {
        var result = await passwordResetService.ConfirmAsync(
            request.RequestId,
            request.Token,
            request.NewPassword,
            request.ConfirmNewPassword,
            cancellationToken);
        return result.Succeeded
            ? TypedResults.NoContent()
            : PasswordResetProblem(result);
    }

    private static ProblemHttpResult PasswordResetProblem(PasswordResetConfirmationResult result) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Password reset could not be completed.",
            detail: result.Detail,
            type: $"https://api.csir.example/problems/{result.ErrorCode!.Replace('_', '-')}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = result.ErrorCode,
                ["errorCode"] = result.ErrorCode,
                ["errors"] = result.Errors
            });

    private static bool TryDecodeResetToken(string encodedToken, out string token)
    {
        token = string.Empty;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
            return !string.IsNullOrWhiteSpace(token);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Returns the claims-derived context of the current user.</summary>
    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> GetCurrentUserAsync(
        HttpContext context,
        UserManager<User> userManager)
    {
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return EndpointProblems.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.AccountStatus is not ("active" or "password-reset-required"))
        {
            return EndpointProblems.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        return TypedResults.Ok(new CurrentUserResponse(
            user.Id, user.UserName, user.Email, user.DisplayName, user.PendingEmail, user.IdentityType, user.AccountStatus,
            user.InstituteId, user.EmployeeId, roles.ToList()));
    }

    /// <summary>
    /// Returns a portal-specific self projection from persisted account and employee linkage.
    /// Deliberately does not trust employee or institute claims as a data selector.
    /// </summary>
    private static async Task<Results<Ok<PortalProfileResponse>, ProblemHttpResult>> GetPortalProfileAsync(
        HttpContext context,
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
            return EndpointProblems.Unauthorized();

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.AccountStatus is not ("active" or "password-reset-required"))
            return EndpointProblems.Unauthorized();

        Employee? employee = null;
        if (user.EmployeeId.HasValue)
        {
            employee = await db.Employees.AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == user.EmployeeId.Value, ct);
            if (employee is null || (user.InstituteId.HasValue && employee.InstituteId != user.InstituteId.Value))
                return EndpointProblems.Unauthorized();
        }

        var instituteId = user.InstituteId ?? employee?.InstituteId;
        PortalInstituteResponse? institute = null;
        if (instituteId.HasValue)
        {
            institute = await db.Institutes.AsNoTracking()
                .Where(candidate => candidate.Id == instituteId.Value && candidate.IsActive)
                .Select(candidate => new PortalInstituteResponse(candidate.Id, candidate.Code, candidate.Name))
                .SingleOrDefaultAsync(ct);

            if (institute is null)
                return EndpointProblems.Unauthorized();
        }

        var employment = employee is null
            ? null
            : await db.EmploymentRecords.AsNoTracking()
                .Where(record => record.EmployeeId == employee.Id && record.InstituteId == employee.InstituteId && record.IsCurrent)
                .OrderByDescending(record => record.EffectiveFrom)
                .Select(record => new
                {
                    record.JobTitle,
                    record.StaffCategory,
                    record.LeadershipRoles,
                    record.DivisionId,
                    record.SectionId,
                    record.GradeId
                })
                .FirstOrDefaultAsync(ct);

        string? gradeCode = null;
        string? gradeName = null;
        if (employment?.GradeId is Guid assignedGradeId)
        {
            var grade = await db.Grades.AsNoTracking()
                .Where(item => item.Id == assignedGradeId)
                .Select(item => new { item.Code, item.Name })
                .FirstOrDefaultAsync(ct);
            gradeCode = grade?.Code;
            gradeName = grade?.Name;
        }

        string? divisionName = null;
        string? sectionName = null;
        if (employment?.DivisionId is Guid divisionId)
        {
            divisionName = await db.Divisions.AsNoTracking()
                .Where(division => division.Id == divisionId)
                .Select(division => division.Name)
                .FirstOrDefaultAsync(ct);
        }

        if (employment?.SectionId is Guid sectionId)
        {
            sectionName = await db.Sections.AsNoTracking()
                .Where(section => section.Id == sectionId)
                .Select(section => section.Name)
                .FirstOrDefaultAsync(ct);
        }

        var identityRoles = await userManager.GetRolesAsync(user);
        var leadership = ResolvePortalLeadership(identityRoles, employment?.LeadershipRoles);
        int? profileCompletion = employee is null
            ? null
            : StaffProfileCompletion.Calculate(employee, employment is not null);

        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new PortalProfileResponse(
            user.Id,
            employee?.Id,
            employee?.StaffId,
            BuildPortalDisplayName(user, employee),
            employee?.PreferredName,
            employment?.JobTitle,
            employment?.StaffCategory,
            gradeCode,
            gradeName,
            institute,
            new PortalContactStatusResponse(
                user.Email,
                user.EmailConfirmed,
                user.PhoneNumberConfirmed,
                HasVerifiedLinkedContact(user, employee)),
            user.IdentityType,
            user.AccountStatus,
            await GetEffectivePermissionsAsync(user, userManager, roleManager),
            leadership.Roles,
            leadership.IsHod,
            leadership.IsDirector,
            divisionName,
            sectionName,
            profileCompletion));
    }

    private static bool HasVerifiedLinkedContact(User user, Employee? employee) =>
        employee is not null &&
        employee.IsHrApproved &&
        user.EmployeeId == employee.Id &&
        ((user.EmailConfirmed && !string.IsNullOrWhiteSpace(user.Email)) ||
         (user.PhoneNumberConfirmed && !string.IsNullOrWhiteSpace(user.PhoneNumber)));

    private static readonly char[] LeadershipRoleSeparators = [',', ';'];

    private static (IReadOnlyList<string> Roles, bool IsHod, bool IsDirector) ResolvePortalLeadership(
        IList<string> identityRoles,
        string? employmentLeadershipRoles)
    {
        var roles = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string label)
        {
            if (seen.Add(label))
                roles.Add(label);
        }

        foreach (var role in ParseEmploymentLeadershipRoles(employmentLeadershipRoles))
            Add(FormatLeadershipRoleLabel(role));

        var isHodFromIdentity = identityRoles.Any(role =>
            role.Equals("HeadOfSection", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("HeadOfDivision", StringComparison.OrdinalIgnoreCase));
        var isDirectorFromIdentity = identityRoles.Any(role =>
            role.Equals("Director", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("InstituteDirector", StringComparison.OrdinalIgnoreCase));

        if (isHodFromIdentity && !roles.Any(IsHodLeadershipLabel))
        {
            if (identityRoles.Any(role => role.Equals("HeadOfDivision", StringComparison.OrdinalIgnoreCase)))
                Add("Head of Division");
            else
                Add("Head of Section");
        }

        if (isDirectorFromIdentity && !roles.Any(IsDirectorLeadershipLabel))
            Add("Institute Director");

        var isHod = isHodFromIdentity || roles.Any(IsHodLeadershipLabel);
        var isDirector = isDirectorFromIdentity || roles.Any(IsDirectorLeadershipLabel);
        return (roles, isHod, isDirector);
    }

    private static string[] ParseEmploymentLeadershipRoles(string? roles) =>
        string.IsNullOrWhiteSpace(roles)
            ? []
            : roles.Split(LeadershipRoleSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(role => role.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static string FormatLeadershipRoleLabel(string role)
    {
        var normalized = role.Trim().Replace('_', '-');
        return normalized.ToLowerInvariant() switch
        {
            "head-of-section" or "head of section" or "section head" or "headofsection" => "Head of Section",
            "head-of-division" or "head of division" or "division head" or "headofdivision" => "Head of Division",
            "institute-director" or "institute director" or "director" or "institutedirector" => "Institute Director",
            "deputy director" or "deputy-director" => "Deputy Director",
            "administrative director" or "admin-director" or "administrative-director" => "Administrative Director",
            "corporate head of administration" or "corporate-head-of-administration" => "Corporate Head of Administration",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.Replace('-', ' ').ToLowerInvariant())
        };
    }

    private static bool IsHodLeadershipLabel(string label)
    {
        var value = label.ToLowerInvariant();
        return value.Contains("head of section", StringComparison.Ordinal) ||
               value.Contains("head of division", StringComparison.Ordinal) ||
               value.Contains("section head", StringComparison.Ordinal) ||
               value.Contains("division head", StringComparison.Ordinal);
    }

    private static bool IsDirectorLeadershipLabel(string label)
    {
        var value = label.ToLowerInvariant();
        return value.Contains("director", StringComparison.Ordinal);
    }

    private static string BuildPortalDisplayName(User user, Employee? employee)
    {
        if (!string.IsNullOrWhiteSpace(employee?.PreferredName))
            return employee.PreferredName;

        var userDisplayName = user.DisplayName?.Trim();
        var userName = user.UserName?.Trim();
        var userEmail = user.Email?.Trim();
        if (!string.IsNullOrWhiteSpace(userDisplayName) &&
            !string.Equals(userDisplayName, userName, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(userDisplayName, userEmail, StringComparison.OrdinalIgnoreCase))
        {
            return userDisplayName;
        }

        if (employee is not null)
        {
            var name = string.Join(' ', new[] { employee.Prefix, employee.OtherNames, employee.Surname }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        return string.IsNullOrWhiteSpace(userDisplayName) ? "Staff member" : userDisplayName;
    }

    private static async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        User user,
        UserManager<User> userManager,
        RoleManager<Role> roleManager)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in await userManager.GetClaimsAsync(user))
        {
            if (claim.Type == "permission" && !string.IsNullOrWhiteSpace(claim.Value))
                permissions.Add(claim.Value);
        }

        foreach (var roleName in await userManager.GetRolesAsync(user))
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
                continue;
            foreach (var claim in await roleManager.GetClaimsAsync(role))
            {
                if (claim.Type == "permission" && !string.IsNullOrWhiteSpace(claim.Value))
                    permissions.Add(claim.Value);
            }
        }

        return permissions.OrderBy(permission => permission, StringComparer.Ordinal).ToArray();
    }

    private static async Task<IResult> UpdateMyProfileAsync(
        UpdateMyProfileRequest request, HttpContext context, UserManager<User> userManager,
        IEmailService emailService, IConfiguration configuration, IAuditService audit, CancellationToken ct)
    {
        var user = await GetAuthenticatedUserAsync(context, userManager);
        if (user is null) return EndpointProblems.Unauthorized();
        var before = JsonSerializer.Serialize(new { user.DisplayName, user.Email, user.PendingEmail });
        user.UpdateDisplayName(request.DisplayName);
        var requestedEmail = request.Email?.Trim();
        if (!string.IsNullOrWhiteSpace(requestedEmail) && !string.Equals(requestedEmail, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await userManager.FindByEmailAsync(requestedEmail);
            var pendingForAnotherUser = await userManager.Users.AnyAsync(candidate => candidate.Id != user.Id && candidate.PendingEmail == requestedEmail, ct);
            if ((existing is not null && existing.Id != user.Id) || pendingForAnotherUser)
                return EndpointProblems.FromError(Error.Conflict("That email address is already in use."));
            user.RequestEmailChange(requestedEmail);
            var token = await userManager.GenerateChangeEmailTokenAsync(user, requestedEmail);
            var verifyUrl = BuildEmailChangeUrl(configuration, token);
            await emailService.SendAsync(requestedEmail, "Confirm your CSIR SPME email address",
                $"Confirm your email change using this secure link: {verifyUrl}", ct: ct);
        }
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded) return EndpointProblems.FromError(Error.Validation(string.Join("; ", updated.Errors.Select(x => x.Description))));
        await audit.RecordAndSaveAsync("settings.profile-updated", "User", user.Id.ToString(), before,
            JsonSerializer.Serialize(new { user.DisplayName, user.Email, user.PendingEmail }), ct);
        return await MapCurrentUserAsync(user, userManager);
    }

    private static async Task<IResult> ConfirmMyEmailAsync(ConfirmMyEmailRequest request, HttpContext context,
        UserManager<User> userManager, IAuditService audit, CancellationToken ct)
    {
        var user = await GetAuthenticatedUserAsync(context, userManager);
        if (user is null) return EndpointProblems.Unauthorized();
        if (string.IsNullOrWhiteSpace(user.PendingEmail) || !TryDecodeResetToken(request.Token, out var token))
            return EndpointProblems.FromError(Error.Validation("The email verification request is invalid or has expired."));
        var result = await userManager.ChangeEmailAsync(user, user.PendingEmail, token);
        if (!result.Succeeded) return EndpointProblems.FromError(Error.Validation("The email verification request is invalid or has expired."));
        user.CompleteEmailChange();
        await userManager.UpdateAsync(user);
        await audit.RecordAndSaveAsync("settings.email-verified", "User", user.Id.ToString(), null, "email-confirmed", ct);
        return await MapCurrentUserAsync(user, userManager);
    }

    private static async Task<IResult> ChangeMyPasswordAsync(ChangeMyPasswordRequest request, HttpContext context,
        UserManager<User> userManager, SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var user = await GetAuthenticatedUserAsync(context, userManager);
        if (user is null) return EndpointProblems.Unauthorized();
        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
            return EndpointProblems.FromError(Error.Validation("The new password confirmation does not match."));
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return EndpointProblems.FromError(Error.Validation(string.Join("; ", result.Errors.Select(x => x.Description))));
        await RevokeRefreshTokensAsync(user.Id, "password-changed", db, ct);
        await audit.RecordAndSaveAsync("settings.password-changed", "User", user.Id.ToString(), null, "security-stamp-rotated", ct);
        return TypedResults.NoContent();
    }

    private static async Task RevokeRefreshTokensAsync(
        Guid userId,
        string reason,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var userTokens = db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null);
        var activeTokens = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? (await userTokens.ToListAsync(ct)).Where(token => token.ExpiresAt > now).ToList()
            : await userTokens.Where(token => token.ExpiresAt > now).ToListAsync(ct);
        foreach (var token in activeTokens)
            token.Revoke(reason, now);
        var sessions = await db.UserSessions.Where(session =>
            session.UserId == userId && session.RevokedAt == null).ToListAsync(ct);
        foreach (var session in sessions) session.Revoke(now);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<IResult> GetNotificationPreferencesAsync(HttpContext context, UserManager<User> userManager, SpmeDbContext db, CancellationToken ct)
    {
        var user = await GetAuthenticatedUserAsync(context, userManager);
        if (user is null) return EndpointProblems.Unauthorized();
        var preference = await db.NotificationPreferences.FindAsync([user.Id], ct);
        if (preference is null) { preference = new NotificationPreference(user.Id); db.NotificationPreferences.Add(preference); await db.SaveChangesAsync(ct); }
        return TypedResults.Ok(Map(preference));
    }

    private static async Task<IResult> UpdateNotificationPreferencesAsync(UpdateNotificationPreferenceRequest request, HttpContext context,
        UserManager<User> userManager, SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var user = await GetAuthenticatedUserAsync(context, userManager);
        if (user is null) return EndpointProblems.Unauthorized();
        var preference = await db.NotificationPreferences.FindAsync([user.Id], ct);
        if (preference is null) { preference = new NotificationPreference(user.Id); db.NotificationPreferences.Add(preference); }
        preference.Update(request.EmailAlerts, request.LeaveReminders, request.PromotionUpdates, request.SystemAnnouncements);
        await db.SaveChangesAsync(ct);
        await audit.RecordAndSaveAsync("settings.notification-preferences-updated", "NotificationPreference", user.Id.ToString(), null, "email-preferences-updated", ct);
        return TypedResults.Ok(Map(preference));
    }

    private static NotificationPreferenceResponse Map(NotificationPreference preference) => new(preference.EmailAlerts, preference.LeaveReminders, preference.PromotionUpdates, preference.SystemAnnouncements);

    private static async Task<IResult> MapCurrentUserAsync(User user, UserManager<User> userManager) => TypedResults.Ok(new CurrentUserResponse(user.Id, user.UserName, user.Email, user.DisplayName, user.PendingEmail, user.IdentityType, user.AccountStatus, user.InstituteId, user.EmployeeId, (await userManager.GetRolesAsync(user)).ToList()));

    private static async Task<User?> GetAuthenticatedUserAsync(HttpContext context, UserManager<User> userManager)
        => Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? await userManager.FindByIdAsync(id.ToString()) : null;

    private static string BuildEmailChangeUrl(IConfiguration configuration, string token)
    {
        var baseUrl = configuration.GetValue<string>("EmailChange:Url") ?? "http://localhost:5173/settings";
        return QueryHelpers.AddQueryString(baseUrl, "token", WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>Lists system user accounts using bounded page-based pagination.</summary>
    private static async Task<Ok<PageResponse<SystemUserResponse>>> GetSystemUsersAsync(
        SpmeDbContext db,
        HttpContext context,
        string? identityType,
        string? accountStatus,
        Guid? instituteId,
        string? role,
        string? search,
        bool includeEmployees = false,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = ApplySystemUserAccessScope(db.Users.AsNoTracking(), context, includeEmployees);
        if (!string.IsNullOrWhiteSpace(identityType))
        {
            var normalizedIdentityType = identityType.Trim().ToUpperInvariant();
            query = query.Where(user => user.IdentityType.ToUpper() == normalizedIdentityType);
        }
        if (!string.IsNullOrWhiteSpace(accountStatus))
        {
            var normalizedAccountStatus = accountStatus.Trim().ToUpperInvariant();
            query = query.Where(user => user.AccountStatus.ToUpper() == normalizedAccountStatus);
        }
        if (instituteId.HasValue)
        {
            query = query.Where(user => user.InstituteId == instituteId.Value);
        }
        if (!string.IsNullOrWhiteSpace(role))
        {
            var normalizedRole = role.Trim().ToUpperInvariant();
            query = query.Where(user => db.UserRoles.Any(userRole =>
                userRole.UserId == user.Id &&
                db.Roles.Any(candidateRole =>
                    candidateRole.Id == userRole.RoleId &&
                    ((candidateRole.NormalizedName != null && candidateRole.NormalizedName == normalizedRole) ||
                     candidateRole.Code.ToUpper() == normalizedRole))));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(user =>
                (user.NormalizedUserName != null && user.NormalizedUserName.Contains(normalizedSearch)) ||
                (user.NormalizedEmail != null && user.NormalizedEmail.Contains(normalizedSearch)));
        }

        var total = await query.CountAsync(cancellationToken);
        var users = await query.OrderBy(user => user.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new SystemUserProjection(
                user.Id,
                user.UserName,
                user.Email,
                user.AccountStatus,
                user.IdentityType,
                user.InstituteId,
                user.EmployeeId,
                user.LastLoginAt))
            .ToListAsync(cancellationToken);
        var items = await MapSystemUsersAsync(users, db, cancellationToken);
        return TypedResults.Ok(new PageResponse<SystemUserResponse>(items, total, page, pageSize));
    }

    /// <summary>Gets one system user account by identifier.</summary>
    private static async Task<Results<Ok<SystemUserResponse>, ProblemHttpResult>> GetSystemUserAsync(
        Guid id,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken,
        bool includeEmployees = false)
    {
        var user = await ApplySystemUserAccessScope(db.Users.AsNoTracking(), context, includeEmployees)
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new SystemUserProjection(
                candidate.Id,
                candidate.UserName,
                candidate.Email,
                candidate.AccountStatus,
                candidate.IdentityType,
                candidate.InstituteId,
                candidate.EmployeeId,
                candidate.LastLoginAt))
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
            return EndpointProblems.FromError(Error.NotFound("System user not found."));

        var mapped = await MapSystemUsersAsync([user], db, cancellationToken);
        return TypedResults.Ok(mapped[0]);
    }

    private static async Task<Results<Ok<SystemUserResponse>, ProblemHttpResult>> UpdateSystemUserRolesAsync(
        Guid id,
        UpdateSystemUserRolesRequest request,
        UserManager<User> userManager,
        SpmeDbContext db,
        HttpContext context,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || IsEmployeeIdentity(user) || !CanAccessSystemUser(context, user))
            return EndpointProblems.FromError(Error.NotFound("System user not found."));

        var requestedRoles = request.Roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (requestedRoles.Count == 0)
            return EndpointProblems.FromError(Error.Validation("At least one role is required."));
        if (requestedRoles.Any(role => string.Equals(role, SpmeRoles.Employee, StringComparison.OrdinalIgnoreCase)))
            return EndpointProblems.FromError(Error.Validation("Employee roles cannot be managed through System Users."));
        if (!IsPlatformAdmin(context) && requestedRoles.Any(role => string.Equals(role, SpmeRoles.PlatformAdmin, StringComparison.OrdinalIgnoreCase)))
            return EndpointProblems.FromError(Error.Validation("Institute administrators cannot assign the PlatformAdmin role."));
        if (requestedRoles.Any(InstituteStaffAccess.IsInstituteScopedManagementRole) && !user.InstituteId.HasValue)
            return EndpointProblems.FromError(Error.Validation(
                "Assign an institute before granting HrAdmin or InstituteAdmin."));

        var normalizedRequestedRoles = requestedRoles.Select(role => role.ToUpperInvariant()).ToArray();
        var existingRoleRows = await db.Roles
            .Where(role => normalizedRequestedRoles.Contains((role.NormalizedName ?? role.Name ?? role.Code).ToUpper()) ||
                           normalizedRequestedRoles.Contains(role.Code.ToUpper()))
            .Select(role => new { role.Code, Name = role.Name ?? role.Code })
            .ToListAsync(cancellationToken);
        var existingRoles = existingRoleRows
            .Select(role => role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var missingRoles = requestedRoles
            .Where(requestedRole => !existingRoleRows.Any(existingRole =>
                string.Equals(existingRole.Name, requestedRole, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existingRole.Code, requestedRole, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (missingRoles.Length > 0)
            return EndpointProblems.FromError(Error.Validation($"Unknown role: {missingRoles[0]}."));

        var beforeRoles = (await userManager.GetRolesAsync(user))
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!IsPlatformAdmin(context) && beforeRoles.Contains(SpmeRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase))
            return EndpointProblems.FromError(Error.NotFound("System user not found."));
        var rolesToRemove = beforeRoles.Except(existingRoles, StringComparer.OrdinalIgnoreCase).ToArray();
        var rolesToAdd = existingRoles.Except(beforeRoles, StringComparer.OrdinalIgnoreCase).ToArray();

        if (rolesToRemove.Length > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
                return EndpointProblems.FromError(Error.Validation(string.Join("; ", removeResult.Errors.Select(error => error.Description))));
        }
        if (rolesToAdd.Length > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
                return EndpointProblems.FromError(Error.Validation(string.Join("; ", addResult.Errors.Select(error => error.Description))));
        }

        if (rolesToRemove.Length > 0 || rolesToAdd.Length > 0)
        {
            var stampResult = await userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
                return EndpointProblems.FromError(Error.Validation(string.Join("; ", stampResult.Errors.Select(error => error.Description))));
            await RevokeRefreshTokensAsync(user.Id, "roles-changed", db, cancellationToken);
        }

        var afterRoles = (await userManager.GetRolesAsync(user))
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToList();
        await audit.RecordAndSaveAsync(
            "system-users.roles-updated",
            "User",
            user.Id.ToString(),
            JsonSerializer.Serialize(new { roles = beforeRoles }),
            JsonSerializer.Serialize(new { roles = afterRoles }),
            cancellationToken);

        var projection = await ProjectSystemUserAsync(user.Id, db, context, includeEmployees: false, cancellationToken);
        return TypedResults.Ok((await MapSystemUsersAsync([projection!], db, cancellationToken))[0]);
    }

    private static async Task<Results<Ok<SystemUserResponse>, ProblemHttpResult>> UpdateSystemUserInstituteAsync(
        Guid id,
        UpdateSystemUserInstituteRequest request,
        UserManager<User> userManager,
        SpmeDbContext db,
        HttpContext context,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || IsEmployeeIdentity(user) || !CanAccessSystemUser(context, user))
            return EndpointProblems.FromError(Error.NotFound("System user not found."));

        var roles = await userManager.GetRolesAsync(user);
        var requiresInstitute =
            roles.Any(InstituteStaffAccess.IsInstituteScopedManagementRole) ||
            InstituteStaffAccess.IsStaffUserIdentityType(user.IdentityType) ||
            InstituteStaffAccess.HasLegacyStaffManagementRole(roles);

        if (!request.InstituteId.HasValue)
        {
            if (requiresInstitute || !InstituteStaffAccess.IsPlatformAdminIdentityType(user.IdentityType))
                return EndpointProblems.FromError(Error.Validation(
                    "Clearing institute is only allowed for PlatformAdmin accounts that do not hold institute-scoped staff-management roles."));

            var beforeClear = user.InstituteId;
            user.ClearInstitute();
            var clearUpdate = await userManager.UpdateAsync(user);
            if (!clearUpdate.Succeeded)
                return EndpointProblems.FromError(Error.Validation(string.Join("; ", clearUpdate.Errors.Select(error => error.Description))));

            var clearStamp = await userManager.UpdateSecurityStampAsync(user);
            if (!clearStamp.Succeeded)
                return EndpointProblems.FromError(Error.Validation(string.Join("; ", clearStamp.Errors.Select(error => error.Description))));
            await RevokeRefreshTokensAsync(user.Id, "institute-cleared", db, cancellationToken);

            await audit.RecordAndSaveAsync(
                "system-users.institute-updated",
                "User",
                user.Id.ToString(),
                JsonSerializer.Serialize(new { instituteId = beforeClear }),
                JsonSerializer.Serialize(new { instituteId = (Guid?)null }),
                cancellationToken);

            var clearedProjection = await ProjectSystemUserAsync(user.Id, db, context, includeEmployees: false, cancellationToken);
            return TypedResults.Ok((await MapSystemUsersAsync([clearedProjection!], db, cancellationToken))[0]);
        }

        var institute = await db.Institutes.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.InstituteId.Value && candidate.IsActive, cancellationToken);
        if (institute is null)
            return EndpointProblems.FromError(Error.Validation("An active institute is required."));

        var beforeInstituteId = user.InstituteId;
        // Preserve IdentityType (e.g. StaffUser); never rewrite to HrAdmin as a side effect.
        user.AssignInstitute(institute.Id);
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return EndpointProblems.FromError(Error.Validation(string.Join("; ", updateResult.Errors.Select(error => error.Description))));

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
            return EndpointProblems.FromError(Error.Validation(string.Join("; ", stampResult.Errors.Select(error => error.Description))));
        await RevokeRefreshTokensAsync(user.Id, "institute-changed", db, cancellationToken);

        await audit.RecordAndSaveAsync(
            "system-users.institute-updated",
            "User",
            user.Id.ToString(),
            JsonSerializer.Serialize(new { instituteId = beforeInstituteId }),
            JsonSerializer.Serialize(new { instituteId = user.InstituteId, instituteCode = institute.Code }),
            cancellationToken);

        var projection = await ProjectSystemUserAsync(user.Id, db, context, includeEmployees: false, cancellationToken);
        return TypedResults.Ok((await MapSystemUsersAsync([projection!], db, cancellationToken))[0]);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteSystemUserAsync(
        Guid id,
        UserManager<User> userManager,
        SpmeDbContext db,
        HttpContext context,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId) && currentUserId == id)
            return EndpointProblems.FromError(Error.Conflict("You cannot delete your own system user account."));

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || IsEmployeeIdentity(user))
            return EndpointProblems.FromError(Error.NotFound("System user not found."));

        var beforeRoles = await userManager.GetRolesAsync(user);
        if (beforeRoles.Contains(SpmeRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase))
        {
            var platformRoleId = await db.Roles.AsNoTracking()
                .Where(role => role.NormalizedName == SpmeRoles.PlatformAdmin.ToUpper() || role.Code.ToUpper() == SpmeRoles.PlatformAdmin.ToUpper())
                .Select(role => (Guid?)role.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var platformAdminCount = platformRoleId.HasValue
                ? await db.UserRoles.CountAsync(userRole => userRole.RoleId == platformRoleId.Value, cancellationToken)
                : 0;
            if (platformAdminCount <= 1)
                return EndpointProblems.FromError(Error.Conflict("The last PlatformAdmin account cannot be deleted."));
        }

        var before = JsonSerializer.Serialize(new
        {
            user.Id,
            user.UserName,
            user.Email,
            user.IdentityType,
            Roles = beforeRoles.OrderBy(role => role).ToList()
        });
        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return EndpointProblems.FromError(Error.Validation(string.Join("; ", result.Errors.Select(error => error.Description))));

        await audit.RecordAndSaveAsync("system-users.deleted", "User", id.ToString(), before, null, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<EmailRecipientsResponse>> GetEmailRecipientsAsync(
        EmailRecipientsRequest request,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var recipients = await ResolveEmailRecipientsAsync(request.UserIds, request.Roles, request.Status, db, context, cancellationToken);
        return TypedResults.Ok(new EmailRecipientsResponse(recipients, recipients.Count));
    }

    private static async Task<Results<Ok<BulkEmailResponse>, ProblemHttpResult>> SendBulkEmailAsync(
        BulkEmailRequest request,
        SpmeDbContext db,
        HttpContext context,
        IEmailService emailService,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
            return EndpointProblems.FromError(Error.Validation("Email subject and body are required."));

        var recipients = await ResolveEmailRecipientsAsync(request.UserIds, request.Roles, request.Status, db, context, cancellationToken);
        var sent = 0;
        var skipped = 0;
        foreach (var recipient in recipients)
        {
            try
            {
                await emailService.SendAsync(
                    recipient.Email,
                    request.Subject.Trim(),
                    PersonalizeEmailBody(request.Body, recipient),
                    request.IsHtml,
                    cancellationToken);
                sent++;
            }
            catch when (!cancellationToken.IsCancellationRequested)
            {
                skipped++;
            }
        }

        await audit.RecordAndSaveAsync(
            "system-users.bulk-email-sent",
            "SystemUserEmail",
            before: null,
            after: JsonSerializer.Serialize(new { request.Roles, request.Status, RecipientCount = recipients.Count, sent, skipped }),
            ct: cancellationToken);
        return TypedResults.Ok(new BulkEmailResponse(sent, skipped));
    }

    private static string PersonalizeEmailBody(string body, EmailRecipientResponse recipient)
    {
        var recipientName = string.IsNullOrWhiteSpace(recipient.UserName) ? recipient.Email : recipient.UserName.Trim();
        return body.Replace("{name}", recipientName, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Ok<SendHrPortalLinksResponse>> SendHrPortalLinksAsync(
        SendHrPortalLinksRequest request,
        SpmeDbContext db,
        HttpContext context,
        IPasswordResetService passwordResetService,
        CancellationToken cancellationToken)
    {
        var roles = request.Roles is { Count: > 0 }
            ? request.Roles
            : [SpmeRoles.PlatformAdmin, SpmeRoles.InstituteAdmin, SpmeRoles.HrAdmin];
        var recipients = await ResolveEmailRecipientsAsync(request.UserIds, roles, request.Status, db, context, cancellationToken);
        var sent = 0;
        var skipped = 0;
        foreach (var recipient in recipients)
        {
            try
            {
                await passwordResetService.RequestAsync(recipient.Email, cancellationToken);
                sent++;
            }
            catch when (!cancellationToken.IsCancellationRequested)
            {
                skipped++;
            }
        }

        return TypedResults.Ok(new SendHrPortalLinksResponse(sent, skipped));
    }

    private static async Task<Ok<LoginLockResponse>> GetLoginLockAsync(
        SpmeDbContext db,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(new LoginLockResponse(await IsLoginLockedAsync(db, cancellationToken)));
    }

    private static async Task<Ok<LoginLockResponse>> UpdateLoginLockAsync(
        UpdateLoginLockRequest request,
        SpmeDbContext db,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var setting = await db.AppSettings.FirstOrDefaultAsync(candidate => candidate.Key == LoginLockSettingKey, cancellationToken);
        var before = setting?.Value ?? "false";
        var after = request.IsLocked ? "true" : "false";
        if (setting is null)
        {
            setting = new AppSetting(LoginLockSettingKey, after);
            db.AppSettings.Add(setting);
        }
        else
        {
            setting.Update(after);
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAndSaveAsync("system-users.login-lock-updated", "AppSetting", LoginLockSettingKey, before, after, cancellationToken);
        return TypedResults.Ok(new LoginLockResponse(request.IsLocked));
    }

    private static async Task<List<SystemUserResponse>> MapSystemUsersAsync(
        IReadOnlyList<SystemUserProjection> users,
        SpmeDbContext db,
        CancellationToken cancellationToken)
    {
        if (users.Count == 0)
            return [];

        var userIds = users.Select(user => user.Id).ToArray();
        var roleRows = await db.UserRoles.AsNoTracking()
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Join(
                db.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, RoleName = role.Name ?? role.Code })
            .ToListAsync(cancellationToken);
        var rolesByUserId = roleRows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(row => row.RoleName).OrderBy(role => role).ToList());

        var employeeIds = users
            .Where(user => user.EmployeeId.HasValue)
            .Select(user => user.EmployeeId!.Value)
            .Distinct()
            .ToArray();
        var employeesById = await db.Employees.AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.Id))
            .Select(employee => new SystemUserEmployeeSummary(
                employee.Id,
                employee.StaffId,
                employee.Prefix,
                employee.Surname,
                employee.OtherNames,
                employee.ProfileStatus))
            .ToDictionaryAsync(employee => employee.Id, cancellationToken);

        var instituteIds = users
            .Where(user => user.InstituteId.HasValue)
            .Select(user => user.InstituteId!.Value)
            .Distinct()
            .ToArray();
        var institutesById = await db.Institutes.AsNoTracking()
            .Where(institute => instituteIds.Contains(institute.Id))
            .Select(institute => new EmployeeInstituteSummary(
                institute.Id,
                institute.Code,
                institute.Name,
                institute.Kind))
            .ToDictionaryAsync(institute => institute.Id, cancellationToken);

        return users
            .Select(user => new SystemUserResponse(
                user.Id,
                user.UserName,
                user.Email,
                user.AccountStatus,
                user.IdentityType,
                user.InstituteId,
                user.EmployeeId,
                rolesByUserId.GetValueOrDefault(user.Id, []),
                user.LastLoginAt,
                user.EmployeeId.HasValue ? employeesById.GetValueOrDefault(user.EmployeeId.Value) : null,
                user.InstituteId.HasValue ? institutesById.GetValueOrDefault(user.InstituteId.Value) : null))
            .ToList();
    }

    /// <summary>Lists all configured application roles.</summary>
    private static async Task<Ok<CollectionResponse<RoleResponse>>> GetRolesAsync(
        SpmeDbContext db,
        CancellationToken cancellationToken)
    {
        var items = await db.Roles.AsNoTracking()
            .OrderBy(role => role.Code)
            .Select(role => new RoleResponse(role.Id, role.Code, role.Name, role.Description, role.IsSystemRole))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(new CollectionResponse<RoleResponse>(items, items.Count));
    }

    private static IQueryable<User> ApplySystemUserAccessScope(
        IQueryable<User> query,
        HttpContext context,
        bool includeEmployees)
    {
        var systemUserQuery = includeEmployees
            ? query
            : query.Where(user => user.IdentityType != SpmeRoles.Employee);

        if (IsPlatformAdmin(context))
            return systemUserQuery;

        var instituteId = ReadInstituteId(context);
        return instituteId.HasValue
            ? systemUserQuery.Where(user => user.InstituteId == instituteId.Value)
            : systemUserQuery.Where(_ => false);
    }

    private static bool CanAccessSystemUser(HttpContext context, User user) =>
        IsPlatformAdmin(context) ||
        (ReadInstituteId(context).HasValue && user.InstituteId == ReadInstituteId(context));

    private static bool IsPlatformAdmin(HttpContext context) =>
        context.User.IsInRole(SpmeRoles.PlatformAdmin);

    private static Guid? ReadInstituteId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirstValue("institute_id"), out var instituteId) ? instituteId : null;

    private static bool IsEmployeeIdentity(User user) =>
        string.Equals(user.IdentityType, SpmeRoles.Employee, StringComparison.OrdinalIgnoreCase);

    private static async Task<SystemUserProjection?> ProjectSystemUserAsync(
        Guid id,
        SpmeDbContext db,
        HttpContext context,
        bool includeEmployees,
        CancellationToken cancellationToken)
    {
        return await ApplySystemUserAccessScope(db.Users.AsNoTracking(), context, includeEmployees)
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new SystemUserProjection(
                candidate.Id,
                candidate.UserName,
                candidate.Email,
                candidate.AccountStatus,
                candidate.IdentityType,
                candidate.InstituteId,
                candidate.EmployeeId,
                candidate.LastLoginAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<List<EmailRecipientResponse>> ResolveEmailRecipientsAsync(
        IReadOnlyList<Guid>? userIds,
        IReadOnlyList<string>? roles,
        string? status,
        SpmeDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var query = ApplySystemUserAccessScope(db.Users.AsNoTracking(), context, includeEmployees: false)
            .Where(user => user.Email != null && user.Email != "");
        if (userIds is { Count: > 0 })
        {
            var requestedUserIds = userIds.Distinct().ToArray();
            query = query.Where(user => requestedUserIds.Contains(user.Id));
        }
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(user => user.AccountStatus.ToUpper() == normalizedStatus);
        }
        if (roles is { Count: > 0 })
        {
            var normalizedRoles = roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim().ToUpperInvariant())
                .Distinct()
                .ToArray();
            query = query.Where(user => db.UserRoles.Any(userRole =>
                userRole.UserId == user.Id &&
                db.Roles.Any(role =>
                    role.Id == userRole.RoleId &&
                    ((role.NormalizedName != null && normalizedRoles.Contains(role.NormalizedName)) ||
                     normalizedRoles.Contains(role.Code.ToUpper())))));
        }

        var users = await query
            .OrderBy(user => user.UserName)
            .Select(user => new SystemUserProjection(
                user.Id,
                user.UserName,
                user.Email,
                user.AccountStatus,
                user.IdentityType,
                user.InstituteId,
                user.EmployeeId,
                user.LastLoginAt))
            .ToListAsync(cancellationToken);
        var mapped = await MapSystemUsersAsync(users, db, cancellationToken);
        return mapped
            .Where(user => !string.IsNullOrWhiteSpace(user.Email))
            .Select(user => new EmailRecipientResponse(
                user.Id,
                user.UserName,
                user.Email!,
                user.Roles,
                user.AccountStatus,
                user.Institute))
            .ToList();
    }

    private static async Task<bool> IsLoginLockedAsync(SpmeDbContext db, CancellationToken cancellationToken)
    {
        var value = await db.AppSettings.AsNoTracking()
            .Where(setting => setting.Key == LoginLockSettingKey)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);
        return bool.TryParse(value, out var isLocked) && isLocked;
    }

    private static AuthenticatedUserResponse MapUser(User user) => new(
        user.Id, user.UserName, user.Email, user.IdentityType, user.AccountStatus,
        user.InstituteId, user.EmployeeId);

    private sealed record SystemUserProjection(
        Guid Id,
        string? UserName,
        string? Email,
        string AccountStatus,
        string IdentityType,
        Guid? InstituteId,
        Guid? EmployeeId,
        DateTimeOffset? LastLoginAt);
}
