using Csir.Spme.Infrastructure;
using Csir.Spme.Infrastructure.Communications;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    private static ServiceProvider CreateServices(IReadOnlyDictionary<string, string?> overrides)
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
        services.AddInfrastructureServices(configuration);
        return services.BuildServiceProvider();
    }
}
