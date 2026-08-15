using Csir.Spme.Domain.Iam;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Infrastructure.Jobs;

public sealed class IdentitySeedHostedService : IHostedService
{
    private static readonly string[] SystemRoles =
    [
        "PlatformAdmin",
        "InstituteAdmin",
        "HrAdmin",
        "StrategicPlanAdmin",
        "ReportsAdmin",
        "Employee",
        "ServiceClient",
        "HeadOfSection",
        "HeadOfDivision",
        "InstituteDirector"
    ];

    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<IdentitySeedHostedService> _logger;

    public IdentitySeedHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<IdentitySeedHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();

        foreach (var roleName in SystemRoles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            var role = new Role(roleName, roleName, $"{roleName} system role.", isSystemRole: true);
            var result = await roleManager.CreateAsync(role);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Could not seed role {roleName}: {FormatErrors(result)}");
        }

        await EnsurePermissionsAsync(roleManager, cancellationToken);
        await EnsureHrAdminAsync(userManager, db, cancellationToken);
        await EnsureEmployeeUsersAsync(userManager, roleManager, db, cancellationToken);

        var section = _configuration.GetSection("Identity:SeedAdmin");
        var userName = section.GetValue<string>("UserName");
        var email = section.GetValue<string>("Email");
        var password = section.GetValue<string>("Password");

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation("Identity seed admin skipped because Identity:SeedAdmin is incomplete.");
            return;
        }

        var admin = await userManager.FindByNameAsync(userName)
            ?? await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new User(userName, "PlatformAdmin")
            {
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(admin, password);
            if (!createResult.Succeeded)
                throw new InvalidOperationException($"Could not seed admin user: {FormatErrors(createResult)}");
        }
        else
        {
            var shouldUpdate = false;
            if (!string.Equals(admin.UserName, userName, StringComparison.Ordinal))
            {
                var userNameResult = await userManager.SetUserNameAsync(admin, userName);
                if (!userNameResult.Succeeded)
                    throw new InvalidOperationException($"Could not update seed admin username: {FormatErrors(userNameResult)}");
            }

            if (!string.Equals(admin.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                var emailResult = await userManager.SetEmailAsync(admin, email);
                if (!emailResult.Succeeded)
                    throw new InvalidOperationException($"Could not update seed admin email: {FormatErrors(emailResult)}");
            }

            if (!admin.EmailConfirmed)
            {
                admin.EmailConfirmed = true;
                shouldUpdate = true;
            }

            if (!string.Equals(admin.IdentityType, "PlatformAdmin", StringComparison.OrdinalIgnoreCase))
            {
                admin.SetIdentityType("PlatformAdmin");
                shouldUpdate = true;
            }

            if (admin.AccountStatus != "active")
            {
                admin.CompletePasswordReset();
                shouldUpdate = true;
            }

            if (shouldUpdate)
            {
                var updateResult = await userManager.UpdateAsync(admin);
                if (!updateResult.Succeeded)
                    throw new InvalidOperationException($"Could not update seed admin user: {FormatErrors(updateResult)}");
            }

            if (_environment.IsDevelopment() || _environment.IsEnvironment("Test"))
            {
                await EnsureDevelopmentSeedPasswordAsync(userManager, admin, password);
            }
        }

        if (!await userManager.IsInRoleAsync(admin, "PlatformAdmin"))
        {
            var addRoleResult = await userManager.AddToRoleAsync(admin, "PlatformAdmin");
            if (!addRoleResult.Succeeded)
                throw new InvalidOperationException($"Could not assign PlatformAdmin role: {FormatErrors(addRoleResult)}");
        }

        _logger.LogInformation("Identity roles ready. Seed admin user configured: {UserName}.", userName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureDevelopmentSeedPasswordAsync(UserManager<User> userManager, User admin, string password)
    {
        if (await userManager.CheckPasswordAsync(admin, password))
            return;

        var token = await userManager.GeneratePasswordResetTokenAsync(admin);
        var resetResult = await userManager.ResetPasswordAsync(admin, token, password);
        if (!resetResult.Succeeded)
            throw new InvalidOperationException($"Could not reset development seed admin password: {FormatErrors(resetResult)}");
    }

    private static string FormatErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));

    private static async Task EnsurePermissionsAsync(RoleManager<Role> roleManager, CancellationToken ct)
    {
        var hrPermissions = new[]
        {
            SpmePermissions.OrganizationRead, SpmePermissions.OrganizationManage,
            SpmePermissions.EmployeesRead, SpmePermissions.EmployeesWrite, SpmePermissions.EmployeesVerify,
            SpmePermissions.LeaveRead, SpmePermissions.LeaveApprove, SpmePermissions.LeaveManage,
            SpmePermissions.MemosRead, SpmePermissions.MemosWrite, SpmePermissions.MemosPublish,
            SpmePermissions.NotificationsManage, SpmePermissions.PromotionsRead, SpmePermissions.PromotionsWrite
        };
        foreach (var (roleName, permissions) in new[]
        {
            ("HrAdmin", hrPermissions),
            ("StrategicPlanAdmin", new[]
            {
                SpmePermissions.StrategicPlansRead,
                SpmePermissions.StrategicPlansWrite,
                SpmePermissions.StrategicPlansActivate
            }),
            ("PlatformAdmin", SpmePermissions.All),
            ("HeadOfSection", new[] { SpmePermissions.LeaveRead, SpmePermissions.LeaveApprove, SpmePermissions.ReportsReview }),
            ("HeadOfDivision", new[] { SpmePermissions.LeaveRead, SpmePermissions.LeaveApprove, SpmePermissions.ReportsReview }),
            ("InstituteDirector", new[] { SpmePermissions.LeaveRead, SpmePermissions.LeaveApprove })
        })
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null) continue;
            var existing = await roleManager.GetClaimsAsync(role);
            foreach (var permission in permissions)
            {
                if (existing.Any(claim => claim.Type == "permission" && claim.Value == permission)) continue;
                await roleManager.AddClaimAsync(role, new Claim("permission", permission));
            }
        }

        var employeeRole = await roleManager.FindByNameAsync("Employee");
        if (employeeRole is not null)
        {
            var existing = await roleManager.GetClaimsAsync(employeeRole);
            foreach (var permission in new[] { SpmePermissions.MemosRead, SpmePermissions.NotificationsSelf,
                         SpmePermissions.LeaveRead, SpmePermissions.LeaveRequest, SpmePermissions.ReportsSelf,
                         SpmePermissions.PromotionsSelfRead })
            {
                if (!existing.Any(claim => claim.Type == "permission" && claim.Value == permission))
                    await roleManager.AddClaimAsync(employeeRole, new Claim("permission", permission));
            }
        }
    }

    private async Task EnsureHrAdminAsync(UserManager<User> userManager, SpmeDbContext db, CancellationToken ct)
    {
        var section = _configuration.GetSection("Identity:SeedHrAdmin");
        var userName = section.GetValue<string>("UserName");
        var email = section.GetValue<string>("Email");
        var password = section.GetValue<string>("Password");
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var instituteCode = section.GetValue<string>("InstituteCode") ?? "DEV-HR";
        var institute = await db.Institutes.FirstOrDefaultAsync(x => x.Code == instituteCode && x.IsActive, ct);
        if (institute is null && _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            institute = new Institute(instituteCode, "Development HR Institute", "Institute");
            db.Institutes.Add(institute);
            await db.SaveChangesAsync(ct);
        }
        if (institute is null)
        {
            _logger.LogWarning("HR seed user skipped because institute {InstituteCode} was not found.", instituteCode);
            return;
        }

        // Prefer username, then email — local DBs may already have the seed email under a
        // different username after earlier imports or partial seeds.
        var user = await userManager.FindByNameAsync(userName)
            ?? await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new User(userName, "HrAdmin") { Email = email, EmailConfirmed = true };
            user.AssignInstitute(institute.Id, "HrAdmin");
            var create = await userManager.CreateAsync(user, password);
            if (!create.Succeeded)
                throw new InvalidOperationException($"Could not seed HR admin user: {FormatErrors(create)}");
        }
        else
        {
            if (!string.Equals(user.UserName, userName, StringComparison.Ordinal))
                await userManager.SetUserNameAsync(user, userName);
            if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = email;
                user.EmailConfirmed = true;
            }
            user.AssignInstitute(institute.Id, "HrAdmin");
            await userManager.UpdateAsync(user);
        }

        if (!await userManager.IsInRoleAsync(user, "HrAdmin"))
        {
            var roleResult = await userManager.AddToRoleAsync(user, "HrAdmin");
            if (!roleResult.Succeeded)
                throw new InvalidOperationException($"Could not assign HrAdmin role: {FormatErrors(roleResult)}");
        }
    }

    private async Task EnsureEmployeeUsersAsync(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        SpmeDbContext db,
        CancellationToken ct)
    {
        if (!_configuration.GetValue("Identity:ProvisionEmployeeUsers", true))
        {
            _logger.LogInformation("Employee Identity user provisioning skipped by configuration.");
            return;
        }

        var employeeRole = await roleManager.FindByNameAsync("Employee");
        if (employeeRole is null)
        {
            _logger.LogWarning("Employee Identity user provisioning skipped because Employee role was not found.");
            return;
        }

        var employees = await db.Employees.AsNoTracking()
            .Where(employee => employee.PrimaryEmail != null && employee.PrimaryEmail != "")
            .Select(employee => new
            {
                employee.Id,
                employee.InstituteId,
                employee.PrimaryEmail,
                employee.ProfileStatus
            })
            .ToListAsync(ct);

        var created = 0;
        var linked = 0;
        var skipped = 0;
        foreach (var employee in employees)
        {
            var email = employee.PrimaryEmail?.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                skipped++;
                continue;
            }

            var linkedUsers = await db.Users
                .Where(candidate => candidate.EmployeeId == employee.Id)
                .ToListAsync(ct);
            var employeeIdentityUsers = linkedUsers.Where(IsEmployeeIdentity).ToList();
            var user = employeeIdentityUsers.Count > 1
                ? await ReconcileDuplicateEmployeeUsersAsync(userManager, employeeIdentityUsers, employee.Id, email)
                : employeeIdentityUsers.SingleOrDefault();

            if (user is null)
            {
                var existing = await userManager.FindByEmailAsync(email)
                    ?? await userManager.FindByNameAsync(email);
                if (existing is null)
                {
                    user = new User(email, "Employee")
                    {
                        Email = email,
                        EmailConfirmed = true
                    };
                    user.LinkEmployee(employee.Id, employee.InstituteId);
                    user.MarkPasswordResetRequired();

                    var create = await userManager.CreateAsync(user);
                    if (!create.Succeeded)
                    {
                        skipped++;
                        _logger.LogWarning(
                            "Could not provision employee user for employee {EmployeeId}: {Errors}",
                            employee.Id, FormatErrors(create));
                        continue;
                    }

                    created++;
                }
                else if (IsEmployeeIdentity(existing))
                {
                    if (existing.EmployeeId is not null && existing.EmployeeId != employee.Id)
                    {
                        skipped++;
                        _logger.LogWarning(
                            "Employee Identity user provisioning skipped because email for employee {EmployeeId} is already linked to employee {LinkedEmployeeId}.",
                            employee.Id, existing.EmployeeId);
                        continue;
                    }

                    user = existing;
                    if (user.EmployeeId is null)
                    {
                        user.LinkEmployee(employee.Id, employee.InstituteId);
                        if (string.IsNullOrWhiteSpace(user.PasswordHash))
                            user.MarkPasswordResetRequired();

                        var update = await userManager.UpdateAsync(user);
                        if (!update.Succeeded)
                        {
                            skipped++;
                            _logger.LogWarning(
                                "Could not link existing user {UserId} to employee {EmployeeId}: {Errors}",
                                user.Id, employee.Id, FormatErrors(update));
                            continue;
                        }

                        linked++;
                    }
                }
                else
                {
                    if (existing.EmployeeId is null)
                    {
                        existing.LinkEmployee(employee.Id, employee.InstituteId, existing.IdentityType);
                        var update = await userManager.UpdateAsync(existing);
                        if (!update.Succeeded)
                        {
                            skipped++;
                            _logger.LogWarning(
                                "Could not link existing {IdentityType} user {UserId} to employee {EmployeeId}: {Errors}",
                                existing.IdentityType, existing.Id, employee.Id, FormatErrors(update));
                            continue;
                        }

                        linked++;
                    }

                    _logger.LogInformation(
                        "Employee Identity account was not created for employee {EmployeeId} because {IdentityType} user {UserId} already uses that email.",
                        employee.Id, existing.IdentityType, existing.Id);
                    continue;
                }
            }

            if (!await userManager.IsInRoleAsync(user, "Employee") && IsEmployeeIdentity(user))
            {
                var roleResult = await userManager.AddToRoleAsync(user, "Employee");
                if (!roleResult.Succeeded)
                {
                    _logger.LogWarning(
                        "Could not assign Employee role to user {UserId}: {Errors}",
                        user.Id, FormatErrors(roleResult));
                }
            }
        }

        _logger.LogInformation(
            "Employee Identity user provisioning complete. Created: {Created}; linked: {Linked}; skipped: {Skipped}.",
            created, linked, skipped);
    }

    private async Task<User> ReconcileDuplicateEmployeeUsersAsync(
        UserManager<User> userManager,
        IReadOnlyList<User> employeeIdentityUsers,
        Guid employeeId,
        string email)
    {
        var canonical = SelectCanonicalEmployeeUser(employeeIdentityUsers, email);
        foreach (var duplicate in employeeIdentityUsers.Where(candidate => candidate.Id != canonical.Id))
        {
            duplicate.UnlinkEmployee();
            var unlink = await userManager.UpdateAsync(duplicate);
            if (!unlink.Succeeded)
            {
                _logger.LogWarning(
                    "Could not unlink duplicate Employee user {UserId} from employee {EmployeeId}: {Errors}",
                    duplicate.Id, employeeId, FormatErrors(unlink));
                continue;
            }

            _logger.LogWarning(
                "Unlinked duplicate Employee user {UserId} from employee {EmployeeId}.",
                duplicate.Id, employeeId);
        }

        return canonical;
    }

    private static bool IsEmployeeIdentity(User user) =>
        string.Equals(user.IdentityType, "Employee", StringComparison.OrdinalIgnoreCase);

    private static User SelectCanonicalEmployeeUser(IReadOnlyList<User> users, string email) =>
        users
            .OrderByDescending(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(user => user.LastLoginAt.HasValue)
            .ThenByDescending(user => user.LastLoginAt)
            .ThenBy(user => user.Id)
            .First();
}
