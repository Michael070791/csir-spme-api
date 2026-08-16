using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Persistence;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Tools.LegacyImport;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = LegacyImportOptions.Parse(args);
        if (!options.IsValid)
        {
            Console.Error.WriteLine(options.ValidationMessage);
            LegacyImportOptions.PrintUsage();
            return 2;
        }

        var preflight = LegacyBacpacPreflight.Validate(options.AuthBackupPath, options.SpmeBackupPath);
        if (!preflight.IsValid)
        {
            Console.Error.WriteLine(preflight.Message);
            return 3;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        await using var target = new SpmeDbContext(
            new DbContextOptionsBuilder<SpmeDbContext>()
                .UseSqlServer(options.TargetConnectionString, sql => sql.CommandTimeout(0))
                .Options);

        var importer = new LegacyImporter(options, target);
        await importer.RunAsync(cancellation.Token);
        return 0;
    }
}

internal sealed record LegacyImportOptions(
    string LegacyAuthConnectionString,
    string LegacySpmeConnectionString,
    string TargetConnectionString,
    string AuthBackupPath,
    string SpmeBackupPath,
    bool Apply)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(LegacyAuthConnectionString) &&
        !string.IsNullOrWhiteSpace(LegacySpmeConnectionString) &&
        !string.IsNullOrWhiteSpace(TargetConnectionString);

    public string ValidationMessage => "Missing required connection string argument.";

    public static LegacyImportOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var apply = false;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--apply")
            {
                apply = true;
                continue;
            }

            if (args[i] == "--dry-run")
            {
                apply = false;
                continue;
            }

            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
                continue;

            values[args[i]] = args[++i];
        }

        return new LegacyImportOptions(
            AsReadOnly(FirstNonBlank(
                values.GetValueOrDefault("--legacy-auth-connection"),
                Environment.GetEnvironmentVariable("LEGACY_AUTH_CONNECTION_STRING"))),
            AsReadOnly(FirstNonBlank(
                values.GetValueOrDefault("--legacy-spme-connection"),
                Environment.GetEnvironmentVariable("LEGACY_SPME_CONNECTION_STRING"))),
            FirstNonBlank(
                values.GetValueOrDefault("--target-connection"),
                Environment.GetEnvironmentVariable("TARGET_CONNECTION_STRING")),
            values.GetValueOrDefault("--auth-backup-path", string.Empty),
            values.GetValueOrDefault("--spme-backup-path", string.Empty),
            apply);
    }

    private static string FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string AsReadOnly(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationIntent = ApplicationIntent.ReadOnly,
            ApplicationName = "Csir.Spme.LegacyImport.ReadOnlySource"
        };
        return builder.ConnectionString;
    }

    public static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: set LEGACY_AUTH_CONNECTION_STRING, LEGACY_SPME_CONNECTION_STRING, and TARGET_CONNECTION_STRING; then run with [--auth-backup-path <path>] [--spme-backup-path <path>] [--dry-run|--apply]. Connection-string CLI arguments remain supported for local compatibility but expose secrets in process metadata.");
    }
}

public static class LegacyImportSourceName
{
    private const int MaximumLength = 128;

    public static string Derive(string authBackupPath, string spmeBackupPath)
    {
        var authName = Path.GetFileNameWithoutExtension(authBackupPath);
        var spmeName = Path.GetFileNameWithoutExtension(spmeBackupPath);
        var sourceName = $"{authName}__{spmeName}";
        if (sourceName.Length <= MaximumLength)
            return sourceName;

        var suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceName)))[..16];
        var availableNameLength = MaximumLength - suffix.Length - 4;
        var authLength = availableNameLength / 2;
        var spmeLength = availableNameLength - authLength;
        return $"{authName[..Math.Min(authName.Length, authLength)]}__{spmeName[..Math.Min(spmeName.Length, spmeLength)]}__{suffix}";
    }
}

internal sealed partial class LegacyImporter
{
    private readonly LegacyImportOptions _options;
    private readonly SpmeDbContext _target;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<int, Guid> _institutesByLegacyId = [];
    private readonly Dictionary<int, Guid> _divisionsByLegacyId = [];
    private readonly Dictionary<int, Guid> _sectionsByLegacyId = [];
    private readonly Dictionary<string, Guid> _institutesByNormalizedText = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, Guid> _employeesByLegacyPersonalInfoId = [];
    private readonly Dictionary<string, Guid> _employeesByLegacyUid = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, Guid> _employeeInstitutesById = [];
    private readonly Dictionary<string, Guid> _rolesByLegacyId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _legacyRoleNamesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _usersByLegacyId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, Guid> _usersByEmployeeId = [];
    private readonly Dictionary<string, Guid> _existingMappings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _stagedEmployeesByInstituteStaffId = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _stagedInstituteAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _stagedEmployeeEmails = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _stagedUserEmails = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _stagedUserRoles = new(StringComparer.OrdinalIgnoreCase);

    private LegacyImportRun _run = null!;
    private bool WritesWorkingState => true;

