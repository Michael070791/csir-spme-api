using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Infrastructure.Jobs;
using Csir.Spme.Infrastructure.Persistence;
using Csir.Spme.Infrastructure.Storage;
using Csir.Spme.Infrastructure.Communications;
using Csir.Spme.Application.Iam;
using Csir.Spme.Application.Promotions;
using Csir.Spme.Application.Reporting;
using Csir.Spme.Application.Hr;
using Csir.Spme.Infrastructure.Identity;
using Csir.Spme.Infrastructure.Workflow;

namespace Csir.Spme.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        var useSqlite = configuration.GetValue<bool>("DatabaseProvider:UseSqlite");

        if (useSqlite)
        {
            var sqlitePath = configuration.GetValue<string>("DatabaseProvider:SqlitePath")
                ?? "csir-spme-v2.db";
            services.AddSingleton(new RowVersionMapping { UseSqlServerRowVersion = false });
            services.AddDbContext<SpmeDbContext>(options =>
            {
                options.UseSqlite($"Data Source={sqlitePath}");
                IgnorePendingModelChangesWhenApplyingMigrations(options, configuration);
            });
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required when SQLite is disabled.");
            services.AddSingleton(new RowVersionMapping
            {
                UseSqlServerRowVersion = SqlServerRowVersionDetector.UsesStoreGeneratedRowVersion(connectionString)
            });
            services.AddDbContext<SpmeDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
                IgnorePendingModelChangesWhenApplyingMigrations(options, configuration);
            });
        }

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<SpmeDbContext>());
        services.AddScoped<IReportingPeriodRepository>(sp => sp.GetRequiredService<SpmeDbContext>());
        services.AddScoped<IReportRepository>(sp => sp.GetRequiredService<SpmeDbContext>());
        services.AddScoped<IStaffQuarterlyReportRepository>(sp => sp.GetRequiredService<SpmeDbContext>());
        services.AddScoped<IPromotionReportRepository>(sp => sp.GetRequiredService<SpmeDbContext>());
        services.AddScoped<ITechnologyRepository>(sp => sp.GetRequiredService<SpmeDbContext>());
        services.AddScoped<IProjectRepository>(sp => sp.GetRequiredService<SpmeDbContext>());
        services.AddScoped<IStrategicPlanRepository>(sp => sp.GetRequiredService<SpmeDbContext>());
        services.AddScoped<IThrustRepository>(sp => sp.GetRequiredService<SpmeDbContext>());
        services.AddScoped<IOutputRepository>(sp => sp.GetRequiredService<SpmeDbContext>());
        services.AddScoped<IIndicatorRepository>(sp => sp.GetRequiredService<SpmeDbContext>());
        services.AddScoped<IIndicatorMeasurementRepository>(sp => sp.GetRequiredService<SpmeDbContext>());
        services.AddScoped<ILeaveRequestRepository>(sp => sp.GetRequiredService<SpmeDbContext>());
        services.AddScoped<IInstituteDirectory, InstituteDirectory>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddOptions<AppraisalReminderOptions>()
            .Bind(configuration.GetSection(AppraisalReminderOptions.SectionName))
            .Validate(options => options.InitialDelaySeconds >= 1 && options.IntervalMinutes >= 1,
                "Appraisal reminder scheduling intervals must be positive.")
            .ValidateOnStart();
        services.AddScoped<AppraisalReminderService>();
        services.AddHostedService<AppraisalReminderHostedService>();
        services.AddScoped<IAccountActivationService, AccountActivationService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddSingleton(TimeProvider.System);

        // Storage
        services.AddOptions<BlobStorageOptions>()
            .Bind(configuration.GetSection(BlobStorageOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ContainerName) &&
                    System.Text.RegularExpressions.Regex.IsMatch(
                        options.ContainerName,
                        "^[a-z0-9](?!.*--)[a-z0-9-]{1,61}[a-z0-9]$"),
                "Storage:ContainerName must be a valid Azure Blob container name.")
            .Validate(
                options => options.ReadUrlLifetime > TimeSpan.Zero && options.ReadUrlLifetime <= TimeSpan.FromMinutes(15),
                "Storage:ReadUrlLifetime must be between one tick and 15 minutes.")
            .ValidateOnStart();
        services.AddSingleton<IProfileImageProcessor, ProfileImageProcessor>();

        var storageProvider = (configuration.GetValue<string>("Storage:Provider") ?? "azure-blob")
            .Trim()
            .ToLowerInvariant();
        if (storageProvider is "azure" or "azure-blob")
        {
            services.AddSingleton(sp => AzureBlobStorageService.CreateClient(
                configuration,
                sp.GetRequiredService<IHostEnvironment>()));
            services.AddSingleton<AzureBlobStorageService>();
            services.AddSingleton<IFileStorageService>(sp => sp.GetRequiredService<AzureBlobStorageService>());
            services.AddSingleton<IDirectFileUploadService>(sp => sp.GetRequiredService<AzureBlobStorageService>());
            services.AddHostedService<DeletedFileCleanupService>();
        }
        else if (storageProvider == "local")
        {
            services.AddSingleton<LocalFileStorageService>();
            services.AddSingleton<IFileStorageService>(sp => sp.GetRequiredService<LocalFileStorageService>());
            services.AddSingleton<IDirectFileUploadService>(sp => sp.GetRequiredService<LocalFileStorageService>());
        }
        else
        {
            throw new InvalidOperationException(
                "Storage:Provider must be 'azure-blob' or the explicitly selected development-only 'local' provider.");
        }
        services.AddSingleton<IPromotionMalwareScanner, DeferredPromotionMalwareScanner>();
        services.AddOptions<PromotionUploadOptions>()
            .Bind(configuration.GetSection(PromotionUploadOptions.SectionName))
            .Validate(options => options.MaximumFileBytes > 0 && options.UploadSessionMinutes is >= 5 and <= 1440,
                "Promotion upload limits are invalid.")
            .ValidateOnStart();
        services.AddOptions<StaffReportUploadOptions>()
            .Bind(configuration.GetSection(StaffReportUploadOptions.SectionName))
            .Validate(options => options.ConceptNoteMaximumFileBytes > 0 &&
                                 options.ImageMaximumFileBytes > 0 &&
                                 options.MaximumImagesPerReport is >= 1 and <= 10 &&
                                 options.UploadSessionMinutes is >= 5 and <= 1440,
                "Staff report upload limits are invalid.")
            .ValidateOnStart();
        services.AddOptions<ProfileDocumentOptions>()
            .Bind(configuration.GetSection(ProfileDocumentOptions.SectionName))
            .Validate(options => options.MaximumFileBytes > 0 && options.UploadSessionMinutes is >= 5 and <= 1440,
                "Profile document upload limits are invalid.")
            .ValidateOnStart();

        // Durable provider-neutral communications
        services.AddOptions<ZeptoMailOptions>()
            .Bind(configuration.GetSection(ZeptoMailOptions.SectionName))
            .Validate(options => !options.Enabled ||
                (!string.IsNullOrWhiteSpace(options.SendMailToken) &&
                 !IsPlaceholder(options.SendMailToken) &&
                 !string.IsNullOrWhiteSpace(options.FromEmail) &&
                 !IsPlaceholder(options.FromEmail) &&
                 System.Net.Mail.MailAddress.TryCreate(options.FromEmail, out _)),
                "ZeptoMail token and sender are required when ZeptoMail is enabled.")
            .Validate(options => Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                "ZeptoMail:ApiBaseUrl must be an absolute HTTPS URL.")
            .Validate(options => !options.Enabled ||
                (SenderOverrideIsValid(options.AuthSendMailToken, options.AuthFromEmail) &&
                 SenderOverrideIsValid(options.NotifySendMailToken, options.NotifyFromEmail)),
                "ZeptoMail category-specific token and sender settings must be supplied together.")
            .Validate(options => options.TimeoutSeconds is >= 1 and <= 120,
                "ZeptoMail:TimeoutSeconds must be between 1 and 120.")
            .ValidateOnStart();
        services.AddOptions<MNotifyOptions>()
            .Bind(configuration.GetSection(MNotifyOptions.SectionName))
            .Validate(options => !options.Enabled ||
                (!string.IsNullOrWhiteSpace(options.ApiKey) &&
                 !IsPlaceholder(options.ApiKey) &&
                 !string.IsNullOrWhiteSpace(options.SenderId) &&
                 options.SenderId.Length <= 11),
                "MNotify API key and a sender ID of at most 11 characters are required when MNotify is enabled.")
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                "MNotify:BaseUrl must be an absolute HTTPS URL.")
            .Validate(options => options.RequestTimeoutSeconds is >= 1 and <= 120 &&
                options.RetryCount is >= 0 and <= 3 && options.RetryDelayMilliseconds is >= 0 and <= 5000,
                "MNotify retry and timeout settings are outside the supported bounds.")
            .ValidateOnStart();
        services.AddOptions<MessagingOptions>()
            .Bind(configuration.GetSection(MessagingOptions.SectionName))
            .Validate(options => options.WorkerBatchSize is >= 1 and <= 200 &&
                options.MaximumAttempts is >= 1 and <= 20 && options.LeaseSeconds is >= 10 and <= 300,
                "Messaging worker settings are outside the supported bounds.")
            .ValidateOnStart();
        services.AddSingleton<CommunicationOptionsPostConfigure>();
        services.AddSingleton<Microsoft.Extensions.Options.IPostConfigureOptions<ZeptoMailOptions>>(
            sp => sp.GetRequiredService<CommunicationOptionsPostConfigure>());
        services.AddSingleton<Microsoft.Extensions.Options.IPostConfigureOptions<MNotifyOptions>>(
            sp => sp.GetRequiredService<CommunicationOptionsPostConfigure>());
        services.AddSingleton<Microsoft.Extensions.Options.IPostConfigureOptions<MessagingOptions>>(
            sp => sp.GetRequiredService<CommunicationOptionsPostConfigure>());
        services.AddOptions<PasswordResetOptions>()
            .Bind(configuration.GetSection(PasswordResetOptions.SectionName))
            .Validate(options => options.TokenLifespan == TimeSpan.FromHours(24),
                "PasswordReset:TokenLifespan must be exactly 24 hours.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.HashKey) &&
                System.Text.Encoding.UTF8.GetByteCount(options.HashKey) >= 32 &&
                !IsPlaceholder(options.HashKey),
                "PasswordReset:HashKey must be a non-placeholder secret of at least 32 UTF-8 bytes.")
            .ValidateOnStart();
        services.AddOptions<PortalUrlOptions>()
            .Bind(configuration.GetSection(PortalUrlOptions.SectionName))
            .Validate<IHostEnvironment>((options, environment) =>
                ValidPortalUri(options.StaffPasswordResetUrl, environment.IsDevelopment()) &&
                ValidPortalUri(options.HrPasswordResetUrl, environment.IsDevelopment()) &&
                ValidPortalUri(options.StaffPortalUrl, environment.IsDevelopment()) &&
                ValidPortalUri(options.HrPortalUrl, environment.IsDevelopment()) &&
                (string.IsNullOrWhiteSpace(options.LogoUrl) ||
                 ValidPortalUri(options.LogoUrl, environment.IsDevelopment())),
                "Portal URLs must be absolute and must use HTTPS outside Development.")
            .ValidateOnStart();
        services.AddSingleton<BrandedEmailRenderer>();

        services.AddHttpClient<IEmailTransport, ZeptoMailTransport>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ZeptoMailOptions>>().Value;
            client.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/'));
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }).RemoveAllLoggers();
        services.AddHttpClient<ISmsTransport, MNotifyTransport>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MNotifyOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        }).RemoveAllLoggers();
        services.AddSingleton<CommunicationDispatchPulse>();
        services.AddScoped<ICommunicationOutbox, DurableCommunicationOutbox>();
        services.AddScoped<IWorkflowNotificationOutbox, WorkflowNotificationOutbox>();
        services.AddScoped<IWorkflowApprovalTokenService, WorkflowApprovalTokenService>();
        services.AddScoped<IWorkflowApproverResolver, WorkflowApproverResolver>();
        services.AddScoped<IEmailService, DurableEmailService>();
        services.AddScoped<ISmsService, DurableSmsService>();
        services.AddHostedService<CommunicationOutboxDispatcher>();

        return services;
    }

    private static bool IsPlaceholder(string value) =>
        value.Trim().StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase);

    private static bool SenderOverrideIsValid(string token, string email) =>
        string.IsNullOrWhiteSpace(token) == string.IsNullOrWhiteSpace(email) &&
        (string.IsNullOrWhiteSpace(token) || (!IsPlaceholder(token) && !IsPlaceholder(email) &&
            System.Net.Mail.MailAddress.TryCreate(email, out _)));

    private static bool ValidPortalUri(string? value, bool isDevelopment) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || (isDevelopment && uri.Scheme == Uri.UriSchemeHttp));

    private static void IgnorePendingModelChangesWhenApplyingMigrations(
        DbContextOptionsBuilder options,
        IConfiguration configuration)
    {
        if (!configuration.GetValue("DatabaseMigration:Apply", false))
            return;

        options.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }
}
