using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class ServiceRequest
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string CustomerPhone { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ServiceType { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(20)]
    public string? RequestCode { get; set; }

    [MaxLength(500)]
    public string? PhotoUrl { get; set; }

    [MaxLength(500)]
    public string? VideoUrl { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? HouseNumber { get; set; }

    [MaxLength(20)]
    public string? AlternatePhone { get; set; }

    public int? Rating { get; set; }

    public DateTime? RatedAt { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "New";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<WhatsAppMessage> WhatsAppMessages { get; set; } = new List<WhatsAppMessage>();
}
