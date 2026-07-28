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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send WhatsApp message to {Phone}", toPhone);
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
        var text = $"✅ FIXIT: Your service request #{requestId} has been received! " +
                   $"We'll assign a technician shortly. Thank you for choosing FIXIT!";
        await SendMessageAsync(phone, text);
    }

    public async Task SendTechnicianAssigned(int requestId, string phone, string techName, string techPhone)
    {
        var text = $"🔧 FIXIT: A technician has been assigned to your request #{requestId}.\n\n" +
                   $"Technician: {techName}\n" +
                   $"Contact: {techPhone}\n\n" +
                   $"They will reach out to you soon!";
        await SendMessageAsync(phone, text);
    }

    public async Task SendRequestCompleted(int requestId, string phone)
    {
        var text = $"🎉 FIXIT: Your service request #{requestId} has been completed! " +
                   $"We hope you're satisfied with our service. Thank you for choosing FIXIT!";
        await SendMessageAsync(phone, text);
    }

    public async Task SendRatingRequest(int requestId, string phone, string requestCode)
    {
        var text = $"⭐ FIXIT: Your service request {requestCode} has been completed!\n\n" +
                   $"How would you rate our service? Please reply with a number from 1 to 5:\n" +
                   $"1 ⭐ - Poor\n" +
                   $"2 ⭐⭐ - Fair\n" +
                   $"3 ⭐⭐⭐ - Good\n" +
                   $"4 ⭐⭐⭐⭐ - Very Good\n" +
                   $"5 ⭐⭐⭐⭐⭐ - Excellent";
        await SendMessageAsync(phone, text);
    }

    public async Task SendStatusUpdate(int requestId, string phone, string newStatus)
    {
        var text = newStatus switch
        {
            "InProgress" => $"🔄 FIXIT: Your service request #{requestId} is now in progress. Our technician is working on it!",
            "Completed" => $"🎉 FIXIT: Your service request #{requestId} has been completed! Thank you for choosing FIXIT!",
            "Cancelled" => $"❌ FIXIT: Your service request #{requestId} has been cancelled. If this was a mistake, please contact us.",
            _ => null
        };

        if (text != null)
            await SendMessageAsync(phone, text);
    }
}
