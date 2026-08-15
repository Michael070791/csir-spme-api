namespace Csir.Spme.Infrastructure.Communications;

public sealed class ZeptoMailOptions
{
    public const string SectionName = "ZeptoMail";
    public bool Enabled { get; set; }
    public string ApiBaseUrl { get; set; } = "https://api.zeptomail.com";
    public string SendMailToken { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "CSIR SPME System";
    public string AuthSendMailToken { get; set; } = string.Empty;
    public string AuthFromEmail { get; set; } = string.Empty;
    public string AuthFromName { get; set; } = string.Empty;
    public string NotifySendMailToken { get; set; } = string.Empty;
    public string NotifyFromEmail { get; set; } = string.Empty;
    public string NotifyFromName { get; set; } = string.Empty;
    public string BounceAddress { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public bool TrackOpens { get; set; }
    public bool TrackClicks { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class MNotifyOptions
{
    public const string SectionName = "MNotify";
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string SenderId { get; set; } = "CSIR";
    public string BaseUrl { get; set; } = "https://api.mnotify.com/api";
    public string SmsEndpoint { get; set; } = "/sms/quick";
    public string DeliveryReportEndpoint { get; set; } = "/campaign/{campaignId}/{status}";
    public int RequestTimeoutSeconds { get; set; } = 8;
    public int RetryCount { get; set; } = 1;
    public int RetryDelayMilliseconds { get; set; } = 250;
    public int OtpExpiryMinutes { get; set; } = 10;
    public int OtpLength { get; set; } = 6;
    public string OtpMessageTemplate { get; set; } = "Your CSIR verification code is %otp_code%. It expires in %expiry% minutes.";
}

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";
    public bool DispatcherEnabled { get; set; } = true;
    public int WorkerBatchSize { get; set; } = 50;
    public int MaximumAttempts { get; set; } = 8;
    public int LeaseSeconds { get; set; } = 60;
}
