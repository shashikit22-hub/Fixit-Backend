using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace backend.Services;

public class WhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppService> _logger;
    private readonly string _phoneNumberId;
    private readonly string _accessToken;
    private readonly bool _isConfigured;

    private const string GraphApiBase = "https://graph.facebook.com/v21.0";

    public WhatsAppService(HttpClient httpClient, IConfiguration config, ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _phoneNumberId = config["WhatsApp:PhoneNumberId"] ?? "";
        _accessToken = config["WhatsApp:AccessToken"] ?? "";

        _isConfigured = !string.IsNullOrEmpty(_phoneNumberId) &&
                        !string.IsNullOrEmpty(_accessToken);

        if (_isConfigured)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);
        }
    }

    public async Task SendMessageAsync(string toPhone, string text)
    {
        if (!_isConfigured)
        {
            _logger.LogInformation("WhatsApp Cloud API not configured — skipping message to {Phone}", toPhone);
            return;
        }

        try
        {
            var payload = new
            {
                messaging_product = "whatsapp",
                to = toPhone,
                type = "text",
                text = new { body = text }
            };

            await PostPayloadAsync(toPhone, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send WhatsApp message to {Phone}", toPhone);
        }
    }

    /// <summary>
    /// Sends an interactive button message (max 3 buttons).
    /// Each button is a (id, title) tuple.
    /// </summary>
    public async Task SendInteractiveButtonsAsync(string toPhone, string? headerText, string bodyText, (string Id, string Title)[] buttons)
    {
        if (!_isConfigured)
        {
            _logger.LogInformation("WhatsApp Cloud API not configured — skipping interactive buttons to {Phone}", toPhone);
            return;
        }

        try
        {
            var buttonList = buttons.Select(b => new
            {
                type = "reply",
                reply = new { id = b.Id, title = b.Title }
            }).ToArray();

            object? header = headerText != null
                ? new { type = "text", text = headerText }
                : null;

            var interactive = new Dictionary<string, object>
            {
                ["type"] = "button",
                ["body"] = new { text = bodyText },
                ["action"] = new { buttons = buttonList }
            };

            if (header != null)
                interactive["header"] = header;

            var payload = new
            {
                messaging_product = "whatsapp",
                to = toPhone,
                type = "interactive",
                interactive
            };

            await PostPayloadAsync(toPhone, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send interactive buttons to {Phone}", toPhone);
        }
    }

    /// <summary>
    /// Sends an interactive list message (up to 10 options).
    /// Each section contains rows of (id, title, description) tuples.
    /// </summary>
    public async Task SendInteractiveListAsync(string toPhone, string? headerText, string bodyText, string buttonLabel, (string Id, string Title, string? Description)[] rows)
    {
        if (!_isConfigured)
        {
            _logger.LogInformation("WhatsApp Cloud API not configured — skipping interactive list to {Phone}", toPhone);
            return;
        }

        try
        {
            var rowList = rows.Select(r =>
            {
                var row = new Dictionary<string, string> { ["id"] = r.Id, ["title"] = r.Title };
                if (r.Description != null)
                    row["description"] = r.Description;
                return row;
            }).ToArray();

            object? header = headerText != null
                ? new { type = "text", text = headerText }
                : null;

            var interactive = new Dictionary<string, object>
            {
                ["type"] = "list",
                ["body"] = new { text = bodyText },
                ["action"] = new
                {
                    button = buttonLabel,
                    sections = new[]
                    {
                        new { title = "Options", rows = rowList }
                    }
                }
            };

            if (header != null)
                interactive["header"] = header;

            var payload = new
            {
                messaging_product = "whatsapp",
                to = toPhone,
                type = "interactive",
                interactive
            };

            await PostPayloadAsync(toPhone, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send interactive list to {Phone}", toPhone);
        }
    }

    private async Task PostPayloadAsync(string toPhone, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"{GraphApiBase}/{_phoneNumberId}/messages", content);

        var responseBody = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("WhatsApp message sent to {Phone}: {Response}", toPhone, responseBody);
        }
        else
        {
            _logger.LogWarning("WhatsApp API error for {Phone}: {Status} {Response}",
                toPhone, response.StatusCode, responseBody);
        }
    }

    public async Task<string?> GetMediaUrlAsync(string mediaId)
    {
        if (!_isConfigured) return null;

        try
        {
            var response = await _httpClient.GetAsync($"{GraphApiBase}/{mediaId}");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get media URL for {MediaId}", mediaId);
            return null;
        }
    }

    public async Task SendRequestConfirmation(int requestId, string phone)
    {
        var text = $"✅ *Request Received!*\n\n" +
                   $"Your service request *#{requestId}* has been logged successfully.\n\n" +
                   $"Our team is reviewing your request and will assign a technician shortly.\n\n" +
                   $"Thank you for choosing *FIXIT*! 🙏";
        await SendMessageAsync(phone, text);
    }

    public async Task SendTechnicianAssigned(int requestId, string phone, string techName, string techPhone)
    {
        var text = $"🔧 *Technician Assigned!*\n\n" +
                   $"Great news! A technician has been assigned to your request *#{requestId}*.\n\n" +
                   $"👤 *Technician:* {techName}\n" +
                   $"📞 *Contact:* {techPhone}\n\n" +
                   $"They will reach out to you shortly to schedule a visit.";
        await SendMessageAsync(phone, text);
    }

    public async Task SendRequestCompleted(int requestId, string phone)
    {
        var text = $"🎉 *Service Completed!*\n\n" +
                   $"Your service request *#{requestId}* has been marked as completed.\n\n" +
                   $"We hope everything is in great shape! You'll receive a rating request shortly.\n\n" +
                   $"Thank you for choosing *FIXIT*! 🙏";
        await SendMessageAsync(phone, text);
    }

    public async Task SendRatingRequest(int requestId, string phone, string requestCode)
    {
        var rows = new (string Id, string Title, string? Description)[]
        {
            ("1", "⭐ Poor", "1 star — Not satisfied"),
            ("2", "⭐⭐ Fair", "2 stars — Below expectations"),
            ("3", "⭐⭐⭐ Good", "3 stars — Met expectations"),
            ("4", "⭐⭐⭐⭐ Very Good", "4 stars — Above expectations"),
            ("5", "⭐⭐⭐⭐⭐ Excellent", "5 stars — Outstanding service")
        };

        await SendInteractiveListAsync(
            phone,
            "Rate Our Service",
            $"Your service request *{requestCode}* has been completed! 🎉\n\nWe'd love to hear your feedback. How would you rate our service?",
            "Select Rating",
            rows);
    }

    public async Task SendStatusUpdate(int requestId, string phone, string newStatus)
    {
        var text = newStatus switch
        {
            "InProgress" => $"🔄 *Work In Progress*\n\n" +
                           $"Your service request *#{requestId}* is now being worked on.\n" +
                           $"Our technician is on it! We'll notify you once it's completed.",
            "Completed" => $"🎉 *Service Completed!*\n\n" +
                          $"Your service request *#{requestId}* has been completed successfully.\n\n" +
                          $"Thank you for choosing *FIXIT*! 🙏",
            "Cancelled" => $"❌ *Request Cancelled*\n\n" +
                          $"Your service request *#{requestId}* has been cancelled.\n\n" +
                          $"If this was a mistake, send *hi* to start a new request.",
            _ => null
        };

        if (text != null)
            await SendMessageAsync(phone, text);
    }
}
