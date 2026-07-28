using System.ComponentModel.DataAnnotations;

namespace backend.Models.DTOs;

public class CreateServiceRequestDto
{
    [Required, MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string CustomerPhone { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ServiceType { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public class UpdateServiceRequestDto
{
    [MaxLength(100)]
    public string? CustomerName { get; set; }

    [MaxLength(20)]
    public string? CustomerPhone { get; set; }

    [MaxLength(50)]
    public string? ServiceType { get; set; }

    public string? Description { get; set; }

    [MaxLength(20)]
    public string? Status { get; set; }
}

public class UpdateStatusDto
{
    [Required, MaxLength(20)]
    public string Status { get; set; } = string.Empty;
}

public class ServiceRequestResponseDto
{
    public int Id { get; set; }
    public string? RequestCode { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PhotoUrl { get; set; }
    public string? VideoUrl { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public string? HouseNumber { get; set; }
    public string? AlternatePhone { get; set; }
    public int? Rating { get; set; }
    public DateTime? RatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<AssignmentResponseDto> Assignments { get; set; } = new();
}
