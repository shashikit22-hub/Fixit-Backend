using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Services;

public class ConversationService
{
    private readonly FixitDbContext _db;
    private readonly WhatsAppService _whatsApp;
    private readonly ILogger<ConversationService> _logger;

    private static readonly Dictionary<string, string> ServiceTypes = new()
    {
        ["1"] = "Electrician",
        ["2"] = "Plumber",
        ["3"] = "Carpenter"
    };

    public ConversationService(FixitDbContext db, WhatsAppService whatsApp, ILogger<ConversationService> logger)
    {
        _db = db;
        _whatsApp = whatsApp;
        _logger = logger;
    }

    public async Task HandleIncomingMessageAsync(
        string phone,
        string name,
        string? body,
        int numMedia,
        string? mediaUrl,
        string? mediaType,
        double? lat,
        double? lon)
    {
        var state = await _db.ConversationStates
            .FirstOrDefaultAsync(c => c.PhoneNumber == phone);

        // Create new state if none exists
        if (state == null)
        {
            state = new ConversationState
            {
                PhoneNumber = phone,
                ProfileName = name,
                CurrentStep = ConversationStep.Greeting,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ConversationStates.Add(state);
            await _db.SaveChangesAsync();
        }

        // Auto-reset stale conversations (>24h)
        if ((DateTime.UtcNow - state.UpdatedAt).TotalHours > 24 &&
            state.CurrentStep != ConversationStep.AwaitingRating)
        {
            ResetState(state);
        }

        var text = body?.Trim() ?? "";
        var textLower = text.ToLowerInvariant();

        // Handle reset/cancel commands at any step
        if (textLower is "reset" or "cancel")
        {
            ResetState(state);
            state.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _whatsApp.SendMessageAsync(phone,
                "🔄 Conversation reset. Send any message to start a new service request.");
            return;
        }

        // Handle rating flow (takes priority if awaiting rating)
        if (state.CurrentStep == ConversationStep.AwaitingRating)
        {
            await HandleRatingAsync(state, text);
            return;
        }

        switch (state.CurrentStep)
        {
            case ConversationStep.Greeting:
                await HandleGreetingAsync(state);
                break;

            case ConversationStep.ServiceSelection:
                await HandleServiceSelectionAsync(state, text);
                break;

            case ConversationStep.Photo:
                await HandlePhotoAsync(state, numMedia, mediaUrl, mediaType);
                break;

            case ConversationStep.Video:
                await HandleVideoAsync(state, text, numMedia, mediaUrl, mediaType);
                break;

            case ConversationStep.Location:
                await HandleLocationAsync(state, text, lat, lon);
                break;

            case ConversationStep.CustomerDetails:
                await HandleCustomerDetailsAsync(state, text);
                break;

            case ConversationStep.Idle:
                // User sent a message after completing a request — start fresh
                ResetState(state);
                await HandleGreetingAsync(state);
                break;
        }
    }

    public async Task TriggerRatingFlowAsync(int requestId, string phone, string requestCode)
    {
        var state = await _db.ConversationStates
            .FirstOrDefaultAsync(c => c.PhoneNumber == phone);

        if (state == null)
        {
            state = new ConversationState
            {
                PhoneNumber = phone,
                CurrentStep = ConversationStep.AwaitingRating,
                AwaitingRatingForRequestId = requestId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ConversationStates.Add(state);
        }
        else
        {
            state.CurrentStep = ConversationStep.AwaitingRating;
            state.AwaitingRatingForRequestId = requestId;
            state.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        await _whatsApp.SendRatingRequest(requestId, phone, requestCode);
    }

    public async Task<string> GenerateRequestCodeAsync()
    {
        var datePrefix = $"FIX-{DateTime.UtcNow:yyyyMMdd}-";

        // Find the highest sequence number for today
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var lastRequest = await _db.ServiceRequests
            .Where(sr => sr.RequestCode != null && sr.CreatedAt >= today && sr.CreatedAt < tomorrow)
            .OrderByDescending(sr => sr.RequestCode)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (lastRequest?.RequestCode != null)
        {
            var parts = lastRequest.RequestCode.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var lastSeq))
            {
                sequence = lastSeq + 1;
            }
        }

        return $"{datePrefix}{sequence:D3}";
    }

    private async Task HandleGreetingAsync(ConversationState state)
    {
        state.CurrentStep = ConversationStep.ServiceSelection;
        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var message = $"👋 Welcome to FIXIT, {state.ProfileName ?? "Customer"}!\n\n" +
                      "We're here to help with your home repairs. Please select a service:\n\n" +
                      "1️⃣ Electrician\n" +
                      "2️⃣ Plumber\n" +
                      "3️⃣ Carpenter\n\n" +
                      "Reply with the number (1, 2, or 3).";

        await _whatsApp.SendMessageAsync(state.PhoneNumber, message);
    }

    private async Task HandleServiceSelectionAsync(ConversationState state, string text)
    {
        if (!ServiceTypes.TryGetValue(text.Trim(), out var serviceType))
        {
            await _whatsApp.SendMessageAsync(state.PhoneNumber,
                "❌ Please reply with a valid option:\n1 - Electrician\n2 - Plumber\n3 - Carpenter");
            return;
        }

        state.SelectedServiceType = serviceType;
        state.CurrentStep = ConversationStep.Photo;
        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _whatsApp.SendMessageAsync(state.PhoneNumber,
            $"✅ You selected: {serviceType}\n\n" +
            "📸 Please send a photo of the issue so our technician can better understand the problem.");
    }

    private async Task HandlePhotoAsync(ConversationState state, int numMedia, string? mediaUrl, string? mediaType)
    {
        if (numMedia < 1 || string.IsNullOrEmpty(mediaUrl) ||
            (mediaType != null && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
        {
            await _whatsApp.SendMessageAsync(state.PhoneNumber,
                "📸 Please send a photo of the issue to proceed.");
            return;
        }

        state.PhotoUrl = mediaUrl;
        state.CurrentStep = ConversationStep.Video;
        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _whatsApp.SendMessageAsync(state.PhoneNumber,
            "✅ Photo received!\n\n" +
            "🎥 Now, please send a short video of the issue for better understanding.\n" +
            "Or type *skip* if you don't have a video.");
    }

    private async Task HandleVideoAsync(ConversationState state, string text, int numMedia, string? mediaUrl, string? mediaType)
    {
        if (text.Trim().Equals("skip", StringComparison.OrdinalIgnoreCase))
        {
            // Skip video
            state.CurrentStep = ConversationStep.Location;
            state.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _whatsApp.SendMessageAsync(state.PhoneNumber,
                "👍 Video skipped.\n\n" +
                "📍 Please share your location using WhatsApp's location feature, or type your full address.");
            return;
        }

        if (numMedia < 1 || string.IsNullOrEmpty(mediaUrl) ||
            (mediaType != null && !mediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)))
        {
            await _whatsApp.SendMessageAsync(state.PhoneNumber,
                "🎥 Please send a video, or type *skip* to continue without one.");
            return;
        }

        state.VideoUrl = mediaUrl;
        state.CurrentStep = ConversationStep.Location;
        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _whatsApp.SendMessageAsync(state.PhoneNumber,
            "✅ Video received!\n\n" +
            "📍 Please share your location using WhatsApp's location feature, or type your full address.");
    }

    private async Task HandleLocationAsync(ConversationState state, string text, double? lat, double? lon)
    {
        if (lat.HasValue && lon.HasValue)
        {
            state.Latitude = lat.Value;
            state.Longitude = lon.Value;
        }
        else if (!string.IsNullOrWhiteSpace(text))
        {
            state.AddressText = text;
        }
        else
        {
            await _whatsApp.SendMessageAsync(state.PhoneNumber,
                "📍 Please share your location or type your address to continue.");
            return;
        }

        state.CurrentStep = ConversationStep.CustomerDetails;
        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _whatsApp.SendMessageAsync(state.PhoneNumber,
            "✅ Location received!\n\n" +
            "📝 Please provide your details in the following format:\n" +
            "*Name, House Number, Alternate Phone*\n\n" +
            "Example: Ravi Kumar, #42 2nd Cross, 9876543210");
    }

    private async Task HandleCustomerDetailsAsync(ConversationState state, string text)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries);

        if (parts.Length < 2)
        {
            await _whatsApp.SendMessageAsync(state.PhoneNumber,
                "❌ Please provide details in the format:\n*Name, House Number, Alternate Phone*\n\n" +
                "Example: Ravi Kumar, #42 2nd Cross, 9876543210");
            return;
        }

        var customerName = parts[0];
        var houseNumber = parts[1];
        var altPhone = parts.Length >= 3 ? parts[2] : null;

        // Generate request code
        var requestCode = await GenerateRequestCodeAsync();

        // Create the service request
        var serviceRequest = new ServiceRequest
        {
            CustomerName = customerName,
            CustomerPhone = state.PhoneNumber,
            ServiceType = state.SelectedServiceType ?? "General",
            Description = $"Service request via WhatsApp bot",
            RequestCode = requestCode,
            PhotoUrl = state.PhotoUrl,
            VideoUrl = state.VideoUrl,
            Latitude = state.Latitude,
            Longitude = state.Longitude,
            Address = state.AddressText,
            HouseNumber = houseNumber,
            AlternatePhone = altPhone,
            Status = "New",
            CreatedAt = DateTime.UtcNow
        };

        _db.ServiceRequests.Add(serviceRequest);
        await _db.SaveChangesAsync();

        // Set conversation to idle
        state.CurrentStep = ConversationStep.Idle;
        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Build summary message
        var locationInfo = state.Latitude.HasValue
            ? $"📍 GPS: {state.Latitude:F4}, {state.Longitude:F4}"
            : $"📍 Address: {state.AddressText}";

        var summary = $"✅ *Service Request Created!*\n\n" +
                      $"🔖 Request ID: *{requestCode}*\n" +
                      $"🔧 Service: {serviceRequest.ServiceType}\n" +
                      $"👤 Name: {customerName}\n" +
                      $"🏠 House: {houseNumber}\n" +
                      (altPhone != null ? $"📞 Alt Phone: {altPhone}\n" : "") +
                      $"{locationInfo}\n" +
                      $"📸 Photo: ✅\n" +
                      (serviceRequest.VideoUrl != null ? $"🎥 Video: ✅\n" : $"🎥 Video: Skipped\n") +
                      $"\nWe'll assign a technician shortly. Thank you for choosing FIXIT! 🙏";

        await _whatsApp.SendMessageAsync(state.PhoneNumber, summary);

        _logger.LogInformation("Service request {Code} created via WhatsApp bot for {Phone}",
            requestCode, state.PhoneNumber);
    }

