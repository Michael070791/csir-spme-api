using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Csir.Spme.Api.Auth;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddJwtBearerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtSecretOptions>()
            .Bind(configuration.GetSection("Jwt"))
            .Validate(options => SecretConfiguration.IsStrongSecret(options.Key),
                "Jwt:Key must come from a secret provider, contain at least 32 UTF-8 bytes, and must not be a placeholder.")
            .ValidateOnStart();
        services.AddOptions<AccountActivationSecretOptions>()
            .Bind(configuration.GetSection("AccountActivation"))
            .Validate(options => SecretConfiguration.IsStrongSecret(options.HashKey),
                "AccountActivation:HashKey must come from a secret provider, contain at least 32 UTF-8 bytes, and must not be a placeholder.")
            .ValidateOnStart();

        services.AddIdentityCore<User>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddRoles<Role>()
        .AddEntityFrameworkStores<SpmeDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(24));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<IConfiguration>((options, currentConfiguration) =>
        {
            var jwtSection = currentConfiguration.GetSection("Jwt");
            var issuer = jwtSection.GetValue<string>("Issuer") ?? "csir-spme-api";
            var audience = jwtSection.GetValue<string>("Audience") ?? "csir-spme-client";
            var key = SecretConfiguration.RequireStrongSecret(currentConfiguration, "Jwt:Key");
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();
                    var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    var tokenStamp = context.Principal?.FindFirst("security_stamp")?.Value;
                    var sessionId = context.Principal?.FindFirst("sid")?.Value;
                    var user = Guid.TryParse(userId, out var id) ? await userManager.FindByIdAsync(id.ToString()) : null;
                    if (user is null || string.IsNullOrEmpty(tokenStamp) || !string.Equals(tokenStamp, user.SecurityStamp, StringComparison.Ordinal))
                    {
                        context.Fail("The session is no longer valid.");
                        return;
                    }

                    if (!Guid.TryParse(sessionId, out var sid))
                    {
                        context.Fail("The session is no longer valid.");
                        return;
                    }

                    var db = context.HttpContext.RequestServices.GetRequiredService<SpmeDbContext>();
                    var activeSession = await db.UserSessions.AsNoTracking()
                        .AnyAsync(session => session.Id == sid && session.UserId == user.Id && session.RevokedAt == null,
                            context.HttpContext.RequestAborted);
                    if (!activeSession)
                        context.Fail("The session is no longer valid.");
                },
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrWhiteSpace(accessToken) &&
                        path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.PlatformAdmin, policy =>
                policy.RequireRole(SpmeRoles.PlatformAdmin))
            .AddPolicy(AuthorizationPolicies.ReadUsers, policy =>
                policy.RequireAssertion(ctx => HasAnyRole(ctx, SpmeRoles.PlatformAdmin, SpmeRoles.InstituteAdmin)))
            .AddPolicy(AuthorizationPolicies.ManageUsers, policy =>
                policy.RequireRole(SpmeRoles.PlatformAdmin))
            .AddPolicy(AuthorizationPolicies.ManageScopedUsers, policy =>
                policy.RequireAssertion(ctx => HasAnyRole(ctx, SpmeRoles.PlatformAdmin, SpmeRoles.InstituteAdmin)))
            .AddPolicy(AuthorizationPolicies.ReadRoles, policy =>
                policy.RequireAssertion(ctx => HasAnyRole(ctx, SpmeRoles.PlatformAdmin, SpmeRoles.InstituteAdmin)))
            .AddPolicy(AuthorizationPolicies.ReadHumanResources, policy =>
                policy.RequireAssertion(ctx =>
                    HasAnyRole(ctx, SpmeRoles.PlatformAdmin, SpmeRoles.InstituteAdmin, SpmeRoles.HrAdmin) ||
                    InstituteStaffAccess.HasStaffManagementReadCompatibility(ctx.User)))
            .AddPolicy(AuthorizationPolicies.ManageHumanResources, policy =>
                policy.RequireAssertion(ctx =>
                    HasPermissionOrRole(ctx, SpmePermissions.EmployeesWrite, SpmeRoles.PlatformAdmin, SpmeRoles.HrAdmin) ||
                    InstituteStaffAccess.HasStaffManagementWriteCompatibility(ctx.User)))
            .AddPolicy(AuthorizationPolicies.ReadHrDashboard, policy =>
                policy.RequireAssertion(ctx => InstituteStaffAccess.CanReadInstituteHr(ctx.User)))
            .AddPolicy(AuthorizationPolicies.ReadProfileImages, policy =>
                policy.RequireAssertion(ctx =>
                    HasAnyRole(ctx, SpmeRoles.PlatformAdmin, SpmeRoles.InstituteAdmin, SpmeRoles.HrAdmin, SpmeRoles.Employee) ||
                    InstituteStaffAccess.HasStaffManagementReadCompatibility(ctx.User)))
            .AddPolicy(AuthorizationPolicies.ManageProfileImages, policy =>
                policy.RequireAssertion(ctx =>
                    HasAnyRole(ctx, SpmeRoles.PlatformAdmin, SpmeRoles.HrAdmin, SpmeRoles.Employee) ||
                    InstituteStaffAccess.HasStaffManagementWriteCompatibility(ctx.User)))
            .AddPolicy(AuthorizationPolicies.ReadPromotions, policy =>
                policy.RequireAssertion(ctx =>
                    HasAnyRole(ctx, SpmeRoles.PlatformAdmin, SpmeRoles.InstituteAdmin, SpmeRoles.HrAdmin) ||
                    InstituteStaffAccess.HasStaffManagementReadCompatibility(ctx.User)))
            .AddPolicy(AuthorizationPolicies.WritePromotions, policy =>
                policy.RequireAssertion(ctx => HasAnyRole(ctx, SpmeRoles.PlatformAdmin, SpmeRoles.HrAdmin)))
            .AddPolicy(AuthorizationPolicies.ApprovePromotions, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(
                    ctx, SpmePermissions.PromotionsApprove, SpmeRoles.PlatformAdmin, SpmeRoles.HrAdmin)))
            .AddPolicy(AuthorizationPolicies.ReadOwnPromotionStatus, policy =>
                policy.RequireRole(SpmeRoles.Employee))
            .AddPolicy(AuthorizationPolicies.ReadPromotionReports, policy =>
                policy.RequireAssertion(ctx =>
                    HasPermissionOrRole(
                        ctx,
                        "promotions.read",
                        SpmeRoles.PlatformAdmin,
                        SpmeRoles.InstituteAdmin,
                        SpmeRoles.HrAdmin,
                        SpmeRoles.Employee)))
            .AddPolicy(AuthorizationPolicies.WriteOwnPromotionReports, policy =>
                policy.RequireRole(SpmeRoles.Employee))
            .AddPolicy(AuthorizationPolicies.ReadReports, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(
                    ctx,
                    SpmePermissions.ReportsRead,
                    SpmeRoles.PlatformAdmin,
                    SpmeRoles.InstituteAdmin,
                    SpmeRoles.HrAdmin,
                    SpmeRoles.ReportsAdmin)))
            .AddPolicy(AuthorizationPolicies.WriteReports, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(
                    ctx,
                    SpmePermissions.ReportsWrite,
                    SpmeRoles.PlatformAdmin,
                    SpmeRoles.ReportsAdmin)))
            .AddPolicy(AuthorizationPolicies.SubmitReports, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(
                    ctx,
                    SpmePermissions.ReportsSubmit,
                    SpmeRoles.PlatformAdmin,
                    SpmeRoles.ReportsAdmin)))
            .AddPolicy(AuthorizationPolicies.ApproveReports, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(
                    ctx,
                    SpmePermissions.ReportsApprove,
                    SpmeRoles.PlatformAdmin,
                    SpmeRoles.ReportsAdmin)))
            .AddPolicy(AuthorizationPolicies.ManageOwnReports, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(
                    ctx, SpmePermissions.ReportsSelf, SpmeRoles.Employee)))
            .AddPolicy(AuthorizationPolicies.ReviewStaffReports, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(
                    ctx, SpmePermissions.ReportsReview,
                    SpmeRoles.HeadOfSection, SpmeRoles.HeadOfDivision)))
            .AddPolicy(AuthorizationPolicies.ReadStaffQuarterlyReports, policy =>
                policy.RequireAssertion(ctx =>
                    HasPermissionOrRole(ctx, SpmePermissions.ReportsSelf, SpmeRoles.Employee) ||
                    HasPermissionOrRole(ctx, SpmePermissions.ReportsReview,
                        SpmeRoles.HeadOfSection, SpmeRoles.HeadOfDivision)))
            .AddPolicy(AuthorizationPolicies.ReadOrganization, policy =>
                policy.RequireAssertion(ctx =>
                    HasPermissionOrRole(
                        ctx,
                        "organization.read",
                        SpmeRoles.PlatformAdmin,
                        SpmeRoles.InstituteAdmin,
                        SpmeRoles.HrAdmin) ||
                    InstituteStaffAccess.HasStaffManagementReadCompatibility(ctx.User)))
            .AddPolicy(AuthorizationPolicies.ManageOrganization, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, "organization.manage", SpmeRoles.PlatformAdmin, SpmeRoles.HrAdmin)))
            .AddPolicy(AuthorizationPolicies.ReadMemos, policy =>
                policy.RequireAssertion(ctx =>
                    HasPermissionOrRole(ctx, "memos.read", SpmeRoles.PlatformAdmin, SpmeRoles.HrAdmin, SpmeRoles.Employee) ||
                    InstituteStaffAccess.HasStaffManagementReadCompatibility(ctx.User)))
            .AddPolicy(AuthorizationPolicies.ManageMemos, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(
                    ctx,
                    "memos.write",
                    SpmeRoles.PlatformAdmin,
                    SpmeRoles.InstituteAdmin,
                    SpmeRoles.HrAdmin)))
            .AddPolicy(AuthorizationPolicies.PublishMemos, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(
                    ctx,
                    "memos.publish",
                    SpmeRoles.PlatformAdmin,
                    SpmeRoles.InstituteAdmin,
                    SpmeRoles.HrAdmin)))
            .AddPolicy(AuthorizationPolicies.ReadHolidays, policy =>
                policy.RequireAssertion(ctx =>
                    HasAnyRole(ctx, SpmeRoles.PlatformAdmin, SpmeRoles.HrAdmin, SpmeRoles.Employee) ||
                    InstituteStaffAccess.HasStaffManagementReadCompatibility(ctx.User)))
            .AddPolicy(AuthorizationPolicies.ManageHolidays, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, "organization.manage", SpmeRoles.PlatformAdmin, SpmeRoles.HrAdmin)))
            .AddPolicy(AuthorizationPolicies.ReadNotifications, policy =>
                policy.RequireAssertion(ctx =>
                    HasPermissionOrRole(ctx, "notifications.self", SpmeRoles.PlatformAdmin, SpmeRoles.HrAdmin, SpmeRoles.Employee) ||
                    InstituteStaffAccess.HasStaffManagementReadCompatibility(ctx.User)))
            .AddPolicy(AuthorizationPolicies.ManageNotifications, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, "notifications.manage", SpmeRoles.PlatformAdmin, SpmeRoles.HrAdmin)))
            .AddPolicy(AuthorizationPolicies.ReadLeave, policy =>
                policy.RequireAssertion(ctx =>
                    HasPermissionOrRole(
                        ctx,
                        "leave.read",
                        SpmeRoles.PlatformAdmin,
                        SpmeRoles.InstituteAdmin,
                        SpmeRoles.HrAdmin,
                        SpmeRoles.Employee) ||
                    InstituteStaffAccess.HasStaffManagementReadCompatibility(ctx.User)))
            .AddPolicy(AuthorizationPolicies.RequestLeave, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, "leave.request", SpmeRoles.PlatformAdmin, SpmeRoles.HrAdmin, SpmeRoles.Employee)))
            .AddPolicy(AuthorizationPolicies.ApproveLeave, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, "leave.approve",
                    SpmeRoles.PlatformAdmin, SpmeRoles.HrAdmin, SpmeRoles.HeadOfSection,
                    SpmeRoles.HeadOfDivision, SpmeRoles.InstituteDirector)))
            .AddPolicy(AuthorizationPolicies.ManageLeave, policy =>
                policy.RequireAssertion(ctx =>
                    HasPermissionOrRole(ctx, "leave.manage", SpmeRoles.PlatformAdmin, SpmeRoles.HrAdmin) ||
                    InstituteStaffAccess.HasStaffManagementWriteCompatibility(ctx.User)))
            .AddPolicy(AuthorizationPolicies.ReadKnowledge, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, SpmePermissions.KnowledgeRead, SpmeRoles.PlatformAdmin)))
            .AddPolicy(AuthorizationPolicies.WriteKnowledge, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, SpmePermissions.KnowledgeWrite, SpmeRoles.PlatformAdmin)))
            .AddPolicy(AuthorizationPolicies.ReadProjects, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, SpmePermissions.ProjectsRead, SpmeRoles.PlatformAdmin)))
            .AddPolicy(AuthorizationPolicies.WriteProjects, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, SpmePermissions.ProjectsWrite, SpmeRoles.PlatformAdmin)))
            .AddPolicy(AuthorizationPolicies.ApproveProjects, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, SpmePermissions.ProjectsApprove, SpmeRoles.PlatformAdmin)))
            .AddPolicy(AuthorizationPolicies.ReadStrategicPlans, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(
                    ctx,
                    SpmePermissions.StrategicPlansRead,
                    SpmeRoles.PlatformAdmin,
                    SpmeRoles.StrategicPlanAdmin)))
            .AddPolicy(AuthorizationPolicies.WriteStrategicPlans, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(
                    ctx,
                    SpmePermissions.StrategicPlansWrite,
                    SpmeRoles.PlatformAdmin,
                    SpmeRoles.StrategicPlanAdmin)))
            .AddPolicy(AuthorizationPolicies.ActivateStrategicPlans, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(
                    ctx,
                    SpmePermissions.StrategicPlansActivate,
                    SpmeRoles.PlatformAdmin,
                    SpmeRoles.StrategicPlanAdmin)))
            .AddPolicy(AuthorizationPolicies.ReadThrusts, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, SpmePermissions.ThrustsRead, SpmeRoles.PlatformAdmin)))
            .AddPolicy(AuthorizationPolicies.WriteThrusts, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, SpmePermissions.ThrustsWrite, SpmeRoles.PlatformAdmin)))
            .AddPolicy(AuthorizationPolicies.ReadOutputs, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, SpmePermissions.OutputsRead, SpmeRoles.PlatformAdmin)))
            .AddPolicy(AuthorizationPolicies.WriteOutputs, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, SpmePermissions.OutputsWrite, SpmeRoles.PlatformAdmin)))
            .AddPolicy(AuthorizationPolicies.ReadIndicators, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, SpmePermissions.IndicatorsRead, SpmeRoles.PlatformAdmin)))
            .AddPolicy(AuthorizationPolicies.WriteIndicators, policy =>
                policy.RequireAssertion(ctx => HasPermissionOrRole(ctx, SpmePermissions.IndicatorsWrite, SpmeRoles.PlatformAdmin)));

        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }

    private static bool HasAnyRole(AuthorizationHandlerContext context, params string[] roles) =>
        roles.Any(context.User.IsInRole);

    private static bool HasPermissionOrRole(AuthorizationHandlerContext context, string permission, params string[] fallbackRoles) =>
        context.User.HasClaim("permission", permission) || HasAnyRole(context, fallbackRoles);
}
