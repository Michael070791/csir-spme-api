using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Infrastructure.Communications;

internal sealed class CommunicationOptionsPostConfigure :
    IPostConfigureOptions<ZeptoMailOptions>,
    IPostConfigureOptions<MNotifyOptions>,
    IPostConfigureOptions<MessagingOptions>
{
    private readonly IHostEnvironment? _environment;
    private readonly IServiceProvider _services;

    public CommunicationOptionsPostConfigure(IServiceProvider services)
    {
        _services = services;
        _environment = services.GetService<IHostEnvironment>();
    }

    public void PostConfigure(string? name, ZeptoMailOptions options)
    {
        options.SendMailToken = ZeptoMailTransport.NormalizeSendMailToken(options.SendMailToken);
        options.AuthSendMailToken = ZeptoMailTransport.NormalizeSendMailToken(options.AuthSendMailToken);
        options.NotifySendMailToken = ZeptoMailTransport.NormalizeSendMailToken(options.NotifySendMailToken);
        options.FromEmail = ZeptoMailTransport.TrimConfiguredValue(options.FromEmail);
        options.AuthFromEmail = ZeptoMailTransport.TrimConfiguredValue(options.AuthFromEmail);
        options.NotifyFromEmail = ZeptoMailTransport.TrimConfiguredValue(options.NotifyFromEmail);
        options.FromName = ZeptoMailTransport.TrimConfiguredValue(options.FromName);
        options.AuthFromName = ZeptoMailTransport.TrimConfiguredValue(options.AuthFromName);
        options.NotifyFromName = ZeptoMailTransport.TrimConfiguredValue(options.NotifyFromName);

        if (options.Enabled || !IsProduction())
            return;

        if (!string.IsNullOrWhiteSpace(options.SendMailToken) &&
            !IsPlaceholder(options.SendMailToken) &&
            !string.IsNullOrWhiteSpace(options.FromEmail) &&
            !IsPlaceholder(options.FromEmail) &&
            System.Net.Mail.MailAddress.TryCreate(options.FromEmail, out _))
            options.Enabled = true;
    }

    public void PostConfigure(string? name, MNotifyOptions options)
    {
        options.ApiKey = ZeptoMailTransport.TrimConfiguredValue(options.ApiKey);
        options.SenderId = ZeptoMailTransport.TrimConfiguredValue(options.SenderId);
    }

    public void PostConfigure(string? name, MessagingOptions options)
    {
        if (options.DispatcherEnabled || !IsProduction())
            return;

        var zepto = _services.GetRequiredService<IOptions<ZeptoMailOptions>>().Value;
        var sms = _services.GetRequiredService<IOptions<MNotifyOptions>>().Value;
        if (zepto.Enabled || sms.Enabled)
            options.DispatcherEnabled = true;
    }

    private bool IsProduction() =>
        _environment is not null && _environment.IsProduction();

    private static bool IsPlaceholder(string value) =>
        value.Trim().StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
}
