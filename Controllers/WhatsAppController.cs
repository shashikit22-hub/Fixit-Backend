using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WhatsAppController : ControllerBase
{
    private readonly ConversationService _conversation;
    private readonly WhatsAppService _whatsApp;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppController> _logger;

    public WhatsAppController(
        ConversationService conversation,
        WhatsAppService whatsApp,
        IConfiguration config,
        ILogger<WhatsAppController> logger)
    {
        _conversation = conversation;
        _whatsApp = whatsApp;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Meta webhook verification endpoint.
    /// Meta sends a GET with hub.mode, hub.verify_token, and hub.challenge.
    /// </summary>
    [HttpGet("webhook")]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var verifyToken = _config["WhatsApp:VerifyToken"] ?? "";

        if (mode == "subscribe" && token == verifyToken)
        {
            _logger.LogInformation("Webhook verified successfully");
            return Ok(challenge);
        }

        _logger.LogWarning("Webhook verification failed: mode={Mode}, token={Token}", mode, token);
        return Forbid();
    }

    /// <summary>
    /// Webhook endpoint for incoming WhatsApp messages from Meta Cloud API.
    /// Meta sends JSON with nested entry[].changes[].value.messages[] structure.
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Receive()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        _logger.LogInformation("Meta webhook payload: {Body}", body);

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("entry", out var entries))
                return Ok();

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes))
                    continue;

                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value))
                        continue;

                    if (!value.TryGetProperty("messages", out var messages))
                        continue;

                    // Get contact info
                    string? profileName = null;
                    if (value.TryGetProperty("contacts", out var contacts) &&
                        contacts.GetArrayLength() > 0)
                    {
                        var contact = contacts[0];
                        if (contact.TryGetProperty("profile", out var profile) &&
                            profile.TryGetProperty("name", out var nameProp))
                        {
                            profileName = nameProp.GetString();
                        }
                    }

                    foreach (var message in messages.EnumerateArray())
                    {
                        await ProcessMessageAsync(message, profileName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Meta webhook payload");
        }

        return Ok();
    }

    private async Task ProcessMessageAsync(JsonElement message, string? profileName)
    {
        var phone = message.TryGetProperty("from", out var fromProp) ? fromProp.GetString() : null;
        if (string.IsNullOrEmpty(phone)) return;

        var name = profileName ?? "Customer";
        var type = message.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "text";

        string? textBody = null;
        int numMedia = 0;
        string? mediaUrl = null;
        string? mediaType = null;
        double? latitude = null;
        double? longitude = null;

        switch (type)
        {
            case "text":
                if (message.TryGetProperty("text", out var textObj) &&
                    textObj.TryGetProperty("body", out var bodyProp))
                {
                    textBody = bodyProp.GetString();
                }
                break;

            case "image":
                if (message.TryGetProperty("image", out var imageObj))
                {
                    numMedia = 1;
                    mediaType = imageObj.TryGetProperty("mime_type", out var imgMime)
                        ? imgMime.GetString() : "image/jpeg";
                    var imageId = imageObj.TryGetProperty("id", out var imgId) ? imgId.GetString() : null;
                    if (imageId != null)
                        mediaUrl = await _whatsApp.GetMediaUrlAsync(imageId);
                    if (imageObj.TryGetProperty("caption", out var captionProp))
                        textBody = captionProp.GetString();
                }
                break;

            case "video":
                if (message.TryGetProperty("video", out var videoObj))
                {
                    numMedia = 1;
                    mediaType = videoObj.TryGetProperty("mime_type", out var vidMime)
                        ? vidMime.GetString() : "video/mp4";
                    var videoId = videoObj.TryGetProperty("id", out var vidId) ? vidId.GetString() : null;
                    if (videoId != null)
                        mediaUrl = await _whatsApp.GetMediaUrlAsync(videoId);
                    if (videoObj.TryGetProperty("caption", out var vidCaption))
                        textBody = vidCaption.GetString();
                }
                break;

            case "location":
                if (message.TryGetProperty("location", out var locObj))
                {
                    latitude = locObj.TryGetProperty("latitude", out var latProp) ? latProp.GetDouble() : null;
                    longitude = locObj.TryGetProperty("longitude", out var lonProp) ? lonProp.GetDouble() : null;
                }
                break;
        }

        _logger.LogInformation(
            "Incoming WhatsApp from {Phone} ({Name}): Type={Type}, Body={Body}, NumMedia={NumMedia}, Lat={Lat}, Lon={Lon}",
            phone, name, type, textBody, numMedia, latitude, longitude);

        await _conversation.HandleIncomingMessageAsync(
            phone, name, textBody, numMedia, mediaUrl, mediaType, latitude, longitude);
    }
}
