using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Csir.Spme.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Infrastructure.Communications;

public sealed class MNotifyTransport : ISmsTransport
{
    private readonly HttpClient _httpClient;
    private readonly MNotifyOptions _options;

    public MNotifyTransport(HttpClient httpClient, IOptions<MNotifyOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<CommunicationTransportResult> SendAsync(
        string to,
        string body,
        CancellationToken ct = default)
    {
        var phone = LoginIdentifierNormalizer.NormalizeGhanaPhone(to);
        if (phone is null)
            return Rejected("invalid_phone_number", null, false);
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
            return Rejected("provider_disabled", null, false);

        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/{_options.SmsEndpoint.TrimStart('/')}?key={Uri.EscapeDataString(_options.ApiKey.Trim())}";
        for (var attempt = 0; ; attempt++)
        {
            using var response = await _httpClient.PostAsJsonAsync(endpoint, new MNotifyRequest(
                [phone], _options.SenderId.Trim(), body, false), ct);
            var payload = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode && IsAccepted(payload))
                return new CommunicationTransportResult(
                    true,
                    "mnotify",
                    TryReadReference(payload),
                    null,
                    (int)response.StatusCode,
                    false);

            var transient = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
                (int)response.StatusCode >= 500;
            if (!transient || attempt >= _options.RetryCount)
                return Rejected(
                    response.IsSuccessStatusCode ? ClassifyProviderPayload(payload) : ClassifyError(response.StatusCode),
                    (int)response.StatusCode,
                    transient);

            await Task.Delay(TimeSpan.FromMilliseconds(_options.RetryDelayMilliseconds * (attempt + 1)), ct);
        }
    }

    private static string ClassifyError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "provider_authentication_failed",
        HttpStatusCode.TooManyRequests => "provider_rate_limited",
        HttpStatusCode.PaymentRequired => "provider_credit_exhausted",
        HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => "provider_rejected_message",
        _ when (int)statusCode >= 500 => "provider_unavailable",
        _ => "provider_rejected_message"
    };

    private static CommunicationTransportResult Rejected(string code, int? status, bool transient) =>
        new(false, "mnotify", null, code, status, transient);

    private static string? TryReadReference(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;
        try
        {
            using var document = JsonDocument.Parse(payload);
            foreach (var name in new[] { "campaign_id", "campaignId", "message_id", "id" })
            {
                if (document.RootElement.TryGetProperty(name, out var value))
                    return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            }
        }
        catch (JsonException)
        {
        }
        return null;
    }

    private static bool IsAccepted(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return true;
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("code", out var code))
                return true;
            var value = code.ValueKind == JsonValueKind.String ? code.GetString() : code.GetRawText();
            return string.Equals(value, "1000", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "success", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ClassifyProviderPayload(string payload)
    {
        if (payload.Contains("credit", StringComparison.OrdinalIgnoreCase) ||
            payload.Contains("balance", StringComparison.OrdinalIgnoreCase))
            return "provider_credit_exhausted";
        if (payload.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
            payload.Contains("api key", StringComparison.OrdinalIgnoreCase))
            return "provider_authentication_failed";
        return "provider_rejected_message";
    }

    private sealed record MNotifyRequest(
        [property: JsonPropertyName("recipient")] IReadOnlyList<string> Recipients,
        [property: JsonPropertyName("sender")] string Sender,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("is_schedule")] bool IsScheduled);
}
