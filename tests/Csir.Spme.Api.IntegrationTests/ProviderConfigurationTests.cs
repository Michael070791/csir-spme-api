using Csir.Spme.Infrastructure;
using Csir.Spme.Infrastructure.Communications;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class ProviderConfigurationTests
{
    [Fact]
    public void Enabled_ZeptoMail_Rejects_Placeholder_Credentials()
    {
        using var services = CreateServices(new Dictionary<string, string?>
        {
            ["ZeptoMail:Enabled"] = "true",
            ["ZeptoMail:SendMailToken"] = "CHANGE_ME_USE_SECRET_PROVIDER",
            ["ZeptoMail:FromEmail"] = "verified-sender@example.test"
        });

        var read = () => services.GetRequiredService<IOptions<ZeptoMailOptions>>().Value;

        read.Should().Throw<OptionsValidationException>()
            .WithMessage("*ZeptoMail token and sender are required*");
    }

    [Fact]
    public void Enabled_MNotify_Rejects_Placeholder_Credentials()
    {
        using var services = CreateServices(new Dictionary<string, string?>
        {
            ["MNotify:Enabled"] = "true",
            ["MNotify:ApiKey"] = "CHANGE_ME_USE_SECRET_PROVIDER",
            ["MNotify:SenderId"] = "CSIR"
        });

        var read = () => services.GetRequiredService<IOptions<MNotifyOptions>>().Value;

        read.Should().Throw<OptionsValidationException>()
            .WithMessage("*MNotify API key*");
    }

    [Fact]
    public void Production_Enables_ZeptoMail_And_Dispatcher_When_Credentials_Are_Present()
    {
        using var services = CreateServices(new Dictionary<string, string?>
        {
            ["ZeptoMail:Enabled"] = "false",
            ["ZeptoMail:SendMailToken"] = "\"Zoho-enczapikey real-token\"",
            ["ZeptoMail:FromEmail"] = "\"admin@csir.test\"",
            ["Messaging:DispatcherEnabled"] = "false"
        }, Environments.Production);

        var zepto = services.GetRequiredService<IOptions<ZeptoMailOptions>>().Value;
        var messaging = services.GetRequiredService<IOptions<MessagingOptions>>().Value;

        zepto.Enabled.Should().BeTrue();
        zepto.SendMailToken.Should().Be("real-token");
        zepto.FromEmail.Should().Be("admin@csir.test");
        messaging.DispatcherEnabled.Should().BeTrue();
    }

    [Fact]
    public void Development_Does_Not_Auto_Enable_Dispatcher_Or_ZeptoMail()
    {
        using var services = CreateServices(new Dictionary<string, string?>
        {
            ["ZeptoMail:Enabled"] = "false",
            ["ZeptoMail:SendMailToken"] = "real-token",
            ["ZeptoMail:FromEmail"] = "admin@csir.test",
            ["Messaging:DispatcherEnabled"] = "false"
        }, Environments.Development);

        services.GetRequiredService<IOptions<ZeptoMailOptions>>().Value.Enabled.Should().BeFalse();
        services.GetRequiredService<IOptions<MessagingOptions>>().Value.DispatcherEnabled.Should().BeFalse();
    }

    private static ServiceProvider CreateServices(
        IReadOnlyDictionary<string, string?> overrides,
        string? environmentName = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["DatabaseProvider:UseSqlite"] = "true",
            ["DatabaseProvider:SqlitePath"] = ":memory:",
            ["Storage:Provider"] = "local",
            ["Storage:ContainerName"] = "spme-private",
            ["Storage:ReadUrlLifetime"] = "00:05:00",
            ["Messaging:DispatcherEnabled"] = "false"
        };
        foreach (var setting in overrides)
            settings[setting.Key] = setting.Value;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        if (!string.IsNullOrWhiteSpace(environmentName))
            services.AddSingleton<IHostEnvironment>(new StaticHostEnvironment(environmentName));
        services.AddInfrastructureServices(configuration);
        return services.BuildServiceProvider();
    }

    private sealed class StaticHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
