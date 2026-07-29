using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models.DTOs;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly FixitDbContext _db;

    public DashboardController(FixitDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var requests = _db.ServiceRequests.AsQueryable();
        var todayUtc = DateTime.UtcNow.Date;

        var ratedRequests = await requests
            .Where(r => r.Rating.HasValue)
            .Select(r => r.Rating!.Value)
            .ToListAsync();

        var dashboard = new DashboardDto
        {
            NewCount = await requests.CountAsync(r => r.Status == "New"),
            AssignedCount = await requests.CountAsync(r => r.Status == "Assigned"),
            InProgressCount = await requests.CountAsync(r => r.Status == "InProgress"),
            CompletedCount = await requests.CountAsync(r => r.Status == "Completed"),
            CancelledCount = await requests.CountAsync(r => r.Status == "Cancelled"),
            TotalRequests = await requests.CountAsync(),
            AvailableTechnicians = await _db.Technicians.CountAsync(t => t.IsAvailable),
            TotalTechnicians = await _db.Technicians.CountAsync(),
            TodayNewCount = await requests.CountAsync(r => r.CreatedAt >= todayUtc),
            TodayCompletedCount = await requests.CountAsync(r => r.Status == "Completed" && r.UpdatedAt.HasValue && r.UpdatedAt.Value >= todayUtc),
            PendingAssignments = await requests.CountAsync(r => r.Status == "New" && !r.Assignments.Any()),
            AverageRating = ratedRequests.Count > 0 ? Math.Round(ratedRequests.Average(), 1) : null,
            RatingDistribution = ratedRequests
                .GroupBy(r => r)
                .ToDictionary(g => g.Key, g => g.Count()),
            ServiceTypeDistribution = await requests
                .Where(r => r.ServiceType != null)
                .GroupBy(r => r.ServiceType!)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Type, g => g.Count),
            RecentRequests = await _db.ServiceRequests
                .Include(sr => sr.Assignments)
                    .ThenInclude(a => a.Technician)
                .OrderByDescending(sr => sr.CreatedAt)
                .Take(10)
                .Select(sr => new ServiceRequestResponseDto
                {
                    Id = sr.Id,
                    RequestCode = sr.RequestCode,
                    CustomerName = sr.CustomerName,
                    CustomerPhone = sr.CustomerPhone,
                    ServiceType = sr.ServiceType,
                    Description = sr.Description,
                    PhotoUrl = sr.PhotoUrl,
                    Rating = sr.Rating,
                    Status = sr.Status,
                    CreatedAt = sr.CreatedAt,
                    UpdatedAt = sr.UpdatedAt,
                    Assignments = sr.Assignments.Select(a => new AssignmentResponseDto
                    {
                        Id = a.Id,
                        ServiceRequestId = a.ServiceRequestId,
                        TechnicianId = a.TechnicianId,
                        TechnicianName = a.Technician.Name,
                        TechnicianPhone = a.Technician.Phone,
                        AssignedAt = a.AssignedAt,
                        CompletedAt = a.CompletedAt,
                        Notes = a.Notes
                    }).ToList()
                })
                .ToListAsync()
        };

        return Ok(dashboard);
    }
}
