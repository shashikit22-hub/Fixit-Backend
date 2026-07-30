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
    public int TodayNewCount { get; set; }
    public int TodayCompletedCount { get; set; }
    public int PendingAssignments { get; set; }
    public double? AverageRating { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new();
    public Dictionary<string, int> ServiceTypeDistribution { get; set; } = new();
    public List<ServiceRequestResponseDto> RecentRequests { get; set; } = new();
    public List<DailyTrendDto> WeeklyTrend { get; set; } = new();
    public List<TopTechnicianDto> TopTechnicians { get; set; } = new();
}

public class DailyTrendDto
{
    public DateTime Date { get; set; }
    public int NewCount { get; set; }
    public int CompletedCount { get; set; }
}

public class TopTechnicianDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public int CompletedJobs { get; set; }
    public double? AverageRating { get; set; }
}
