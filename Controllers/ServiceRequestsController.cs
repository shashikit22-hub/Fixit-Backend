using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Models.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServiceRequestsController : ControllerBase
{
    private readonly TinyfixDbContext _db;
    private readonly ExcelExportService _excel;

    private readonly WhatsAppService _whatsApp;

    public ServiceRequestsController(TinyfixDbContext db, ExcelExportService excel, WhatsAppService whatsApp)
    {
        _db = db;
        _excel = excel;
        _whatsApp = whatsApp;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? serviceType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        [FromQuery] string? rating,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.ServiceRequests
            .Include(sr => sr.Assignments)
                .ThenInclude(a => a.Technician)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(sr => sr.Status == status);

        if (!string.IsNullOrEmpty(serviceType))
            query = query.Where(sr => sr.ServiceType == serviceType);

        if (from.HasValue)
            query = query.Where(sr => sr.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(sr => sr.CreatedAt <= to.Value.Date.AddDays(1));

        if (!string.IsNullOrEmpty(search))
            query = query.Where(sr =>
                sr.CustomerName.Contains(search) ||
                sr.CustomerPhone.Contains(search) ||
                (sr.Description != null && sr.Description.Contains(search)) ||
                (sr.RequestCode != null && sr.RequestCode.Contains(search)));

        if (!string.IsNullOrEmpty(rating))
        {
            if (rating == "rated")
                query = query.Where(sr => sr.Rating.HasValue);
            else if (rating == "unrated")
                query = query.Where(sr => !sr.Rating.HasValue);
            else if (int.TryParse(rating, out var ratingValue))
                query = query.Where(sr => sr.Rating == ratingValue);
        }

        var totalCount = await query.CountAsync();
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var requests = await query
            .OrderByDescending(sr => sr.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new
        {
            data = requests.Select(MapToDto).ToList(),
            totalCount,
            page,
            pageSize
        };
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetById(int id)
    {
        var request = await _db.ServiceRequests
            .Include(sr => sr.Assignments)
                .ThenInclude(a => a.Technician)
            .FirstOrDefaultAsync(sr => sr.Id == id);

        if (request == null)
            return NotFound(new { message = "Service request not found" });

        return Ok(MapToDto(request));
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequestDto dto)
    {
        var request = new ServiceRequest
        {
            CustomerName = dto.CustomerName,
            CustomerPhone = dto.CustomerPhone,
            ServiceType = dto.ServiceType,
            Description = dto.Description,
            Status = "New",
            CreatedAt = DateTime.UtcNow
        };

        _db.ServiceRequests.Add(request);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, MapToDto(request));
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateServiceRequestDto dto)
    {
        var request = await _db.ServiceRequests.FindAsync(id);
        if (request == null)
            return NotFound(new { message = "Service request not found" });

        if (dto.CustomerName != null) request.CustomerName = dto.CustomerName;
        if (dto.CustomerPhone != null) request.CustomerPhone = dto.CustomerPhone;
        if (dto.ServiceType != null) request.ServiceType = dto.ServiceType;
        if (dto.Description != null) request.Description = dto.Description;
        if (dto.Status != null) request.Status = dto.Status;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(request));
    }

    [HttpPut("{id}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDto dto)
    {
        var validStatuses = new[] { "New", "Assigned", "InProgress", "Completed", "Cancelled" };
        if (!validStatuses.Contains(dto.Status))
            return BadRequest(new { message = $"Invalid status. Valid values: {string.Join(", ", validStatuses)}" });

        var request = await _db.ServiceRequests.FindAsync(id);
        if (request == null)
            return NotFound(new { message = "Service request not found" });

        request.Status = dto.Status;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Send WhatsApp notification for specific status changes
        _ = _whatsApp.SendStatusUpdate(request.Id, request.CustomerPhone, dto.Status);

        return Ok(MapToDto(request));
    }

    [HttpGet("export")]
    [Authorize]
    public async Task<IActionResult> Export(
        [FromQuery] string? status,
        [FromQuery] string? serviceType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var query = _db.ServiceRequests
            .Include(sr => sr.Assignments)
                .ThenInclude(a => a.Technician)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(sr => sr.Status == status);

        if (!string.IsNullOrEmpty(serviceType))
            query = query.Where(sr => sr.ServiceType == serviceType);

        if (from.HasValue)
            query = query.Where(sr => sr.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(sr => sr.CreatedAt <= to.Value);

        var requests = await query.OrderByDescending(sr => sr.CreatedAt).ToListAsync();

        var bytes = _excel.ExportServiceRequests(requests);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"ServiceRequests_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    private static ServiceRequestResponseDto MapToDto(ServiceRequest sr) => new()
    {
        Id = sr.Id,
        RequestCode = sr.RequestCode,
        CustomerName = sr.CustomerName,
        CustomerPhone = sr.CustomerPhone,
        ServiceType = sr.ServiceType,
        Description = sr.Description,
        PhotoUrl = sr.PhotoUrl,
        VideoUrl = sr.VideoUrl,
        Latitude = sr.Latitude,
        Longitude = sr.Longitude,
        Address = sr.Address,
        HouseNumber = sr.HouseNumber,
        AlternatePhone = sr.AlternatePhone,
        Rating = sr.Rating,
        RatedAt = sr.RatedAt,
        Status = sr.Status,
        CreatedAt = sr.CreatedAt,
        UpdatedAt = sr.UpdatedAt,
        Assignments = sr.Assignments.Select(a => new AssignmentResponseDto
        {
            Id = a.Id,
            ServiceRequestId = a.ServiceRequestId,
            TechnicianId = a.TechnicianId,
            TechnicianName = a.Technician?.Name ?? "",
            TechnicianPhone = a.Technician?.Phone ?? "",
            AssignedAt = a.AssignedAt,
            CompletedAt = a.CompletedAt,
            Status = a.Status,
            AcceptedAt = a.AcceptedAt,
            StartedAt = a.StartedAt,
            RejectedAt = a.RejectedAt,
            Notes = a.Notes
        }).ToList()
    };
}
