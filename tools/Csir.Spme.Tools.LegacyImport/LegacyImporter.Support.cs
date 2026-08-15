using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Iam;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Tools.LegacyImport;

internal sealed partial class LegacyImporter
{
    private static readonly IReadOnlyDictionary<string, string[]> RequiredAuthSchema =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["AspNetRoles"] = ["Id", "Name"],
            ["AspNetUsers"] = ["Id", "UserName", "Email", "PasswordHash", "UID", "InstituteName"],
            ["AspNetUserRoles"] = ["UserId", "RoleId"],
            ["PersonalInfos"] = ["Id", "UID", "StaffId", "Institute", "Designation", "Gender"],
            ["ChildInfos"] = ["Id", "UserId", "Name", "DateOfBirth", "Gender"],
            ["EducationInfos"] = ["Id", "UserId", "InstitutionName", "CourseStudied"],
            ["PositionTypes"] = ["Id", "Name", "AnnualLeaveDays"],
            ["EmployeeLeaveRecords"] = ["Id", "UserId", "LeaveType", "Year", "TotalDays", "UsedDays"],
            ["Holidays"] = ["Id", "Name", "Date"],
            ["LeaveRequests"] = ["Id", "UserId", "LeaveType", "StartDate", "EndDate", "Status"],
            ["LeaveApprovals"] = ["Id", "LeaveRequestId", "ApproverUserId", "ApprovalStage", "IsApproved"],
            ["LeaveHandovers"] = ["Id", "LeaveRequestId", "HandoverNotes"],
            ["LeaveResumptions"] = ["Id", "LeaveRequestId", "EmployeeId", "ResumptionDate", "Status"]
        };

    private static readonly IReadOnlyDictionary<string, string[]> RequiredSpmeSchema =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Institutes"] = ["Id", "Name", "ShortName"],
            ["Divisions"] = ["Id", "Name", "InstituteId"],
            ["Sections"] = ["Id", "Name", "DivisionId"],
            ["Thrusts"] = ["Id", "UID", "InstituteId", "Description", "Objective"],
            ["Outputs"] = ["Id", "OutputIdNumber", "Description", "ThrustId"],
            ["Indicators"] = ["Id", "OutputId", "Description", "Baseline", "Target", "OVI"],
            ["IndicatorData"] = ["Id", "IndicatorId", "Achieved", "Period", "Year"],
            ["Projects"] = ["Id", "Name", "Institute", "Objective", "Status"],
            ["Reports"] = ["Id", "Institute", "Period", "Year", "TypeOfReport", "Summary"],
            ["TechnologyInfo"] = ["Id", "Name", "Institute", "Description", "ApplicationArea"],
            ["Memos"] = ["Id", "Title", "Body", "PublishedByUserId", "PublishedAt"],
            ["MemoInstitutes"] = ["Id", "MemoId", "InstituteId"],
            ["Notifications"] = ["Id", "UserId", "Title", "Message", "IsRead"]
        };

    private async Task ValidateSourceSchemasAsync(
        SqlConnection legacyAuth,
        SqlConnection legacySpme,
        CancellationToken cancellationToken)
    {
        await ValidateSchemaAsync(legacyAuth, "LegacyAuthSpme", RequiredAuthSchema, cancellationToken);
        await ValidateSchemaAsync(legacySpme, "LegacySpme", RequiredSpmeSchema, cancellationToken);
    }

    private static async Task ValidateSchemaAsync(
        SqlConnection connection,
        string database,
        IReadOnlyDictionary<string, string[]> required,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select TABLE_NAME as TableName, COLUMN_NAME as ColumnName
            from INFORMATION_SCHEMA.COLUMNS
            where TABLE_SCHEMA = 'dbo'
            """;
        var rows = await connection.QueryAsync<SchemaColumn>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        var actual = rows
            .GroupBy(row => row.TableName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        var missing = new List<string>();
        foreach (var table in required)
        {
            if (!actual.TryGetValue(table.Key, out var columns))
            {
                missing.Add($"dbo.{table.Key}");
                continue;
            }

            missing.AddRange(table.Value
                .Where(column => !columns.Contains(column))
                .Select(column => $"dbo.{table.Key}.{column}"));
        }

        if (missing.Count > 0)
            throw new InvalidOperationException($"{database} schema validation failed. Missing: {string.Join(", ", missing)}");
    }

    private async Task LoadExistingMappingsAsync(CancellationToken cancellationToken)
    {
        var mappings = await _target.LegacyIdMappings.AsNoTracking()
            .OrderByDescending(mapping => mapping.CreatedAt)
            .Select(mapping => new
            {
                mapping.SourceDatabase,
                mapping.SourceTable,
                mapping.SourceKey,
                mapping.TargetId
            })
            .ToListAsync(cancellationToken);

        foreach (var mapping in mappings)
            _existingMappings.TryAdd(MappingKey(mapping.SourceDatabase, mapping.SourceTable, mapping.SourceKey), mapping.TargetId);

        var linkedUsers = await _target.Users.AsNoTracking()
            .Where(user => user.EmployeeId.HasValue)
            .Select(user => new { EmployeeId = user.EmployeeId!.Value, user.Id })
            .ToListAsync(cancellationToken);
        foreach (var user in linkedUsers)
            _usersByEmployeeId.TryAdd(user.EmployeeId, user.Id);
    }

    private Guid? GetExistingMapping(string sourceDatabase, string sourceTable, string sourceKey) =>
        _existingMappings.TryGetValue(MappingKey(sourceDatabase, sourceTable, sourceKey), out var id) ? id : null;

    private static string MappingKey(string sourceDatabase, string sourceTable, string sourceKey) =>
        $"{sourceDatabase}\u001f{sourceTable}\u001f{sourceKey}";

    private async Task<Guid?> ResolveEmployeeByLegacyEmailAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim().ToUpperInvariant();
        var matches = await _target.Employees.AsNoTracking()
            .Where(employee => employee.NormalizedPrimaryEmail == normalized)
            .Select(employee => employee.Id)
            .Take(2)
            .ToListAsync();
        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task<Guid?> ResolveInstituteOrNullAsync(string? sourceText)
    {
        await WarmInstituteLookupAsync();
        return !string.IsNullOrWhiteSpace(sourceText) &&
               _institutesByNormalizedText.TryGetValue(NormalizeKey(sourceText), out var instituteId)
            ? instituteId
            : null;
    }

    private async Task EnsureEmployeeAccountsAsync(CancellationToken cancellationToken)
    {
        var employeeRoleId = await _target.Roles.AsNoTracking()
            .Where(role => role.NormalizedName == "EMPLOYEE")
            .Select(role => (Guid?)role.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!employeeRoleId.HasValue)
            throw new InvalidOperationException("The target Employee Identity role must be seeded before legacy import.");

        var employees = await _target.Employees.AsNoTracking()
            .Select(employee => new
            {
                employee.Id,
                employee.InstituteId,
                employee.StaffId,
                employee.PrimaryEmail,
                employee.NormalizedPrimaryEmail
            })
            .ToListAsync(cancellationToken);
        var userNames = (await _target.Users.AsNoTracking()
                .Where(user => user.NormalizedUserName != null)
                .Select(user => user.NormalizedUserName!)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var userEmails = (await _target.Users.AsNoTracking()
                .Where(user => user.NormalizedEmail != null)
                .Select(user => user.NormalizedEmail!)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stagedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var employee in employees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_usersByEmployeeId.TryGetValue(employee.Id, out var userId))
            {
                var baseName = string.IsNullOrWhiteSpace(employee.StaffId)
                    ? $"employee-{employee.Id:N}"
                    : employee.StaffId.Trim();
                var userName = baseName;
                if (!userNames.Add(userName.ToUpperInvariant()))
                {
                    userName = $"employee-{baseName}-{employee.Id:N}"[..Math.Min(64, $"employee-{baseName}-{employee.Id:N}".Length)];
                    userNames.Add(userName.ToUpperInvariant());
                }

                var user = new User(userName, "Employee");
                var email = employee.PrimaryEmail;
                if (!string.IsNullOrWhiteSpace(employee.NormalizedPrimaryEmail) &&
                    userEmails.Add(employee.NormalizedPrimaryEmail))
                {
                    user.Email = email;
                    user.NormalizedEmail = employee.NormalizedPrimaryEmail;
                }

                user.LinkEmployee(employee.Id, employee.InstituteId);
                user.MarkPasswordResetRequired();
                userId = user.Id;
                _usersByEmployeeId[employee.Id] = userId;
                if (WritesWorkingState)
                    _target.Users.Add(user);
                _run.AddInserted();
                AddIssue(
                    "LegacyAuthSpme",
                    "PersonalInfos",
                    employee.Id.ToString(),
                    "info",
                    "employee-account-provisioned",
                    "A reset-required employee account was provisioned so self-service data remains addressable.",
                    new { employee.Id, employee.InstituteId });
            }

            var assignmentKey = $"{userId:N}:{employeeRoleId.Value:N}";
            var exists = await _target.Set<IdentityUserRole<Guid>>().AsNoTracking()
                .AnyAsync(role => role.UserId == userId && role.RoleId == employeeRoleId.Value, cancellationToken);
            if (!exists && stagedRoles.Add(assignmentKey))
            {
                if (WritesWorkingState)
                {
                    _target.Set<IdentityUserRole<Guid>>().Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = employeeRoleId.Value });
                    _target.AuditRecords.Add(new AuditRecord(
                        _migrationActorUserId,
                        "legacy-import.role-assigned",
                        "UserRole",
                        _run.Id.ToString())
                    {
                        TargetId = userId.ToString(),
                        AfterSummary = JsonSerializer.Serialize(new
                        {
                            roleId = employeeRoleId.Value,
                            roleName = "Employee",
                            strategy = "provisioned-employee-self-service",
                            employeeId = employee.Id
                        }, _jsonOptions)
                    });
                }
                _run.AddInserted();
            }
        }

        await SaveIfApplyAsync();
    }

    private async Task RecordReconciliationAsync(
        IReadOnlyDictionary<string, int> rowCounts,
        SqlConnection legacyAuth,
        SqlConnection legacySpme,
        CancellationToken cancellationToken)
    {
        var source = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in rowCounts.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            var parts = table.Key.Split('.', 3);
            var connection = parts[0].Equals("LegacyAuthSpme", StringComparison.OrdinalIgnoreCase)
                ? legacyAuth
                : legacySpme;
            var checksum = await ReadChecksumAsync(connection, parts[1], parts[2], cancellationToken);
            source[table.Key] = new { rows = table.Value, checksum };
        }

        var targetCounts = await _target.Database.SqlQueryRaw<TargetTableCount>("""
                select s.name as SchemaName, t.name as TableName, sum(p.rows) as [Rows]
                from sys.tables t
                join sys.schemas s on s.schema_id = t.schema_id
                join sys.partitions p on p.object_id = t.object_id and p.index_id in (0, 1)
                group by s.name, t.name
                """)
            .ToListAsync(cancellationToken);

        var instituteCounts = new
        {
            employees = await _target.Employees.AsNoTracking().GroupBy(item => item.InstituteId)
                .Select(group => new { instituteId = group.Key, rows = group.Count() }).ToListAsync(cancellationToken),
            leaveRequests = await _target.LeaveRequests.AsNoTracking().GroupBy(item => item.InstituteId)
                .Select(group => new { instituteId = group.Key, rows = group.Count() }).ToListAsync(cancellationToken),
            projects = await _target.Projects.AsNoTracking().GroupBy(item => item.InstituteId)
                .Select(group => new { instituteId = group.Key, rows = group.Count() }).ToListAsync(cancellationToken),
            reports = await _target.Reports.AsNoTracking().GroupBy(item => item.InstituteId)
                .Select(group => new { instituteId = group.Key, rows = group.Count() }).ToListAsync(cancellationToken),
            technologies = await _target.Technologies.AsNoTracking().GroupBy(item => item.InstituteId)
                .Select(group => new { instituteId = group.Key, rows = group.Count() }).ToListAsync(cancellationToken),
            memos = await _target.Memos.AsNoTracking().GroupBy(item => item.InstituteId)
                .Select(group => new { instituteId = group.Key, rows = group.Count() }).ToListAsync(cancellationToken)
        };

        var reconciliation = JsonSerializer.Serialize(new
        {
            source,
            target = targetCounts.OrderBy(item => item.SchemaName).ThenBy(item => item.TableName),
            byInstitute = instituteCounts,
            generatedAt = DateTimeOffset.UtcNow
        }, _jsonOptions);
        _run.RecordReconciliation(reconciliation);
        await SaveIfApplyAsync();
    }

    private static async Task<int?> ReadChecksumAsync(
        SqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        if (!schema.All(char.IsLetterOrDigit) || !table.All(character => char.IsLetterOrDigit(character) || character == '_'))
            throw new InvalidOperationException("Unsafe schema/table identifier in reconciliation.");

        var sql = $"select CHECKSUM_AGG(BINARY_CHECKSUM(*)) from [{schema}].[{table}]";
        return await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private sealed record SchemaColumn(string TableName, string ColumnName);
    private sealed record TargetTableCount(string SchemaName, string TableName, long Rows);
}
