using Microsoft.Extensions.Configuration;

ConfigureLinuxDeveloperCertificateTrust();

var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator.WithDataVolume("csir-spme-v2-azurite-data"));
var blobs = storage.AddBlobs("blobs");

// Bind Kestrel directly to the documented launch-profile ports so ASPNETCORE_URLS
// stays http://localhost:5082 (and https://localhost:7443) across AppHost restarts.
// Default Aspire DCP proxying assigns a new dynamic target port each run, which
// breaks portal Vite proxies and leaves a hung listener on 5082.
var api = builder.AddProject<Projects.Csir_Spme_Api>("spme-api")
    .WithEndpoint(
        port: 5082,
        scheme: "http",
        name: "http",
        isExternal: true,
        isProxied: false)
    .WithEndpoint(
        port: 7443,
        scheme: "https",
        name: "https",
        isExternal: true,
        isProxied: false)
    .WithExternalHttpEndpoints()
    .WithEnvironment("DatabaseProvider__UseSqlite", "false")
    .WithEnvironment("Storage__Provider", "azure-blob")
    .WithEnvironment("Storage__ContainerName", "spme-private")
    .WithEnvironment("Storage__CreateContainer", "true")
    .WithEnvironment("Jwt__Issuer", "csir-spme-api")
    .WithEnvironment("Jwt__Audience", "csir-spme-client")
    .WithEnvironment("Identity__SeedAdmin__UserName", "platform.admin")
    .WithEnvironment("Identity__SeedAdmin__Email", "platform.admin@csir.local")
    .WithEnvironment("Identity__SeedHrAdmin__UserName", "hr.admin")
    .WithEnvironment("Identity__SeedHrAdmin__Email", "hr.admin@csir.local")
    .WithEnvironment("Identity__SeedHrAdmin__InstituteCode", "DEV-HR")
    .WithReference(blobs)
    .WaitFor(blobs);
var jwtKey = builder.AddParameter("jwt-key", secret: true);
var accountActivationHashKey = builder.AddParameter("account-activation-hash-key", secret: true);
var passwordResetHashKey = builder.AddParameter("password-reset-hash-key", secret: true);
var seedAdminPassword = builder.AddParameter("seed-admin-password", secret: true);
api.WithEnvironment("Jwt__Key", jwtKey)
    .WithEnvironment("AccountActivation__HashKey", accountActivationHashKey)
    .WithEnvironment("PasswordReset__HashKey", passwordResetHashKey)
    .WithEnvironment("Identity__SeedAdmin__Password", seedAdminPassword);

// Optional: set SPME_SEED_HR_ADMIN_PASSWORD (or Parameters:seed-hr-admin-password) to seed local HrAdmin.
var seedHrAdminPassword = builder.Configuration["SPME_SEED_HR_ADMIN_PASSWORD"]
    ?? builder.Configuration["Parameters:seed-hr-admin-password"]
    ?? string.Empty;
api.WithEnvironment("Identity__SeedHrAdmin__Password", seedHrAdminPassword);

var databaseMode = builder.Configuration["ASPIRE_DATABASE_MODE"]?.Trim().ToLowerInvariant() ?? "persistent";
var applyDatabaseMigrations = builder.Configuration.GetValue<bool?>("ASPIRE_APPLY_DATABASE_MIGRATIONS") ?? false;
if (databaseMode == "external")
{
    var externalDatabase = builder.AddConnectionString("DefaultConnection");
    var preflight = builder.AddProject<Projects.Csir_Spme_Tools_DatabaseMigrator>("db-preflight")
        .WithEnvironment("DatabaseProvider__UseSqlite", "false")
        .WithEnvironment("DatabaseMigration__Apply", "false")
        .WithEnvironment("DatabaseMigration__ConnectionTimeoutSeconds", "45")
        .WithReference(externalDatabase, "DefaultConnection");
    api.WithReference(externalDatabase)
        .WaitForCompletion(preflight);
}
else if (databaseMode is "persistent" or "container")
{
    var sqlDataVolume = builder.Configuration["ASPIRE_SQL_DATA_VOLUME"]?.Trim();
    if (string.IsNullOrWhiteSpace(sqlDataVolume))
        sqlDataVolume = "csir-spme-v2-sql-data";
    if (!IsSafeDockerVolumeName(sqlDataVolume))
        throw new InvalidOperationException("ASPIRE_SQL_DATA_VOLUME contains unsupported characters.");

    var sqlHostPort = builder.Configuration.GetValue<int?>("ASPIRE_SQL_HOST_PORT") ?? 15433;
    if (sqlHostPort is < 1 or > 65535)
        throw new InvalidOperationException("ASPIRE_SQL_HOST_PORT must be between 1 and 65535.");

    var sqlPassword = builder.AddParameter("sql-password", secret: true);
    var sqlServer = builder.AddSqlServer("spme-sql", sqlPassword, sqlHostPort)
        .WithDataVolume(sqlDataVolume)
        .WithLifetime(ContainerLifetime.Persistent);
    var database = sqlServer.AddDatabase("spme-database", "CsirSpmeV2");
    var preflight = builder.AddProject<Projects.Csir_Spme_Tools_DatabaseMigrator>("db-preflight")
        .WithEnvironment("DatabaseProvider__UseSqlite", "false")
        .WithEnvironment("DatabaseMigration__Apply", applyDatabaseMigrations ? "true" : "false")
        .WithEnvironment("DatabaseMigration__ConnectionTimeoutSeconds", "45")
        .WithReference(database, "DefaultConnection");
    api.WithReference(database, "DefaultConnection")
        .WaitForCompletion(preflight);
}
else
{
    throw new InvalidOperationException(
        "ASPIRE_DATABASE_MODE must be 'persistent' (the legacy 'container' alias is accepted) or 'external'.");
}

builder.Build().Run();

static bool IsSafeDockerVolumeName(string value) =>
    value.Length <= 128 && value.All(character =>
        char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

static void ConfigureLinuxDeveloperCertificateTrust()
{
    if (!OperatingSystem.IsLinux())
        return;

    var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    if (string.IsNullOrWhiteSpace(homeDirectory))
        return;

    var trustDirectory = Path.Combine(homeDirectory, ".aspnet", "dev-certs", "trust");
    if (!Directory.Exists(trustDirectory))
        return;

    var currentCertificateDirectories = Environment.GetEnvironmentVariable("SSL_CERT_DIR");
    if (currentCertificateDirectories?.Split(':', StringSplitOptions.RemoveEmptyEntries).Contains(trustDirectory) == true)
        return;

    var updatedCertificateDirectories = string.IsNullOrWhiteSpace(currentCertificateDirectories)
        ? $"{trustDirectory}:/usr/lib/ssl/certs"
        : $"{trustDirectory}:{currentCertificateDirectories}";

    Environment.SetEnvironmentVariable("SSL_CERT_DIR", updatedCertificateDirectories);
}
