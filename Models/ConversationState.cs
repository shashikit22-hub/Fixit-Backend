using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class ConversationState
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(30)]
    public string CurrentStep { get; set; } = ConversationStep.Greeting;

    [MaxLength(100)]
    public string? ProfileName { get; set; }

    [MaxLength(50)]
    public string? SelectedServiceType { get; set; }

    [MaxLength(500)]
    public string? PhotoUrl { get; set; }

    [MaxLength(500)]
    public string? VideoUrl { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    [MaxLength(500)]
    public string? AddressText { get; set; }

    [MaxLength(100)]
    public string? CustomerName { get; set; }

    [MaxLength(200)]
    public string? CustomerArea { get; set; }

    public int? AwaitingRatingForRequestId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
