namespace backend.Models.DTOs;

public class DashboardDto
{
    public int NewCount { get; set; }
    public int AssignedCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public int TotalRequests { get; set; }
    public int AvailableTechnicians { get; set; }
    public int TotalTechnicians { get; set; }
    public List<ServiceRequestResponseDto> RecentRequests { get; set; } = new();
}