    private async Task HandleRatingAsync(ConversationState state, string text)
    {
        if (!int.TryParse(text.Trim(), out var rating) || rating < 1 || rating > 5)
        {
            await _whatsApp.SendMessageAsync(state.PhoneNumber,
                "Please reply with a number from 1 to 5 to rate our service.");
            return;
        }

        if (state.AwaitingRatingForRequestId.HasValue)
        {
            var request = await _db.ServiceRequests.FindAsync(state.AwaitingRatingForRequestId.Value);
            if (request != null)
            {
                request.Rating = rating;
                request.RatedAt = DateTime.UtcNow;
                request.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Reset to idle
        state.CurrentStep = ConversationStep.Idle;
        state.AwaitingRatingForRequestId = null;
        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var stars = new string('⭐', rating);
        await _whatsApp.SendMessageAsync(state.PhoneNumber,
            $"🙏 Thank you for your rating! {stars}\n\n" +
            "We appreciate your feedback. See you next time!");

        _logger.LogInformation("Rating {Rating} received from {Phone}", rating, state.PhoneNumber);
    }

    private static void ResetState(ConversationState state)
    {
        state.CurrentStep = ConversationStep.Greeting;
        state.SelectedServiceType = null;
        state.PhotoUrl = null;
        state.VideoUrl = null;
        state.Latitude = null;
        state.Longitude = null;
        state.AddressText = null;
        state.AwaitingRatingForRequestId = null;
    }
}
