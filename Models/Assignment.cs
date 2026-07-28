namespace backend.Models;

public class Assignment
{
    public int Id { get; set; }

    public int ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    public int TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public string? Notes { get; set; }
}
