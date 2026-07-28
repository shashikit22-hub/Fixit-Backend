using System.ComponentModel.DataAnnotations;

namespace backend.Models.DTOs;

public class CreateAssignmentDto
{
    [Required]
    public int ServiceRequestId { get; set; }

    [Required]
    public int TechnicianId { get; set; }

    public string? Notes { get; set; }
}

public class AssignmentResponseDto
{
    public int Id { get; set; }
    public int ServiceRequestId { get; set; }
    public int TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public string TechnicianPhone { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
}