    public LegacyImporter(LegacyImportOptions options, SpmeDbContext target)
    {
        _options = options;
        _target = target;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var sourceBackupSha256 = await HashSourcesAsync();
        var sourceName = LegacyImportSourceName.Derive(_options.AuthBackupPath, _options.SpmeBackupPath);
        _run = new LegacyImportRun(
            sourceName,
            $"{_options.AuthBackupPath}|{_options.SpmeBackupPath}",
            sourceBackupSha256,
            _options.Apply ? "apply" : "dry-run");

        _target.LegacyImportRuns.Add(_run);
        await SaveIfApplyAsync();

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? importTransaction = null;
        if (!_options.Apply)
            importTransaction = await _target.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await using var legacyAuth = new SqlConnection(_options.LegacyAuthConnectionString);
            await using var legacySpme = new SqlConnection(_options.LegacySpmeConnectionString);
            await legacyAuth.OpenAsync(cancellationToken);
            await legacySpme.OpenAsync(cancellationToken);

            await ValidateSourceSchemasAsync(legacyAuth, legacySpme, cancellationToken);
            await LoadExistingMappingsAsync(cancellationToken);

            var rowCounts = await RecordSourceSummariesAsync(legacyAuth, legacySpme);
            _run.RecordSourceSummary(rowCounts.Count, rowCounts.Sum(item => item.Value), JsonSerializer.Serialize(rowCounts, _jsonOptions));

            if (_options.Apply)
            {
                var priorCompletedRun = await _target.LegacyImportRuns.AsNoTracking()
                    .Where(item =>
                        item.Id != _run.Id &&
                        item.SourceName == sourceName &&
                        item.SourceBackupSha256 == sourceBackupSha256 &&
                        item.Mode == "apply" &&
                        item.Status == "completed")
                    .OrderByDescending(item => item.CompletedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (priorCompletedRun is not null)
                {
                    _run.RecordReconciliation(priorCompletedRun.ReconciliationJson);
                    _run.Complete($"No-op: source backup was already applied successfully by run {priorCompletedRun.Id}.");
                    await SaveIfApplyAsync();
                    Console.WriteLine($"apply completed as an idempotent no-op. Source rows: {_run.SourceRowCount}; inserted: 0; updated: 0; issues: 0.");
                    return;
                }
            }

            await ImportOrganizationAsync(legacySpme);
            _migrationActorUserId = await ResolveMigrationActorAsync(cancellationToken);
            await ImportPositionTypesAsync(legacyAuth, cancellationToken);
            await ImportEmployeesAsync(legacyAuth);
            await ImportRolesAsync(legacyAuth);
            await ImportUsersAsync(legacyAuth);
            await ImportUserRolesAsync(legacyAuth);
            await EnsureEmployeeAccountsAsync(cancellationToken);
            await ImportSpousesAsync(legacyAuth);
            await ImportChildrenAsync(legacyAuth);
            await ImportEducationAsync(legacyAuth);
            await ImportRemainingBusinessDataAsync(legacyAuth, legacySpme, cancellationToken);
            await RecordPendingBusinessTablesAsync(rowCounts);
            await RecordArchivedOperationalTablesAsync(rowCounts);
            await RecordReconciliationAsync(rowCounts, legacyAuth, legacySpme, cancellationToken);

            _run.Complete(_options.Apply ? "Legacy import applied." : "Legacy import dry-run completed.");
            await SaveIfApplyAsync();
            if (!_options.Apply)
                await importTransaction!.RollbackAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (importTransaction is not null)
            {
                try
                {
                    await importTransaction.RollbackAsync(CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                    // Remote hosts may abort long transactions before client rollback completes.
                }
            }

            _target.ChangeTracker.Clear();
            if (_options.Apply)
            {
                var failedRun = new LegacyImportRun(
                    sourceName,
                    $"{_options.AuthBackupPath}|{_options.SpmeBackupPath}",
                    sourceBackupSha256,
                    "apply");
                failedRun.Fail(ex.Message);
                _target.LegacyImportRuns.Add(failedRun);
                await _target.SaveChangesAsync(CancellationToken.None);
            }

            throw;
        }
        finally
        {
            if (importTransaction is not null)
                await importTransaction.DisposeAsync();
        }

        Console.WriteLine($"{_run.Mode} completed. Source rows: {_run.SourceRowCount}; inserted: {_run.TargetInsertedCount}; updated: {_run.TargetUpdatedCount}; issues: {_run.IssueCount}.");
    }

    private async Task ImportOrganizationAsync(SqlConnection legacySpme)
    {
        var institutes = (await legacySpme.QueryAsync<LegacyInstitute>("select Id, Name, ShortName from dbo.Institutes")).ToList();
        foreach (var source in institutes)
        {
            var name = RequiredText(source.Name, $"Institute {source.Id}");
            var code = CodeFrom(source.ShortName, source.Id);
            var normalizedName = NormalizeKey(name);
            var target = await _target.Institutes.FirstOrDefaultAsync(item => item.NormalizedName == normalizedName);
            var strategy = "normalized-name";

            if (target is null)
            {
                target = new Institute(code, name, "research-institute");
                if (WritesWorkingState)
                    _target.Institutes.Add(target);
                _run.AddInserted();
                strategy = "created";
            }
            else
            {
                _run.AddUpdated();
            }

            _institutesByLegacyId[source.Id] = target.Id;
            RememberInstituteText(target.Id, name);
            RememberInstituteText(target.Id, source.ShortName);
            AddMapping("LegacySpme", "Institutes", source.Id.ToString(CultureInfo.InvariantCulture), "org", "Institutes", target.Id, normalizedName, strategy, source);
        }

        await SaveIfApplyAsync();
        await EnsureInstituteAliasesAsync(institutes);
        await ImportDivisionsAsync(legacySpme);
        await ImportSectionsAsync(legacySpme);
    }

    private async Task EnsureInstituteAliasesAsync(IEnumerable<LegacyInstitute> institutes)
    {
        foreach (var source in institutes)
        {
            if (!_institutesByLegacyId.TryGetValue(source.Id, out var instituteId))
                continue;

            foreach (var alias in new[] { source.Name, source.ShortName }.Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                var normalized = NormalizeKey(alias);
                if (!_stagedInstituteAliases.Add(normalized) || await _target.InstituteAliases.AnyAsync(item => item.NormalizedAlias == normalized))
                    continue;

                if (WritesWorkingState)
                    _target.InstituteAliases.Add(new InstituteAlias(instituteId, alias!));
            }
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportDivisionsAsync(SqlConnection legacySpme)
    {
        var divisions = await legacySpme.QueryAsync<LegacyDivision>("select Id, Name, InstituteId from dbo.Divisions");
        foreach (var source in divisions)
        {
            if (!_institutesByLegacyId.TryGetValue(source.InstituteId, out var instituteId))
            {
                AddIssue("LegacySpme", "Divisions", source.Id.ToString(CultureInfo.InvariantCulture), "error", "institute-not-found", $"Division institute {source.InstituteId} was not imported.", source);
                continue;
            }

            var name = RequiredText(source.Name, $"Division {source.Id}");
            var target = await _target.Divisions.FirstOrDefaultAsync(item => item.InstituteId == instituteId && item.Name == name);
            var strategy = "institute-name";
            if (target is null)
            {
                target = new Division(instituteId, name);
                if (WritesWorkingState)
                    _target.Divisions.Add(target);
                _run.AddInserted();
                strategy = "created";
            }

            _divisionsByLegacyId[source.Id] = target.Id;
            AddMapping("LegacySpme", "Divisions", source.Id.ToString(CultureInfo.InvariantCulture), "org", "Divisions", target.Id, $"{instituteId}:{NormalizeKey(name)}", strategy, source);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportSectionsAsync(SqlConnection legacySpme)
    {
        var sections = await legacySpme.QueryAsync<LegacySection>("select Id, Name, DivisionId from dbo.Sections");
        foreach (var source in sections)
        {
            if (!_divisionsByLegacyId.TryGetValue(source.DivisionId, out var divisionId))
            {
                AddIssue("LegacySpme", "Sections", source.Id.ToString(CultureInfo.InvariantCulture), "error", "division-not-found", $"Section division {source.DivisionId} was not imported.", source);
                continue;
            }

            var name = RequiredText(source.Name, $"Section {source.Id}");
            var target = await _target.Sections.FirstOrDefaultAsync(item => item.DivisionId == divisionId && item.Name == name);
            var strategy = "division-name";
            if (target is null)
            {
                target = new Section(divisionId, name);
                if (WritesWorkingState)
                    _target.Sections.Add(target);
                _run.AddInserted();
                strategy = "created";
            }

            _sectionsByLegacyId[source.Id] = target.Id;
            AddMapping("LegacySpme", "Sections", source.Id.ToString(CultureInfo.InvariantCulture), "org", "Sections", target.Id, $"{divisionId}:{NormalizeKey(name)}", strategy, source);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportEmployeesAsync(SqlConnection legacyAuth)
    {
        await WarmInstituteLookupAsync();
        var people = (await legacyAuth.QueryAsync<LegacyPersonalInfo>("select * from dbo.PersonalInfos")).ToList();
        foreach (var source in people)
        {
            var employeeId = await ImportEmployeeAsync(source);
            if (employeeId.HasValue)
            {
                _employeesByLegacyPersonalInfoId[source.Id] = employeeId.Value;
                if (!string.IsNullOrWhiteSpace(source.UID))
                    _employeesByLegacyUid[source.UID] = employeeId.Value;
            }
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportRolesAsync(SqlConnection legacyAuth)
    {
        var roles = await legacyAuth.QueryAsync<LegacyRole>("select Id, Name from dbo.AspNetRoles");
        foreach (var source in roles)
        {
            var roleName = RequiredText(source.Name, "LegacyRole");
            var role = await _target.Roles.FirstOrDefaultAsync(item => item.NormalizedName == roleName.ToUpperInvariant());
            var strategy = "normalized-name";
            if (role is null)
            {
                role = new Role(roleName, roleName, $"{roleName} legacy role.", isSystemRole: false);
                if (WritesWorkingState)
                    _target.Roles.Add(role);
                _run.AddInserted();
                strategy = "created";
            }

            _rolesByLegacyId[source.Id] = role.Id;
            _legacyRoleNamesById[source.Id] = roleName;
            AddMapping("LegacyAuthSpme", "AspNetRoles", source.Id, "iam", "Roles", role.Id, roleName.ToUpperInvariant(), strategy, source);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportUsersAsync(SqlConnection legacyAuth)
    {
        var users = await legacyAuth.QueryAsync<LegacyUser>("select Id, UserName, Email, PhoneNumber, UID, StaffId, InstituteName, PasswordHash, EmailConfirmed, PhoneNumberConfirmed, LockoutEnabled, LockoutEnd, AccessFailedCount from dbo.AspNetUsers");
        foreach (var source in users)
        {
            var username = FirstNonBlank(source.UserName, source.Email, source.UID, source.StaffId, source.Id);
            if (string.IsNullOrWhiteSpace(username))
            {
                AddIssue("LegacyAuthSpme", "AspNetUsers", source.Id, "error", "missing-username", "User row has no usable username.", source);
                continue;
            }

            var normalizedUserName = username.ToUpperInvariant();
            var user = await _target.Users.FirstOrDefaultAsync(item => item.NormalizedUserName == normalizedUserName);
            var strategy = "normalized-username";
            if (user is null)
            {
                var uniqueEmail = await ResolveUniqueUserEmailAsync(source.Email, source.Id);
                user = new User(username, "Employee")
                {
                    Email = uniqueEmail,
                    NormalizedEmail = uniqueEmail?.ToUpperInvariant(),
                    PhoneNumber = string.IsNullOrWhiteSpace(source.PhoneNumber) ? null : source.PhoneNumber.Trim(),
                    EmailConfirmed = false,
                    PhoneNumberConfirmed = false,
                    LockoutEnabled = true
                };
                user.MarkPasswordResetRequired();
                if (WritesWorkingState)
                    _target.Users.Add(user);
                _run.AddInserted();
                strategy = "created-reset-required";
            }

            var employeeId = ResolveEmployeeByLegacyUser(source.UID)
                ?? await ResolveEmployeeByLegacyStaffIdAsync(source.StaffId)
                ?? await ResolveEmployeeByLegacyEmailAsync(source.Email);
            if (employeeId.HasValue)
            {
                var instituteId = await ResolveEmployeeInstituteAsync(employeeId.Value);
                if (instituteId.HasValue)
                {
                    user.LinkEmployee(employeeId.Value, instituteId.Value, "StaffUser");
                    _usersByEmployeeId[employeeId.Value] = user.Id;
                }
                else
                {
                    AddIssue("LegacyAuthSpme", "AspNetUsers", source.Id, "warning", "employee-not-found", "Legacy user resolved to an employee that is not available in the target import scope.", source);
                }
            }
            else
            {
                var instituteId = await ResolveInstituteOrNullAsync(source.InstituteName);
                if (instituteId.HasValue)
                    user.AssignInstitute(instituteId.Value, "StaffUser");
                AddIssue("LegacyAuthSpme", "AspNetUsers", source.Id, "info", "staff-user-not-employee", "Legacy management user was retained as a staff user without an unverified employee link.", new { source.Id, source.InstituteName });
            }

            if (!user.ImportCompatibleLegacyCredentials(
                    source.PasswordHash,
                    source.EmailConfirmed,
                    source.PhoneNumberConfirmed,
                    source.LockoutEnabled,
                    source.LockoutEnd,
                    source.AccessFailedCount))
            {
                AddIssue("LegacyAuthSpme", "AspNetUsers", source.Id, "warning", "password-reset-required", "Legacy password hash was not an ASP.NET Core Identity V3 hash; reset is required.", new { source.Id });
            }

            _usersByLegacyId[source.Id] = user.Id;
            AddMapping("LegacyAuthSpme", "AspNetUsers", source.Id, "iam", "Users", user.Id, normalizedUserName, strategy, source);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportUserRolesAsync(SqlConnection legacyAuth)
    {
        var assignments = await legacyAuth.QueryAsync<LegacyUserRole>("select UserId, RoleId from dbo.AspNetUserRoles");
        foreach (var source in assignments)
        {
            if (!_usersByLegacyId.TryGetValue(source.UserId, out var userId) || !_rolesByLegacyId.TryGetValue(source.RoleId, out var roleId))
            {
                AddIssue("LegacyAuthSpme", "AspNetUserRoles", $"{source.UserId}:{source.RoleId}", "warning", "principal-not-found", "User role assignment could not be matched.", source);
                continue;
            }

            var sourceRoleName = _legacyRoleNamesById[source.RoleId];
            await AddImportedRoleAssignmentAsync(userId, roleId, sourceRoleName, source, "legacy-role-preserved");

            var compatibilityRoleName = sourceRoleName.ToUpperInvariant() switch
            {
                "EMPLOYEE" => "Employee",
                _ => null
            };

            if (compatibilityRoleName is not null)
            {
                var compatibilityRole = await _target.Roles.SingleAsync(
                    item => item.NormalizedName == compatibilityRoleName.ToUpperInvariant());
                await AddImportedRoleAssignmentAsync(userId, compatibilityRole.Id, compatibilityRoleName, source, "approved-compatibility-role");
                AddIssue("LegacyAuthSpme", "AspNetUserRoles", $"{source.UserId}:{source.RoleId}", "info", "compatibility-role-mapped", $"Legacy role '{sourceRoleName}' was also mapped to approved V2 role '{compatibilityRoleName}'.", new { source.UserId, sourceRoleName, compatibilityRoleName });
            }
            else if (sourceRoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                     sourceRoleName.Equals("Director", StringComparison.OrdinalIgnoreCase) ||
                     sourceRoleName.Equals("DG", StringComparison.OrdinalIgnoreCase) ||
                     sourceRoleName.Equals("Reader", StringComparison.OrdinalIgnoreCase) ||
                     sourceRoleName.Equals("HR", StringComparison.OrdinalIgnoreCase) ||
                     sourceRoleName.Equals("Writer", StringComparison.OrdinalIgnoreCase))
            {
                AddIssue("LegacyAuthSpme", "AspNetUserRoles", $"{source.UserId}:{source.RoleId}", "warning", "policy-ambiguity", $"Legacy role '{sourceRoleName}' was preserved but not granted V2 permissions because no exact approved mapping exists.", new { source.UserId, sourceRoleName });
            }
        }

        await SaveIfApplyAsync();
    }

    private async Task AddImportedRoleAssignmentAsync(
        Guid userId,
        Guid roleId,
        string roleName,
        LegacyUserRole source,
        string strategy)
    {
        var assignmentKey = $"{userId:N}:{roleId:N}";
        var exists = _stagedUserRoles.Contains(assignmentKey) ||
            await _target.Set<IdentityUserRole<Guid>>().AnyAsync(item => item.UserId == userId && item.RoleId == roleId);
        if (exists)
            return;

        _stagedUserRoles.Add(assignmentKey);
        if (WritesWorkingState)
        {
            _target.Set<IdentityUserRole<Guid>>().Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = roleId });
            _target.AuditRecords.Add(new AuditRecord(
                _migrationActorUserId,
                "legacy-import.role-assigned",
                "UserRole",
                _run.Id.ToString())
            {
                TargetId = userId.ToString(),
                AfterSummary = JsonSerializer.Serialize(new
                {
                    roleId,
                    roleName,
                    strategy,
                    sourceLegacyUserId = source.UserId,
                    sourceLegacyRoleId = source.RoleId
                }, _jsonOptions)
            });
        }

        _run.AddInserted();
    }

    private async Task<Guid?> ImportEmployeeAsync(LegacyPersonalInfo source)
    {
        var staffId = FirstNonBlank(source.StaffId, source.UID);
        if (string.IsNullOrWhiteSpace(staffId))
        {
            AddIssue("LegacyAuthSpme", "PersonalInfos", source.Id.ToString(), "error", "missing-staff-id", "PersonalInfos row has neither StaffId nor UID.", source);
            return null;
        }

        var instituteId = await ResolveInstituteAsync(source.Institute, source.Id.ToString());
        var surname = string.IsNullOrWhiteSpace(source.Surname) ? "Unknown" : source.Surname.Trim();
        var gender = NormalizeGender(source.Gender, source.Id.ToString());
        var normalizedStaffId = staffId.Trim().ToUpperInvariant();
        var stagedEmployeeKey = $"{instituteId:N}:{normalizedStaffId}";
        var employee = await _target.Employees.FirstOrDefaultAsync(item => item.InstituteId == instituteId && item.NormalizedStaffId == normalizedStaffId);
        var strategy = "staff-id";

        if (employee is null)
        {
            if (_stagedEmployeesByInstituteStaffId.TryGetValue(stagedEmployeeKey, out var stagedEmployeeId))
            {
                AddIssue("LegacyAuthSpme", "PersonalInfos", source.Id.ToString(), "warning", "duplicate-staff-id", $"Staff id '{staffId}' appears more than once for the same institute and was mapped to the first imported employee.", source);
                AddMapping("LegacyAuthSpme", "PersonalInfos", source.Id.ToString(), "hr", "Employees", stagedEmployeeId, normalizedStaffId, "duplicate-staff-id", source);
                _employeeInstitutesById[stagedEmployeeId] = instituteId;
                return stagedEmployeeId;
            }

            employee = new Employee(instituteId, staffId.Trim(), surname, gender);
            if (WritesWorkingState)
                _target.Employees.Add(employee);
            _run.AddInserted();
            strategy = "created";
        }
        else
        {
            _run.AddUpdated();
        }

        var uniqueEmail = await ResolveUniqueEmployeeEmailAsync(source.Email, employee.Id, source.Id.ToString());

        employee.UpdateImportedProfile(
            source.Prefix,
            source.OtherNames,
            LegacyValueParser.ParseDate(source.DateOfBirth),
            source.Nationality,
            source.Religion,
            source.MaritalStatus,
            uniqueEmail,
            source.Phone,
            source.HrApproved || source.Verified);

        var staffCategory = LegacyStaffCategoryMapper.Map(source.Designation);
        if (staffCategory is null && !string.IsNullOrWhiteSpace(source.Designation))
            AddIssue("LegacyAuthSpme", "PersonalInfos", source.Id.ToString(), "warning", "unknown-staff-category", $"Designation '{source.Designation}' is not a supported staff category.", source);

        if (!string.IsNullOrWhiteSpace(source.Grade))
            AddIssue("LegacyAuthSpme", "PersonalInfos", source.Id.ToString(), "info", "legacy-grade-not-promoted", "Legacy grade text is retained in migration provenance but was not assigned to a canonical promotion grade without an approved equivalency mapping.", new { source.Id, source.Grade });

        if (!await _target.EmploymentRecords.AnyAsync(item => item.EmployeeId == employee.Id && item.IsCurrent))
        {
            var employment = new EmploymentRecord(
                employee.Id,
                instituteId,
                source.DivisionId.HasValue && _divisionsByLegacyId.TryGetValue(source.DivisionId.Value, out var divisionId) ? divisionId : null,
                source.SectionId.HasValue && _sectionsByLegacyId.TryGetValue(source.SectionId.Value, out var sectionId) ? sectionId : null,
                source.PositionTypeId.HasValue &&
                    _positionTypesByLegacyId.TryGetValue(source.PositionTypeId.Value, out var positionTypeId)
                        ? positionTypeId
                        : null,
                source.CurrentPosition,
                null,
                staffCategory,
                NormalizeServiceStatus(source.ServiceStatus),
                null,
                LegacyValueParser.ParseDate(source.AppointmentDate),
                LegacyValueParser.ParseDate(source.PromotionDate),
                source.PensionType,
                source.PensionId,
                LegacyValueParser.ParseDate(source.AppointmentDate) ?? DateTime.UtcNow.Date,
                true);

            if (WritesWorkingState)
                _target.EmploymentRecords.Add(employment);
        }

        AddMapping("LegacyAuthSpme", "PersonalInfos", source.Id.ToString(), "hr", "Employees", employee.Id, normalizedStaffId, strategy, source);
        _stagedEmployeesByInstituteStaffId[stagedEmployeeKey] = employee.Id;
        _employeeInstitutesById[employee.Id] = instituteId;
        return employee.Id;
    }

    private async Task ImportSpousesAsync(SqlConnection legacyAuth)
    {
        var spouses = await legacyAuth.QueryAsync<LegacySpouse>("select Id, Name, Profession, Address, UserId from dbo.Spouses");
        foreach (var source in spouses)
        {
            var employeeId = ResolveEmployeeByLegacyUser(source.UserId);
            if (!employeeId.HasValue)
            {
                AddIssue("LegacyAuthSpme", "Spouses", source.Id.ToString(), "warning", "employee-not-found", "Spouse row could not be matched to an employee.", source);
                continue;
            }

            if (await _target.EmployeeSpouses.AnyAsync(item => item.EmployeeId == employeeId.Value))
            {
                AddIssue("LegacyAuthSpme", "Spouses", source.Id.ToString(), "warning", "duplicate-spouse", "Employee already has a spouse record.", source);
                continue;
            }

            var spouse = new EmployeeSpouse(employeeId.Value, RequiredText(source.Name, "Unknown spouse"), null, null, null, source.Profession, source.Address);
            if (WritesWorkingState)
                _target.EmployeeSpouses.Add(spouse);
            _run.AddInserted();
            AddMapping("LegacyAuthSpme", "Spouses", source.Id.ToString(), "hr", "EmployeeSpouses", spouse.Id, employeeId.Value.ToString(), "employee-link", source);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportChildrenAsync(SqlConnection legacyAuth)
    {
        var children = await legacyAuth.QueryAsync<LegacyChild>("select Id, Name, DateOfBirth, Gender, BirthCertId, UserId from dbo.ChildInfos order by UserId, DateOfBirth");
        var importedChildren = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in children)
        {
            var existingMapping = GetExistingMapping("LegacyAuthSpme", "ChildInfos", source.Id.ToString());
            if (existingMapping.HasValue)
            {
                AddMapping("LegacyAuthSpme", "ChildInfos", source.Id.ToString(), "hr", "EmployeeChildren", existingMapping.Value, source.UserId.ToString(), "resume-existing", source);
                continue;
            }

            var employeeId = ResolveEmployeeByLegacyUser(source.UserId.ToString());
            if (!employeeId.HasValue)
            {
                AddIssue("LegacyAuthSpme", "ChildInfos", source.Id.ToString(), "warning", "employee-not-found", "Child row could not be matched to an employee.", source);
                continue;
            }

            var dob = LegacyValueParser.ParseDate(source.DateOfBirth);
            if (!dob.HasValue)
            {
                AddIssue("LegacyAuthSpme", "ChildInfos", source.Id.ToString(), "error", "invalid-child-date", "Child date of birth could not be parsed.", source);
                continue;
            }

            var duplicateKey = $"{employeeId.Value:N}|{NormalizeKey(source.Name)}|{dob:yyyy-MM-dd}|{NormalizeKey(source.Gender)}|{NormalizeKey(source.BirthCertId)}";
            if (importedChildren.TryGetValue(duplicateKey, out var duplicateTargetId))
            {
                AddIssue("LegacyAuthSpme", "ChildInfos", source.Id.ToString(), "info", "exact-duplicate-collapsed", "An exact duplicate child row was mapped to the first V2 child record.", new { source.Id, duplicateTargetId });
                AddMapping("LegacyAuthSpme", "ChildInfos", source.Id.ToString(), "hr", "EmployeeChildren", duplicateTargetId, employeeId.Value.ToString(), "exact-duplicate", source);
                continue;
            }

            var child = new EmployeeChild(employeeId.Value, source.Name, dob.Value, NormalizeGender(source.Gender, source.Id.ToString()), source.BirthCertId, null);
            if (WritesWorkingState)
                _target.EmployeeChildren.Add(child);
            _run.AddInserted();
            importedChildren[duplicateKey] = child.Id;
            AddMapping("LegacyAuthSpme", "ChildInfos", source.Id.ToString(), "hr", "EmployeeChildren", child.Id, employeeId.Value.ToString(), "employee-link", source);
        }

        await SaveIfApplyAsync();
    }

    private async Task ImportEducationAsync(SqlConnection legacyAuth)
    {
        var records = await legacyAuth.QueryAsync<LegacyEducation>("select * from dbo.EducationInfos");
        foreach (var source in records)
        {
            var existingMapping = GetExistingMapping("LegacyAuthSpme", "EducationInfos", source.Id.ToString());
            if (existingMapping.HasValue)
            {
                AddMapping("LegacyAuthSpme", "EducationInfos", source.Id.ToString(), "hr", "EducationRecords", existingMapping.Value, source.UserId.ToString(), "resume-existing", source);
                continue;
            }

            var employeeId = ResolveEmployeeByLegacyUser(source.UserId.ToString());
            if (!employeeId.HasValue)
            {
                AddIssue("LegacyAuthSpme", "EducationInfos", source.Id.ToString(), "warning", "employee-not-found", "Education row could not be matched to an employee.", source);
                continue;
            }

            var education = new EducationRecord(
                employeeId.Value,
                LimitRequiredText(source.InstitutionName, "Unknown institution", 256, "EducationInfos", source.Id.ToString(), nameof(source.InstitutionName), source),
                LimitRequiredText(source.CourseStudied, "Unspecified course", 256, "EducationInfos", source.Id.ToString(), nameof(source.CourseStudied), source),
                LimitRequiredText(source.CertificateAwarded, "Unspecified certificate", 256, "EducationInfos", source.Id.ToString(), nameof(source.CertificateAwarded), source),
                "other",
                LimitOptionalText(source.Grade, 64, "EducationInfos", source.Id.ToString(), nameof(source.Grade), source),
                LimitOptionalText(source.Specialization, 256, "EducationInfos", source.Id.ToString(), nameof(source.Specialization), source),
                LimitOptionalText(source.ProfesionalQualifications, 512, "EducationInfos", source.Id.ToString(), nameof(source.ProfesionalQualifications), source),
                LimitOptionalText(source.Affiliations, 512, "EducationInfos", source.Id.ToString(), nameof(source.Affiliations), source),
                LimitOptionalText(source.CertificateNumber, 128, "EducationInfos", source.Id.ToString(), nameof(source.CertificateNumber), source),
                LegacyValueParser.ParseDate(source.DateCommenced),
                LegacyValueParser.ParseDate(source.DateCompleted));

            if (WritesWorkingState)
                _target.EducationRecords.Add(education);
            _run.AddInserted();
            AddMapping("LegacyAuthSpme", "EducationInfos", source.Id.ToString(), "hr", "EducationRecords", education.Id, employeeId.Value.ToString(), "employee-link", source);
        }

        await SaveIfApplyAsync();
    }

    private async Task RecordArchivedOperationalTablesAsync(IReadOnlyDictionary<string, int> rowCounts)
    {
        var archivedTables = rowCounts.Keys.Where(IsOperationalTable).Order(StringComparer.OrdinalIgnoreCase);
        foreach (var table in archivedTables)
        {
            AddIssue("LegacyOperational", table, "*", "info", "archived-operational-table", $"Legacy operational table '{table}' has {rowCounts[table]} rows and is reconciled but not activated.", new { table, rows = rowCounts[table] });
        }

        await SaveIfApplyAsync();
    }

    private async Task RecordPendingBusinessTablesAsync(IReadOnlyDictionary<string, int> rowCounts)
    {
        var activeTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "LegacySpme.dbo.Institutes",
            "LegacySpme.dbo.Divisions",
            "LegacySpme.dbo.Sections",
            "LegacyAuthSpme.dbo.PersonalInfos",
            "LegacyAuthSpme.dbo.AspNetRoles",
            "LegacyAuthSpme.dbo.AspNetUsers",
            "LegacyAuthSpme.dbo.AspNetUserRoles",
            "LegacyAuthSpme.dbo.Spouses",
            "LegacyAuthSpme.dbo.ChildInfos",
            "LegacyAuthSpme.dbo.EducationInfos",
            "LegacyAuthSpme.dbo.PositionTypes",
            "LegacyAuthSpme.dbo.EmployeeLeaveRecords",
            "LegacyAuthSpme.dbo.Holidays",
            "LegacyAuthSpme.dbo.HolidayPeriods",
            "LegacyAuthSpme.dbo.CompassionateLeaveTypes",
            "LegacyAuthSpme.dbo.LeaveRequests",
            "LegacyAuthSpme.dbo.LeaveApprovals",
            "LegacyAuthSpme.dbo.LeaveHandovers",
            "LegacyAuthSpme.dbo.LeaveResumptions",
            "LegacySpme.dbo.Thrusts",
            "LegacySpme.dbo.Outputs",
            "LegacySpme.dbo.Indicators",
            "LegacySpme.dbo.IndicatorData",
            "LegacySpme.dbo.Projects",
            "LegacySpme.dbo.Reports",
            "LegacySpme.dbo.TechnologyInfo",
            "LegacySpme.dbo.Publications",
            "LegacySpme.dbo.SuccessStories",
            "LegacySpme.dbo.Memos",
            "LegacySpme.dbo.MemoInstitutes",
            "LegacySpme.dbo.Notifications"
        };

        foreach (var item in rowCounts.Where(item => item.Value > 0 && !activeTables.Contains(item.Key) && !IsOperationalTable(item.Key)).OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            AddIssue("LegacyBusiness", item.Key, "*", "info", "pending-domain-import", $"Legacy business table '{item.Key}' has {item.Value} rows pending a dedicated V2 aggregate importer.", new { table = item.Key, rows = item.Value });
        }

        await SaveIfApplyAsync();
    }

    private static bool IsOperationalTable(string table) =>
        table.StartsWith("HangFire.", StringComparison.OrdinalIgnoreCase) ||
        table.Contains(".HangFire.", StringComparison.OrdinalIgnoreCase) ||
        table.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
        table.Contains("EmailQueue", StringComparison.OrdinalIgnoreCase) ||
        table.Contains("LoginLock", StringComparison.OrdinalIgnoreCase) ||
        table.Contains("VerificationChallenge", StringComparison.OrdinalIgnoreCase) ||
        table.Contains("RegistrationInvite", StringComparison.OrdinalIgnoreCase) ||
        table.Contains("PushDevice", StringComparison.OrdinalIgnoreCase) ||
        table.Contains("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase);

    private async Task<IReadOnlyDictionary<string, int>> RecordSourceSummariesAsync(SqlConnection legacyAuth, SqlConnection legacySpme)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in await ReadTableCountsAsync(legacyAuth, "LegacyAuthSpme"))
            result[item.Key] = item.Value;
        foreach (var item in await ReadTableCountsAsync(legacySpme, "LegacySpme"))
            result[item.Key] = item.Value;
        return result;
    }

    private static async Task<IReadOnlyDictionary<string, int>> ReadTableCountsAsync(SqlConnection connection, string sourceName)
    {
        const string sql = """
            select s.name as SchemaName, t.name as TableName, sum(p.rows) as [Rows]
            from sys.tables t
            join sys.schemas s on s.schema_id = t.schema_id
            join sys.partitions p on p.object_id = t.object_id and p.index_id in (0, 1)
            group by s.name, t.name
            """;

        var rows = await connection.QueryAsync<TableCountRow>(sql);
        return rows.ToDictionary(
            row => $"{sourceName}.{row.SchemaName}.{row.TableName}",
            row => Convert.ToInt32(row.Rows, CultureInfo.InvariantCulture),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Guid> ResolveInstituteAsync(string? sourceText, string sourceKey)
    {
        await WarmInstituteLookupAsync();
        if (!string.IsNullOrWhiteSpace(sourceText) && _institutesByNormalizedText.TryGetValue(NormalizeKey(sourceText), out var instituteId))
            return instituteId;

        AddIssue("LegacyAuthSpme", "PersonalInfos", sourceKey, "warning", "institute-not-found", $"Institute '{sourceText}' could not be matched; employee assigned to UNKNOWN.", new { sourceText });
        var unknown = await _target.Institutes.FirstOrDefaultAsync(item => item.Code == "UNKNOWN");
        if (unknown is null)
        {
            unknown = new Institute("UNKNOWN", "Unknown Legacy Institute", "legacy-holding");
            if (WritesWorkingState)
                _target.Institutes.Add(unknown);
            _run.AddInserted();
            await SaveIfApplyAsync();
        }

        RememberInstituteText(unknown.Id, "UNKNOWN");
        return unknown.Id;
    }

    private async Task WarmInstituteLookupAsync()
    {
        if (_institutesByNormalizedText.Count > 0)
            return;

        var institutes = await _target.Institutes.AsNoTracking().ToListAsync();
        foreach (var institute in institutes)
        {
            RememberInstituteText(institute.Id, institute.Code);
            RememberInstituteText(institute.Id, institute.Name);
            RememberInstituteText(institute.Id, institute.NormalizedName);
        }

        var aliases = await _target.InstituteAliases.AsNoTracking().ToListAsync();
        foreach (var alias in aliases)
            RememberInstituteText(alias.InstituteId, alias.Alias);
    }

    private void RememberInstituteText(Guid instituteId, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            _institutesByNormalizedText[NormalizeKey(text)] = instituteId;
    }

    private Guid? ResolveEmployeeByLegacyUser(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;
        if (Guid.TryParse(userId, out var guid) && _employeesByLegacyPersonalInfoId.TryGetValue(guid, out var employeeId))
            return employeeId;
        return _employeesByLegacyUid.TryGetValue(userId, out var uidEmployeeId) ? uidEmployeeId : null;
    }

    private async Task<Guid?> ResolveEmployeeByLegacyStaffIdAsync(string? staffId)
    {
        if (string.IsNullOrWhiteSpace(staffId))
            return null;

        var normalized = staffId.Trim().ToUpperInvariant();
        return await _target.Employees.AsNoTracking()
            .Where(item => item.NormalizedStaffId == normalized)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<Guid?> ResolveEmployeeInstituteAsync(Guid employeeId)
    {
        if (_employeeInstitutesById.TryGetValue(employeeId, out var instituteId))
            return instituteId;

        return await _target.Employees.AsNoTracking()
            .Where(employee => employee.Id == employeeId)
            .Select(employee => (Guid?)employee.InstituteId)
            .FirstOrDefaultAsync();
    }

    private async Task<string?> ResolveUniqueEmployeeEmailAsync(string? email, Guid employeeId, string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim().ToUpperInvariant();
        if (!_stagedEmployeeEmails.Add(normalized))
        {
            AddIssue("LegacyAuthSpme", "PersonalInfos", sourceKey, "warning", "duplicate-email", $"Employee email '{email}' appears more than once in the legacy import batch and was not imported for this row.", new { email });
            return null;
        }

        var duplicate = await _target.Employees.AsNoTracking()
            .AnyAsync(item => item.Id != employeeId && item.NormalizedPrimaryEmail == normalized);
        if (duplicate)
        {
            AddIssue("LegacyAuthSpme", "PersonalInfos", sourceKey, "warning", "duplicate-email", $"Employee email '{email}' already exists in V2 and was not imported.", new { email });
            return null;
        }

        return email.Trim();
    }

    private async Task<string?> ResolveUniqueUserEmailAsync(string? email, string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim().ToUpperInvariant();
        if (!_stagedUserEmails.Add(normalized))
        {
            AddIssue("LegacyAuthSpme", "AspNetUsers", sourceKey, "warning", "duplicate-email", $"User email '{email}' appears more than once in the legacy import batch and was not imported for this row.", new { email });
            return null;
        }

        var duplicate = await _target.Users.AsNoTracking().AnyAsync(item => item.NormalizedEmail == normalized);
        if (duplicate)
        {
            AddIssue("LegacyAuthSpme", "AspNetUsers", sourceKey, "warning", "duplicate-email", $"User email '{email}' already exists in V2 and was not imported.", new { email });
            return null;
        }

        return email.Trim();
    }

    private void AddMapping(
        string sourceDatabase,
        string sourceTable,
        string sourceKey,
        string targetSchema,
        string targetTable,
        Guid targetId,
        string matchKey,
        string matchStrategy,
        object source)
    {
        var mappingKey = MappingKey(sourceDatabase, sourceTable, sourceKey);
        if (_existingMappings.ContainsKey(mappingKey))
            return;

        var checksum = Checksum(JsonSerializer.Serialize(source, _jsonOptions));
        _existingMappings[mappingKey] = targetId;
        if (WritesWorkingState)
        {
            _target.LegacyIdMappings.Add(new LegacyIdMapping(_run.Id, sourceDatabase, sourceTable, sourceKey, targetSchema, targetTable, targetId, matchKey, matchStrategy, checksum));
        }
    }

    private void AddIssue(string sourceDatabase, string sourceTable, string sourceKey, string severity, string code, string message, object payload)
    {
        _run.AddIssue();
        if (WritesWorkingState)
        {
            _target.LegacyImportIssues.Add(new LegacyImportIssue(_run.Id, sourceDatabase, sourceTable, sourceKey, severity, code, message, JsonSerializer.Serialize(payload, _jsonOptions)));
        }

        if (string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase))
            Console.WriteLine($"{severity}: {sourceDatabase}.{sourceTable}:{sourceKey} {code} - {message}");
    }

    private async Task SaveIfApplyAsync()
    {
        await _target.SaveChangesAsync();
    }

    private async Task<string> HashSourcesAsync()
    {
        var parts = new List<string>();
        foreach (var path in new[] { _options.AuthBackupPath, _options.SpmeBackupPath })
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            await using var stream = File.OpenRead(path);
            parts.Add(Convert.ToHexString(await SHA256.HashDataAsync(stream)));
        }

        return parts.Count == 0 ? "NO_BACKUP_PATHS_PROVIDED" : Checksum(string.Join("|", parts));
    }

    private static string NormalizeGender(string? value, string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "m" or "male" => "male",
            "f" or "female" => "female",
            _ => normalized
        };
    }

    private static string NormalizeServiceStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "active";

        return value.Trim().ToLowerInvariant() switch
        {
            "retired" => "retired",
            "contract" or "post retirement contract" => "contract",
            "active" or "active." or "activel" or "action" or "study leave" or "leave of absence" => "active",
            _ => "active"
        };
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string RequiredText(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private string LimitRequiredText(string? value, string fallback, int maxLength, string sourceTable, string sourceKey, string fieldName, object source)
    {
        var text = RequiredText(value, fallback);
        return LimitText(text, maxLength, sourceTable, sourceKey, fieldName, source) ?? fallback;
    }

    private string? LimitOptionalText(string? value, int maxLength, string sourceTable, string sourceKey, string fieldName, object source)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return LimitText(value.Trim(), maxLength, sourceTable, sourceKey, fieldName, source);
    }

    private string? LimitText(string? value, int maxLength, string sourceTable, string sourceKey, string fieldName, object source)
    {
        if (value is null || value.Length <= maxLength)
            return value;

        AddIssue("LegacyAuthSpme", sourceTable, sourceKey, "warning", "value-truncated", $"Field '{fieldName}' exceeded {maxLength} characters and was truncated for the active V2 record.", source);
        return value[..maxLength];
    }

    private static string CodeFrom(string? value, int id)
    {
        var code = new string((value ?? $"INST-{id}").Where(char.IsLetterOrDigit).Take(32).ToArray());
        return string.IsNullOrWhiteSpace(code) ? $"INST{id}" : code.ToUpperInvariant();
    }

    private static string NormalizeKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static string Checksum(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public static class LegacyValueParser
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "MM/dd/yyyy",
        "M/d/yyyy",
        "dd-MM-yyyy",
        "d-M-yyyy",
        "MMM d yyyy",
        "d MMM yyyy"
    ];

    public static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (DateTime.TryParseExact(trimmed, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var exact))
            return exact.Date;

        return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.Date
            : null;
    }

    public static DateTimeOffset? ParseDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTimeOffset.TryParse(
                value.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        return null;
    }
}

public readonly record struct LegacyRowPrecedence(DateTimeOffset Timestamp, Guid SourceId)
    : IComparable<LegacyRowPrecedence>
{
    public static LegacyRowPrecedence From(string? createdAt, string? updatedAt, Guid sourceId) =>
        new(
            LegacyValueParser.ParseDateTimeOffset(updatedAt) ??
            LegacyValueParser.ParseDateTimeOffset(createdAt) ??
            DateTimeOffset.MinValue,
            sourceId);

    public int CompareTo(LegacyRowPrecedence other)
    {
        var timestampComparison = Timestamp.CompareTo(other.Timestamp);
        return timestampComparison != 0 ? timestampComparison : SourceId.CompareTo(other.SourceId);
    }
}

public static class LegacyStaffCategoryMapper
{
    public static string? Map(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ");
        if (normalized.Contains("senior staff", StringComparison.Ordinal) || normalized == "senior")
            return "senior-staff";
        if (normalized.Contains("junior staff", StringComparison.Ordinal) || normalized == "junior")
            return "junior-staff";
        if (normalized.Contains("senior member", StringComparison.Ordinal) || normalized.Contains("member", StringComparison.Ordinal))
            return "senior-member";

        return null;
    }
}

internal sealed record TableCountRow(string SchemaName, string TableName, long Rows);
internal sealed record LegacyInstitute(int Id, string Name, string ShortName);
internal sealed record LegacyDivision(int Id, string Name, int InstituteId);
internal sealed record LegacySection(int Id, string Name, int DivisionId);
internal sealed record LegacyRole(string Id, string Name);
internal sealed record LegacyUser(
    string Id,
    string? UserName,
    string? Email,
    string? PhoneNumber,
    string? UID,
    string? StaffId,
    string? InstituteName,
    string? PasswordHash,
    bool EmailConfirmed,
    bool PhoneNumberConfirmed,
    bool LockoutEnabled,
    DateTimeOffset? LockoutEnd,
    int AccessFailedCount);
internal sealed record LegacyUserRole(string UserId, string RoleId);

internal sealed class LegacyPersonalInfo
{
    public Guid Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public string? Surname { get; set; }
    public string? OtherNames { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Gender { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Nationality { get; set; }
    public string? Religion { get; set; }
    public string? ServiceStatus { get; set; }
    public string? Designation { get; set; }
    public string? Grade { get; set; }
    public string? MaritalStatus { get; set; }
    public string? StaffId { get; set; }
    public string? AppointmentDate { get; set; }
    public string? PromotionDate { get; set; }
    public string? Institute { get; set; }
    public string? PensionType { get; set; }
    public string? PensionId { get; set; }
    public bool Verified { get; set; }
    public string? CurrentPosition { get; set; }
    public Guid? PositionTypeId { get; set; }
    public bool HrApproved { get; set; }
    public string? Prefix { get; set; }
    public int? DivisionId { get; set; }
    public int? SectionId { get; set; }
}

internal sealed record LegacySpouse(Guid Id, string? Name, string? Profession, string? Address, string UserId);
internal sealed record LegacyChild(Guid Id, string Name, string DateOfBirth, string Gender, string BirthCertId, Guid UserId);

internal sealed class LegacyEducation
{
    public Guid Id { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
    public string DateCommenced { get; set; } = string.Empty;
    public string DateCompleted { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string CourseStudied { get; set; } = string.Empty;
    public string CertificateAwarded { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string? Specialization { get; set; }
    public string? ProfesionalQualifications { get; set; }
    public string? Affiliations { get; set; }
    public string? CertificateNumber { get; set; }
}
