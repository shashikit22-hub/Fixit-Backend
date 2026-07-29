using System.ComponentModel.DataAnnotations;

namespace backend.Models.DTOs;

public class CreateTechnicianDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    [RegularExpression(@"^[0-9+\-\s()]+$", ErrorMessage = "Invalid phone format")]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Specialty { get; set; }

    [MaxLength(100), EmailAddress]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(500)]
    public string? PhotoUrl { get; set; }

    [MaxLength(50)]
    public string? GovtIdNumber { get; set; }

    [MaxLength(500)]
    public string? LicenseNumber { get; set; }

    public bool IsAvailable { get; set; } = true;
}

public class UpdateTechnicianDto
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(20)]
    [RegularExpression(@"^[0-9+\-\s()]+$", ErrorMessage = "Invalid phone format")]
    public string? Phone { get; set; }

    [MaxLength(50)]
    public string? Specialty { get; set; }

    [MaxLength(100), EmailAddress]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(500)]
    public string? PhotoUrl { get; set; }

    [MaxLength(50)]
    public string? GovtIdNumber { get; set; }

    [MaxLength(500)]
    public string? LicenseNumber { get; set; }

    public bool? IsAvailable { get; set; }
}

public class TechnicianResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? PhotoUrl { get; set; }
    public string? GovtIdNumber { get; set; }
    public string? LicenseNumber { get; set; }
    public DateTime JoinedAt { get; set; }
    public int ActiveJobs { get; set; }
}

public class TechnicianDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? PhotoUrl { get; set; }
    public string? GovtIdNumber { get; set; }
    public string? LicenseNumber { get; set; }
    public DateTime JoinedAt { get; set; }
    public int ActiveJobs { get; set; }
    public int TotalJobs { get; set; }
    public int CompletedJobs { get; set; }
    public double CompletionRate { get; set; }
    public double? AverageRating { get; set; }
    public List<TechnicianAssignmentDto> Assignments { get; set; } = new();
}

public class TechnicianAssignmentDto
{
    public int Id { get; set; }
    public int ServiceRequestId { get; set; }
    public string? RequestCode { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public int? Rating { get; set; }
}
