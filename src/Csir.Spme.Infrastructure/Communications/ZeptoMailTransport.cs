using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Csir.Spme.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Infrastructure.Communications;

public sealed class ZeptoMailTransport : IEmailTransport
{
    private readonly HttpClient _httpClient;
    private readonly ZeptoMailOptions _options;

    public ZeptoMailTransport(HttpClient httpClient, IOptions<ZeptoMailOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task<CommunicationTransportResult> SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml,
        string category,
        CancellationToken ct = default) =>
        SendAsync(to, subject, body, isHtml, null, category, ct);

    public async Task<CommunicationTransportResult> SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml,
        string? textBody,
        string category,
        CancellationToken ct = default,
        IReadOnlyList<EmailAttachment>? attachments = null)
    {
        var sender = ResolveSender(category);
        var token = NormalizeSendMailToken(sender.Token);
        var fromName = ResolveFromName(sender.Name);
        if (!_options.Enabled || string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(sender.Email))
            return Rejected("provider_disabled", null, false);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1.1/email");
        request.Headers.TryAddWithoutValidation("Authorization", $"Zoho-enczapikey {token}");
        request.Content = JsonContent.Create(new ZeptoMailRequest(
            new ZeptoAddress(sender.Email.Trim(), fromName),
            [new ZeptoRecipient(new ZeptoAddress(to.Trim(), to.Trim()))],
            subject.Trim(),
            isHtml ? body : null,
            isHtml ? textBody : body,
            string.IsNullOrWhiteSpace(_options.BounceAddress) ? null : _options.BounceAddress.Trim(),
            _options.TrackOpens,
            _options.TrackClicks,
            MapAttachments(attachments)));

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (response.IsSuccessStatusCode)
            return new CommunicationTransportResult(
                true,
                "zeptomail",
                TryReadString(payload, "request_id") ?? TryReadString(payload, "message_id") ??
                    TryReadString(payload, "message_uuid"),
                null,
                (int)response.StatusCode,
                false);

        var transient = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
            (int)response.StatusCode >= 500;
        return Rejected(ClassifyError(response.StatusCode), (int)response.StatusCode, transient);
    }

    private (string Token, string Email, string Name) ResolveSender(string category)
    {
        if (string.Equals(category, "authentication", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(_options.AuthSendMailToken))
            return (_options.AuthSendMailToken, _options.AuthFromEmail, _options.AuthFromName);

        if ((string.Equals(category, "notification", StringComparison.OrdinalIgnoreCase) ||
             category.StartsWith("leave-", StringComparison.OrdinalIgnoreCase) ||
             category.StartsWith("staff-quarterly-report", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(_options.NotifySendMailToken))
            return (_options.NotifySendMailToken, _options.NotifyFromEmail, _options.NotifyFromName);

        return (_options.SendMailToken, _options.FromEmail, _options.FromName);
    }

    internal static string NormalizeSendMailToken(string? token)
    {
        const string prefix = "Zoho-enczapikey";
        var normalized = token?.Trim() ?? string.Empty;
        while (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            normalized = normalized[prefix.Length..].Trim();
        return normalized;
    }

    private string ResolveFromName(string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
            return name.Trim();
        return string.IsNullOrWhiteSpace(_options.FromName) ? "CSIR SPME System" : _options.FromName.Trim();
    }

    private static string ClassifyError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "provider_authentication_failed",
        HttpStatusCode.TooManyRequests => "provider_rate_limited",
        HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => "provider_rejected_message",
        _ when (int)statusCode >= 500 => "provider_unavailable",
        _ => "provider_rejected_message"
    };

    private static CommunicationTransportResult Rejected(string code, int? status, bool transient) =>
        new(false, "zeptomail", null, code, status, transient);

    private static string? TryReadString(string payload, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;
        try
        {
            using var document = JsonDocument.Parse(payload);
            return FindString(document.RootElement, propertyName);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FindString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    return property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : property.Value.GetRawText();
                var nested = FindString(property.Value, propertyName);
                if (nested is not null)
                    return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindString(item, propertyName);
                if (nested is not null)
                    return nested;
            }
        }
        return null;
    }

    private static IReadOnlyList<ZeptoAttachment>? MapAttachments(IReadOnlyList<EmailAttachment>? attachments) =>
        attachments is { Count: > 0 }
            ? attachments.Select(item => new ZeptoAttachment(item.FileName, item.ContentType, item.ContentBase64)).ToList()
            : null;

    private sealed record ZeptoMailRequest(
        [property: JsonPropertyName("from")] ZeptoAddress From,
        [property: JsonPropertyName("to")] IReadOnlyList<ZeptoRecipient> To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("htmlbody"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? HtmlBody,
        [property: JsonPropertyName("textbody"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TextBody,
        [property: JsonPropertyName("bounce_address"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? BounceAddress,
        [property: JsonPropertyName("track_opens")] bool TrackOpens,
        [property: JsonPropertyName("track_clicks")] bool TrackClicks,
        [property: JsonPropertyName("attachments"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ZeptoAttachment>? Attachments);

    private sealed record ZeptoRecipient([property: JsonPropertyName("email_address")] ZeptoAddress EmailAddress);

    private sealed record ZeptoAddress(
        [property: JsonPropertyName("address")] string Address,
        [property: JsonPropertyName("name")] string Name);

    private sealed record ZeptoAttachment(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("mime_type")] string MimeType,
        [property: JsonPropertyName("content")] string Content);
}
