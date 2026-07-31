using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Services;

public class WhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _phoneNumberId;
    private readonly string _accessToken;
    private readonly bool _isConfigured;

    private const string GraphApiBase = "https://graph.facebook.com/v21.0";
    private const string DefaultCountryCode = "91";

    public WhatsAppService(HttpClient httpClient, IConfiguration config, ILogger<WhatsAppService> logger, IServiceScopeFactory scopeFactory)
    {
        _httpClient = httpClient;
        _logger = logger;
        _scopeFactory = scopeFactory;

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
        toPhone = NormalizePhone(toPhone);
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
        toPhone = NormalizePhone(toPhone);
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
        toPhone = NormalizePhone(toPhone);
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

    /// <summary>
    /// Ensures phone number has country code prefix (defaults to 91 for India).
    /// </summary>
    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 10)
            digits = DefaultCountryCode + digits;
        return digits;
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

    /// <summary>
    /// Downloads media from WhatsApp CDN and stores it in the database.
    /// Returns the API URL (e.g., /api/media/{guid}).
    /// </summary>
    public async Task<string?> DownloadAndStoreMediaAsync(string mediaId, string extension)
    {
        if (!_isConfigured) return null;

        try
        {
            // Step 1: Get the CDN URL from media ID
            var metaResponse = await _httpClient.GetAsync($"{GraphApiBase}/{mediaId}");
            if (!metaResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get media metadata for {MediaId}: {Status}", mediaId, metaResponse.StatusCode);
                return null;
            }

            var metaJson = await metaResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(metaJson);
            if (!doc.RootElement.TryGetProperty("url", out var urlProp))
                return null;

            var cdnUrl = urlProp.GetString();
            if (string.IsNullOrEmpty(cdnUrl)) return null;

            // Step 2: Download the actual media file (requires Bearer token)
            var mediaResponse = await _httpClient.GetAsync(cdnUrl);
            if (!mediaResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to download media from CDN: {Status}", mediaResponse.StatusCode);
                return null;
            }

            var bytes = await mediaResponse.Content.ReadAsByteArrayAsync();
            var contentType = mediaResponse.Content.Headers.ContentType?.MediaType
                ?? (extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    ".mp4" => "video/mp4",
                    ".3gp" => "video/3gpp",
                    ".mov" => "video/quicktime",
                    _ => "application/octet-stream"
                });

            // Step 3: Save to database
            var media = new Models.MediaFile
            {
                Id = Guid.NewGuid(),
                FileName = $"{mediaId}{extension}",
                ContentType = contentType,
                Data = bytes,
                CreatedAt = DateTime.UtcNow
            };

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Data.TinyfixDbContext>();
            db.MediaFiles.Add(media);
            await db.SaveChangesAsync();

            _logger.LogInformation("Media saved to DB: {Id} ({Size} bytes) from mediaId {MediaId}", media.Id, bytes.Length, mediaId);

            return $"/api/media/{media.Id}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download and store media {MediaId}", mediaId);
            return null;
        }
    }

    public async Task SendRequestConfirmation(int requestId, string phone)
    {
        var text = $"✅ *Request Received!*\n\n" +
                   $"Your service request *#{requestId}* has been logged successfully.\n\n" +
                   $"Our team is reviewing your request and will assign a technician shortly.\n\n" +
                   $"Thank you for choosing *TinyFix*! 🙏";
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
                   $"Thank you for choosing *TinyFix*! 🙏";
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

    public async Task SendJobAssignmentToTechnician(
        int assignmentId, string techPhone, string techName,
        string requestCode, string customerName, string customerPhone,
        string serviceType, string? description, string? address, string? houseNumber,
        string? photoUrl = null, string? videoUrl = null,
        double? latitude = null, double? longitude = null,
        string? baseUrl = null)
    {
        var location = address ?? houseNumber ?? "Not provided";
        if (address != null && houseNumber != null)
            location = $"{houseNumber}, {address}";

        var mapLink = latitude.HasValue && longitude.HasValue
            ? $"https://maps.google.com/?q={latitude.Value},{longitude.Value}"
            : null;

        var desc = description ?? "No description";
        if (desc.Length > 200)
            desc = desc[..200] + "...";

        var bodyText = $"Hi {techName}! You have a new job assignment.\n\n" +
                       $"🔖 *Job ID:* {requestCode}\n" +
                       $"🔧 *Service:* {serviceType}\n" +
                       $"👤 *Customer:* {customerName}\n" +
                       $"📞 *Phone:* {customerPhone}\n" +
                       $"📍 *Location:* {location}\n";

        if (mapLink != null)
            bodyText += $"🗺️ *Map:* {mapLink}\n";

        bodyText += $"📝 *Description:* {desc}\n\n" +
                    $"Please accept or reject this job.";

        var buttons = new (string Id, string Title)[]
        {
            ($"accept_job_{assignmentId}", "Accept"),
            ($"reject_job_{assignmentId}", "Reject")
        };

        await SendInteractiveButtonsAsync(techPhone, "New Job Assignment", bodyText, buttons);

        // Build full URL for local paths (e.g., /uploads/abc.jpg → https://host/uploads/abc.jpg)
        var fullPhotoUrl = photoUrl;
        var fullVideoUrl = videoUrl;
        if (!string.IsNullOrEmpty(baseUrl))
        {
            if (fullPhotoUrl?.StartsWith("/") == true)
                fullPhotoUrl = baseUrl.TrimEnd('/') + fullPhotoUrl;
            if (fullVideoUrl?.StartsWith("/") == true)
                fullVideoUrl = baseUrl.TrimEnd('/') + fullVideoUrl;
        }

        // Send photo as follow-up if available
        if (!string.IsNullOrEmpty(fullPhotoUrl))
            await SendImageAsync(techPhone, fullPhotoUrl, $"📸 Issue photo for {requestCode}");

        // Send video as follow-up if available
        if (!string.IsNullOrEmpty(fullVideoUrl))
            await SendVideoAsync(techPhone, fullVideoUrl, $"🎥 Issue video for {requestCode}");

        // Send location as follow-up if available
        if (latitude.HasValue && longitude.HasValue)
            await SendLocationAsync(techPhone, latitude.Value, longitude.Value, customerName, location);
    }

    public async Task SendImageAsync(string toPhone, string imageUrl, string? caption = null)
    {
        toPhone = NormalizePhone(toPhone);
        if (!_isConfigured) return;

        try
        {
            var image = new Dictionary<string, string> { ["link"] = imageUrl };
            if (caption != null) image["caption"] = caption;

            var payload = new
            {
                messaging_product = "whatsapp",
                to = toPhone,
                type = "image",
                image
            };
            await PostPayloadAsync(toPhone, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send image to {Phone}", toPhone);
        }
    }

    public async Task SendVideoAsync(string toPhone, string videoUrl, string? caption = null)
    {
        toPhone = NormalizePhone(toPhone);
        if (!_isConfigured) return;

        try
        {
            var video = new Dictionary<string, string> { ["link"] = videoUrl };
            if (caption != null) video["caption"] = caption;

            var payload = new
            {
                messaging_product = "whatsapp",
                to = toPhone,
                type = "video",
                video
            };
            await PostPayloadAsync(toPhone, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send video to {Phone}", toPhone);
        }
    }

    public async Task SendLocationAsync(string toPhone, double latitude, double longitude, string name, string address)
    {
        toPhone = NormalizePhone(toPhone);
        if (!_isConfigured) return;

        try
        {
            var payload = new
            {
                messaging_product = "whatsapp",
                to = toPhone,
                type = "location",
                location = new
                {
                    latitude,
                    longitude,
                    name,
                    address
                }
            };
            await PostPayloadAsync(toPhone, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send location to {Phone}", toPhone);
        }
    }

    public async Task SendJobAcceptedConfirmationToTechnician(
        int assignmentId, string techPhone, string requestCode, string customerName, string customerPhone)
    {
        var bodyText = $"✅ *Job Accepted!*\n\n" +
                       $"You have accepted job *{requestCode}*.\n\n" +
                       $"👤 *Customer:* {customerName}\n" +
                       $"📞 *Phone:* {customerPhone}\n\n" +
                       $"Please tap *Start Job* when you begin work.";

        var buttons = new (string Id, string Title)[]
        {
            ($"start_job_{assignmentId}", "▶️ Start Job")
        };

        await SendInteractiveButtonsAsync(techPhone, "Job Accepted", bodyText, buttons);
    }

    public async Task SendJobRejectedConfirmationToTechnician(string techPhone, string requestCode)
    {
        var text = $"❌ *Job Rejected*\n\n" +
                   $"You have rejected job *{requestCode}*.\n\n" +
                   $"No worries! We'll assign another technician.";
        await SendMessageAsync(techPhone, text);
    }

    public async Task SendTechnicianAcceptedToCustomer(
        string customerPhone, string requestCode, string techName, string techPhone)
    {
        var text = $"🎉 *Technician Confirmed!*\n\n" +
                   $"Great news! Your technician for request *{requestCode}* has accepted the job.\n\n" +
                   $"👤 *Technician:* {techName}\n" +
                   $"📞 *Contact:* {techPhone}\n\n" +
                   $"They will contact you shortly to schedule a visit.";
        await SendMessageAsync(customerPhone, text);
    }

    public async Task SendJobStartedConfirmationToTechnician(
        int assignmentId, string techPhone, string requestCode)
    {
        var bodyText = $"🔧 *Job Started!*\n\n" +
                       $"You've started working on job *{requestCode}*.\n\n" +
                       $"Tap *Complete Job* when you're done.";

        var buttons = new (string Id, string Title)[]
        {
            ($"complete_job_{assignmentId}", "✅ Complete Job")
        };

        await SendInteractiveButtonsAsync(techPhone, "Job Started", bodyText, buttons);
    }

    public async Task SendJobStartedToCustomer(
        string customerPhone, string requestCode, string techName)
    {
        var text = $"🔧 *Work Started!*\n\n" +
                   $"Your technician *{techName}* has started working on request *{requestCode}*.\n\n" +
                   $"We'll notify you once the work is completed.";
        await SendMessageAsync(customerPhone, text);
    }

    public async Task SendJobCompletedConfirmationToTechnician(
        string techPhone, string requestCode)
    {
        var text = $"✅ *Job Completed!*\n\n" +
                   $"You've completed job *{requestCode}*.\n\n" +
                   $"Thank you for your service!";
        await SendMessageAsync(techPhone, text);
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
                          $"Thank you for choosing *TinyFix*! 🙏",
            "Cancelled" => $"❌ *Request Cancelled*\n\n" +
                          $"Your service request *#{requestId}* has been cancelled.\n\n" +
                          $"If this was a mistake, send *hi* to start a new request.",
            _ => null
        };

        if (text != null)
            await SendMessageAsync(phone, text);
    }
}
