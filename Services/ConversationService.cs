using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Services;

public class ConversationService
{
    private readonly TinyfixDbContext _db;
    private readonly WhatsAppService _whatsApp;
    private readonly ILogger<ConversationService> _logger;

    private static readonly Dictionary<string, string> ServiceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1"] = "Electrician",
        ["2"] = "Plumber",
        ["3"] = "Carpenter",
        ["electrician"] = "Electrician",
        ["plumber"] = "Plumber",
        ["carpenter"] = "Carpenter"
    };

    public ConversationService(TinyfixDbContext db, WhatsAppService whatsApp, ILogger<ConversationService> logger)
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
        // Intercept messages from technicians with pending assignments
        var techJobHandled = await TryHandleTechnicianJobResponseAsync(phone, body?.Trim() ?? "");
        if (techJobHandled) return;

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

        // Global commands — available at any step
        if (textLower is "help" or "?")
        {
            await _whatsApp.SendMessageAsync(phone,
                "*TinyFix Help* ℹ️\n\n" +
                "Here's what you can do:\n\n" +
                "📋 *Commands:*\n" +
                "• *menu* or *start* — Start a new request\n" +
                "• *reset* or *cancel* — Cancel current request\n" +
                "• *help* or *?* — Show this help message\n\n" +
                "📝 *How it works:*\n" +
                "1. Choose a service (Electrician, Plumber, or Carpenter)\n" +
                "2. Send a photo of the issue\n" +
                "3. Optionally send a video\n" +
                "4. Share your location\n" +
                "5. Provide your details\n\n" +
                "A technician will be assigned to you shortly after! 🔧");
            return;
        }

        if (textLower is "menu" or "start")
        {
            ResetState(state);
            state.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await HandleGreetingAsync(state);
            return;
        }

        // Handle reset/cancel commands at any step
        if (textLower is "reset" or "cancel")
        {
            ResetState(state);
            state.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _whatsApp.SendMessageAsync(phone,
                "🔄 *Request Cancelled*\n\n" +
                "Your current request has been cleared.\n" +
                "Send *menu* or any message to start a new service request.");
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
        var datePrefix = $"TNF-{DateTime.UtcNow:yyyyMMdd}-";

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

        var buttons = new (string Id, string Title)[]
        {
            ("electrician", "⚡ Electrician"),
            ("plumber", "🔧 Plumber"),
            ("carpenter", "🪚 Carpenter")
        };

        await _whatsApp.SendInteractiveButtonsAsync(
            state.PhoneNumber,
            "Welcome to TinyFix!",
            $"Hi {state.ProfileName ?? "there"}! 👋\n\nWe're here to help with your home repairs.\n\nWhat service do you need?",
            buttons);
    }

    private async Task HandleServiceSelectionAsync(ConversationState state, string text)
    {
        if (!ServiceTypes.TryGetValue(text.Trim(), out var serviceType))
        {
            // Resend buttons with friendly message
            var buttons = new (string Id, string Title)[]
            {
                ("electrician", "⚡ Electrician"),
                ("plumber", "🔧 Plumber"),
                ("carpenter", "🪚 Carpenter")
            };

            await _whatsApp.SendInteractiveButtonsAsync(
                state.PhoneNumber,
                "Select a Service",
                "Hmm, I didn't catch that. Please tap one of the options below to select a service:",
                buttons);
            return;
        }

        state.SelectedServiceType = serviceType;
        state.CurrentStep = ConversationStep.Photo;
        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _whatsApp.SendMessageAsync(state.PhoneNumber,
            $"✅ Great choice! *{serviceType}* selected.\n\n" +
            "📸 Now, please send a *photo* of the issue.\n\n" +
            "A clear photo helps our technician understand the problem and come prepared with the right tools.");
    }

    private async Task HandlePhotoAsync(ConversationState state, int numMedia, string? mediaUrl, string? mediaType)
    {
        if (numMedia < 1 || string.IsNullOrEmpty(mediaUrl) ||
            (mediaType != null && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
        {
            await _whatsApp.SendMessageAsync(state.PhoneNumber,
                "📸 I need a *photo* of the issue to continue.\n\n" +
                "💡 *Tip:* Use the camera icon 📷 or send an image from your gallery. " +
                "Make sure the problem area is clearly visible.");
            return;
        }

        state.PhotoUrl = mediaUrl;
        state.CurrentStep = ConversationStep.Video;
        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var buttons = new (string Id, string Title)[]
        {
            ("send_video", "📹 Send Video"),
            ("skip", "⏭️ Skip")
        };

        await _whatsApp.SendInteractiveButtonsAsync(
            state.PhoneNumber,
            "Photo Received! ✅",
            "Great, got your photo!\n\nWould you also like to send a short *video* of the issue? A video helps our technician get a better understanding of the problem.",
            buttons);
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
                "👍 No worries, video skipped!\n\n" +
                "📍 Now I need your *location* so we can send a technician.\n\n" +
                "You can either:\n" +
                "• Tap the 📎 icon → *Location* to share your GPS location\n" +
                "• Or type your *full address*");
            return;
        }

        // If user tapped "Send Video" button, prompt them to send the actual video
        if (text.Trim().Equals("send_video", StringComparison.OrdinalIgnoreCase))
        {
            await _whatsApp.SendMessageAsync(state.PhoneNumber,
                "🎥 Please send your video now.\n\n" +
                "💡 *Tip:* Use the camera icon 📷 to record or send a video from your gallery.");
            return;
        }

        if (numMedia < 1 || string.IsNullOrEmpty(mediaUrl) ||
            (mediaType != null && !mediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)))
        {
            var buttons = new (string Id, string Title)[]
            {
                ("send_video", "📹 Send Video"),
                ("skip", "⏭️ Skip")
            };

            await _whatsApp.SendInteractiveButtonsAsync(
                state.PhoneNumber,
                "Video Required",
                "I was expecting a video. Please send a video of the issue, or tap Skip to continue without one.",
                buttons);
            return;
        }

        state.VideoUrl = mediaUrl;
        state.CurrentStep = ConversationStep.Location;
        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _whatsApp.SendMessageAsync(state.PhoneNumber,
            "✅ Video received!\n\n" +
            "📍 Now I need your *location* so we can send a technician.\n\n" +
            "You can either:\n" +
            "• Tap the 📎 icon → *Location* to share your GPS location\n" +
            "• Or type your *full address*");
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
                "📍 I still need your location to proceed.\n\n" +
                "You can either:\n" +
                "• Tap the 📎 icon → *Location* to share your GPS\n" +
                "• Or type your *full address*\n\n" +
                "This helps us send the nearest available technician to you.");
            return;
        }

        state.CurrentStep = ConversationStep.CustomerDetails;
        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _whatsApp.SendMessageAsync(state.PhoneNumber,
            "✅ Location saved!\n\n" +
            "📝 Last step! Please provide your details in this format:\n\n" +
            "*Name, House/Flat No., Alternate Phone*\n\n" +
            "Example:\n_Ravi Kumar, #42 2nd Cross MG Road, 9876543210_\n\n" +
            "💡 Alternate phone is optional — just send Name and House No. if you prefer.");
    }

    private async Task HandleCustomerDetailsAsync(ConversationState state, string text)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries);

        if (parts.Length < 2)
        {
            await _whatsApp.SendMessageAsync(state.PhoneNumber,
                "😅 I couldn't parse that. Please provide your details separated by commas:\n\n" +
                "*Name, House/Flat No., Alternate Phone*\n\n" +
                "Example:\n_Ravi Kumar, #42 2nd Cross MG Road, 9876543210_\n\n" +
                "💡 Make sure to include at least your *name* and *house/flat number*.");
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

        // Build rich summary message
        var locationInfo = state.Latitude.HasValue
            ? $"📍 *Location:* GPS ({state.Latitude:F4}, {state.Longitude:F4})"
            : $"📍 *Location:* {state.AddressText}";

        var summary = $"✅ *Service Request Submitted!*\n\n" +
                      $"━━━━━━━━━━━━━━━━━━\n" +
                      $"🔖 *Request ID:* {requestCode}\n" +
                      $"🔧 *Service:* {serviceRequest.ServiceType}\n" +
                      $"👤 *Name:* {customerName}\n" +
                      $"🏠 *House:* {houseNumber}\n" +
                      (altPhone != null ? $"📞 *Alt Phone:* {altPhone}\n" : "") +
                      $"{locationInfo}\n" +
                      $"📸 *Photo:* Attached ✅\n" +
                      (serviceRequest.VideoUrl != null ? $"🎥 *Video:* Attached ✅\n" : $"🎥 *Video:* Skipped\n") +
                      $"━━━━━━━━━━━━━━━━━━\n\n" +
                      $"Our team will review your request and assign a technician shortly.\n\n" +
                      $"Thank you for choosing *TinyFix*! 🙏\n" +
                      $"Send *menu* anytime to place a new request.";

        await _whatsApp.SendMessageAsync(state.PhoneNumber, summary);

        _logger.LogInformation("Service request {Code} created via WhatsApp bot for {Phone}",
            requestCode, state.PhoneNumber);
    }

    private async Task HandleRatingAsync(ConversationState state, string text)
    {
        if (!int.TryParse(text.Trim(), out var rating) || rating < 1 || rating > 5)
        {
            // Resend the interactive list for rating
            var rows = new (string Id, string Title, string? Description)[]
            {
                ("1", "⭐ Poor", "1 star — Not satisfied"),
                ("2", "⭐⭐ Fair", "2 stars — Below expectations"),
                ("3", "⭐⭐⭐ Good", "3 stars — Met expectations"),
                ("4", "⭐⭐⭐⭐ Very Good", "4 stars — Above expectations"),
                ("5", "⭐⭐⭐⭐⭐ Excellent", "5 stars — Outstanding service")
            };

            await _whatsApp.SendInteractiveListAsync(
                state.PhoneNumber,
                "Rate Our Service",
                "Please select a rating from the list below to let us know how we did:",
                "Select Rating",
                rows);
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
        var thankYouMessage = rating switch
        {
            >= 4 => $"🙏 *Thank you for your {stars} rating!*\n\n" +
                    $"We're thrilled you had a great experience! Your feedback motivates our team.\n\n" +
                    $"See you next time on *TinyFix*! 💙",
            3 => $"🙏 *Thank you for your {stars} rating!*\n\n" +
                 $"We appreciate your honest feedback. We'll keep working to improve our service.\n\n" +
                 $"See you next time on *TinyFix*! 💙",
            _ => $"🙏 *Thank you for your {stars} rating.*\n\n" +
                 $"We're sorry we didn't meet your expectations. Your feedback helps us do better.\n\n" +
                 $"We hope to serve you better next time! 💙"
        };

        await _whatsApp.SendMessageAsync(state.PhoneNumber, thankYouMessage);

        _logger.LogInformation("Rating {Rating} received from {Phone}", rating, state.PhoneNumber);
    }

    private async Task<bool> TryHandleTechnicianJobResponseAsync(string phone, string body)
    {
        var bodyLower = body.ToLowerInvariant();
        // Strip country code for DB lookup (DB may store 10-digit, WhatsApp sends with 91 prefix)
        var phoneLocal = phone.Length > 10 ? phone[^10..] : phone;

        // Check for button press: accept_job_{id}, reject_job_{id}, start_job_{id}, complete_job_{id}
        if (bodyLower.StartsWith("accept_job_") || bodyLower.StartsWith("reject_job_"))
        {
            var isAccept = bodyLower.StartsWith("accept_job_");
            var idPart = body.Substring(isAccept ? "accept_job_".Length : "reject_job_".Length);

            if (int.TryParse(idPart, out var assignmentId))
            {
                var assignment = await _db.Assignments
                    .Include(a => a.Technician)
                    .Include(a => a.ServiceRequest)
                    .FirstOrDefaultAsync(a => a.Id == assignmentId
                        && (a.Technician.Phone == phone || a.Technician.Phone == phoneLocal)
                        && a.Status == AssignmentStatus.Pending);

                if (assignment != null)
                {
                    if (isAccept)
                        await AcceptAssignmentAsync(assignment);
                    else
                        await RejectAssignmentAsync(assignment);
                    return true;
                }
            }
        }

        if (bodyLower.StartsWith("start_job_"))
        {
            var idPart = body.Substring("start_job_".Length);
            if (int.TryParse(idPart, out var assignmentId))
            {
                var assignment = await _db.Assignments
                    .Include(a => a.Technician)
                    .Include(a => a.ServiceRequest)
                    .FirstOrDefaultAsync(a => a.Id == assignmentId
                        && (a.Technician.Phone == phone || a.Technician.Phone == phoneLocal)
                        && a.Status == AssignmentStatus.Accepted);

                if (assignment != null)
                {
                    await StartAssignmentAsync(assignment);
                    return true;
                }
            }
        }

        if (bodyLower.StartsWith("complete_job_"))
        {
            var idPart = body.Substring("complete_job_".Length);
            if (int.TryParse(idPart, out var assignmentId))
            {
                var assignment = await _db.Assignments
                    .Include(a => a.Technician)
                    .Include(a => a.ServiceRequest)
                    .FirstOrDefaultAsync(a => a.Id == assignmentId
                        && (a.Technician.Phone == phone || a.Technician.Phone == phoneLocal)
                        && a.Status == AssignmentStatus.Started);

                if (assignment != null)
                {
                    await CompleteAssignmentAsync(assignment);
                    return true;
                }
            }
        }

        // Check text reply fallback
        var technician = await _db.Technicians.FirstOrDefaultAsync(t => t.Phone == phone || t.Phone == phoneLocal);
        if (technician != null)
        {
            // "1"/"accept" or "2"/"reject" for pending assignments
            if (bodyLower is "1" or "accept" or "2" or "reject")
            {
                var pendingAssignment = await _db.Assignments
                    .Include(a => a.Technician)
                    .Include(a => a.ServiceRequest)
                    .Where(a => a.TechnicianId == technician.Id && a.Status == AssignmentStatus.Pending)
                    .OrderByDescending(a => a.AssignedAt)
                    .FirstOrDefaultAsync();

                if (pendingAssignment != null)
                {
                    if (bodyLower is "1" or "accept")
                        await AcceptAssignmentAsync(pendingAssignment);
                    else
                        await RejectAssignmentAsync(pendingAssignment);
                    return true;
                }
            }

            // "3"/"start" for accepted assignments
            if (bodyLower is "3" or "start")
            {
                var acceptedAssignment = await _db.Assignments
                    .Include(a => a.Technician)
                    .Include(a => a.ServiceRequest)
                    .Where(a => a.TechnicianId == technician.Id && a.Status == AssignmentStatus.Accepted)
                    .OrderByDescending(a => a.AcceptedAt)
                    .FirstOrDefaultAsync();

                if (acceptedAssignment != null)
                {
                    await StartAssignmentAsync(acceptedAssignment);
                    return true;
                }
            }

            // "4"/"complete"/"done" for started assignments
            if (bodyLower is "4" or "complete" or "done")
            {
                var startedAssignment = await _db.Assignments
                    .Include(a => a.Technician)
                    .Include(a => a.ServiceRequest)
                    .Where(a => a.TechnicianId == technician.Id && a.Status == AssignmentStatus.Started)
                    .OrderByDescending(a => a.StartedAt)
                    .FirstOrDefaultAsync();

                if (startedAssignment != null)
                {
                    await CompleteAssignmentAsync(startedAssignment);
                    return true;
                }
            }

            // Check if sender is a technician with any active assignment but sent something unrelated
            var activeAssignment = await _db.Assignments
                .Where(a => a.TechnicianId == technician.Id
                    && (a.Status == AssignmentStatus.Pending
                        || a.Status == AssignmentStatus.Accepted
                        || a.Status == AssignmentStatus.Started))
                .OrderByDescending(a => a.AssignedAt)
                .FirstOrDefaultAsync();

            if (activeAssignment != null)
            {
                var reminder = activeAssignment.Status switch
                {
                    AssignmentStatus.Pending =>
                        "You have a pending job assignment.\n\n" +
                        "Please reply *1* to *Accept* or *2* to *Reject*.",
                    AssignmentStatus.Accepted =>
                        "You have an accepted job.\n\n" +
                        "Please reply *3* or *start* when you begin work.",
                    AssignmentStatus.Started =>
                        "You have a job in progress.\n\n" +
                        "Please reply *4* or *done* when you've completed the work.",
                    _ => null
                };

                if (reminder != null)
                {
                    await _whatsApp.SendMessageAsync(phone, reminder);
                    return true;
                }
            }
        }

        return false;
    }

    private async Task AcceptAssignmentAsync(Assignment assignment)
    {
        assignment.Status = AssignmentStatus.Accepted;
        assignment.AcceptedAt = DateTime.UtcNow;

        // Move request to InProgress
        assignment.ServiceRequest.Status = "InProgress";
        assignment.ServiceRequest.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var requestCode = assignment.ServiceRequest.RequestCode
            ?? $"#{assignment.ServiceRequest.Id}";

        // Send confirmation to technician (with Start Job button)
        _ = _whatsApp.SendJobAcceptedConfirmationToTechnician(
            assignment.Id,
            assignment.Technician.Phone,
            requestCode,
            assignment.ServiceRequest.CustomerName,
            assignment.ServiceRequest.CustomerPhone);

        // Notify customer
        _ = _whatsApp.SendTechnicianAcceptedToCustomer(
            assignment.ServiceRequest.CustomerPhone,
            requestCode,
            assignment.Technician.Name,
            assignment.Technician.Phone);

        _logger.LogInformation("Assignment {Id} accepted by technician {Tech} for request {Code}",
            assignment.Id, assignment.Technician.Name, requestCode);
    }

    private async Task RejectAssignmentAsync(Assignment assignment)
    {
        assignment.Status = AssignmentStatus.Rejected;
        assignment.RejectedAt = DateTime.UtcNow;

        // Check if there are other active (non-rejected) assignments for this request
        var hasOtherActive = await _db.Assignments
            .AnyAsync(a => a.ServiceRequestId == assignment.ServiceRequestId
                && a.Id != assignment.Id
                && a.Status != AssignmentStatus.Rejected);

        if (!hasOtherActive)
        {
            // Revert request status to New
            assignment.ServiceRequest.Status = "New";
            assignment.ServiceRequest.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        var requestCode = assignment.ServiceRequest.RequestCode
            ?? $"#{assignment.ServiceRequest.Id}";

        // Send confirmation to technician
        _ = _whatsApp.SendJobRejectedConfirmationToTechnician(
            assignment.Technician.Phone,
            requestCode);

        _logger.LogInformation("Assignment {Id} rejected by technician {Tech} for request {Code}",
            assignment.Id, assignment.Technician.Name, requestCode);
    }

    private async Task StartAssignmentAsync(Assignment assignment)
    {
        assignment.Status = AssignmentStatus.Started;
        assignment.StartedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var requestCode = assignment.ServiceRequest.RequestCode
            ?? $"#{assignment.ServiceRequest.Id}";

        // Send confirmation to technician (with Complete Job button)
        _ = _whatsApp.SendJobStartedConfirmationToTechnician(
            assignment.Id,
            assignment.Technician.Phone,
            requestCode);

        // Notify customer that work has started
        _ = _whatsApp.SendJobStartedToCustomer(
            assignment.ServiceRequest.CustomerPhone,
            requestCode,
            assignment.Technician.Name);

        _logger.LogInformation("Assignment {Id} started by technician {Tech} for request {Code}",
            assignment.Id, assignment.Technician.Name, requestCode);
    }

    private async Task CompleteAssignmentAsync(Assignment assignment)
    {
        assignment.Status = AssignmentStatus.Completed;
        assignment.CompletedAt = DateTime.UtcNow;

        // Check if all assignments for this request are completed
        var allCompleted = await _db.Assignments
            .Where(a => a.ServiceRequestId == assignment.ServiceRequestId && a.Id != assignment.Id)
            .AllAsync(a => a.CompletedAt != null);

        if (allCompleted)
        {
            assignment.ServiceRequest.Status = "Completed";
            assignment.ServiceRequest.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        var requestCode = assignment.ServiceRequest.RequestCode
            ?? $"#{assignment.ServiceRequest.Id}";

        // Send completion confirmation to technician
        _ = _whatsApp.SendJobCompletedConfirmationToTechnician(
            assignment.Technician.Phone,
            requestCode);

        // Send completion message + rating request to customer when all assignments are done
        if (allCompleted)
        {
            _ = TriggerRatingFlowAsync(
                assignment.ServiceRequest.Id,
                assignment.ServiceRequest.CustomerPhone,
                requestCode);
        }

        _logger.LogInformation("Assignment {Id} completed by technician {Tech} for request {Code}",
            assignment.Id, assignment.Technician.Name, requestCode);
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
