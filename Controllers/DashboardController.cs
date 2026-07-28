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
            RecentRequests = await _db.ServiceRequests
                .Include(sr => sr.Assignments)
                    .ThenInclude(a => a.Technician)
                .OrderByDescending(sr => sr.CreatedAt)
                .Take(10)
                .Select(sr => new ServiceRequestResponseDto
                {
                    Id = sr.Id,
                    CustomerName = sr.CustomerName,
                    CustomerPhone = sr.CustomerPhone,
                    ServiceType = sr.ServiceType,
                    Description = sr.Description,
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
