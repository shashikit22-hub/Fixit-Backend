using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Models.DTOs;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TechniciansController : ControllerBase
{
    private readonly TinyfixDbContext _db;

    public TechniciansController(TinyfixDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? specialty,
        [FromQuery] bool? isAvailable)
    {
        var query = _db.Technicians
            .Include(t => t.Assignments)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(t =>
                t.Name.ToLower().Contains(q) ||
                t.Phone.Contains(q) ||
                (t.Email != null && t.Email.ToLower().Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(specialty))
            query = query.Where(t => t.Specialty == specialty);

        if (isAvailable.HasValue)
            query = query.Where(t => t.IsAvailable == isAvailable.Value);

        var technicians = await query.OrderBy(t => t.Name).ToListAsync();

        var allTechs = await _db.Technicians.ToListAsync();
        var total = allTechs.Count;
        var available = allTechs.Count(t => t.IsAvailable);

        var data = technicians.Select(MapToResponseDto).ToList();

        return Ok(new
        {
            data,
            stats = new { total, available, unavailable = total - available }
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var technician = await _db.Technicians
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (technician == null)
            return NotFound(new { message = "Technician not found" });

        return Ok(MapToResponseDto(technician));
    }

    [HttpGet("{id}/details")]
    public async Task<IActionResult> GetDetails(int id)
    {
        var technician = await _db.Technicians
            .Include(t => t.Assignments)
                .ThenInclude(a => a.ServiceRequest)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (technician == null)
            return NotFound(new { message = "Technician not found" });

        var assignments = technician.Assignments.OrderByDescending(a => a.AssignedAt).ToList();
        var totalJobs = assignments.Count;
        var completedJobs = assignments.Count(a => a.CompletedAt != null);
        var completionRate = totalJobs > 0 ? Math.Round((double)completedJobs / totalJobs * 100, 1) : 0;

        var completedWithRating = assignments
            .Where(a => a.CompletedAt != null && a.ServiceRequest.Rating.HasValue)
            .ToList();
        double? averageRating = completedWithRating.Count > 0
            ? Math.Round(completedWithRating.Average(a => a.ServiceRequest.Rating!.Value), 1)
            : null;

        var detail = new TechnicianDetailDto
        {
            Id = technician.Id,
            Name = technician.Name,
            Phone = technician.Phone,
            Specialty = technician.Specialty,
            IsAvailable = technician.IsAvailable,
            Email = technician.Email,
            Address = technician.Address,
            PhotoUrl = technician.PhotoUrl,
            GovtIdNumber = technician.GovtIdNumber,
            LicenseNumber = technician.LicenseNumber,
            JoinedAt = technician.JoinedAt,
            ActiveJobs = technician.Assignments.Count(a => a.CompletedAt == null),
            TotalJobs = totalJobs,
            CompletedJobs = completedJobs,
            CompletionRate = completionRate,
            AverageRating = averageRating,
            Assignments = assignments.Select(a => new TechnicianAssignmentDto
            {
                Id = a.Id,
                ServiceRequestId = a.ServiceRequestId,
                RequestCode = a.ServiceRequest.RequestCode,
                CustomerName = a.ServiceRequest.CustomerName,
                ServiceType = a.ServiceRequest.ServiceType,
                Status = a.ServiceRequest.Status,
                AssignedAt = a.AssignedAt,
                CompletedAt = a.CompletedAt,
                Notes = a.Notes,
                Rating = a.ServiceRequest.Rating,
                AssignmentStatus = a.Status,
            }).ToList()
        };

        return Ok(detail);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTechnicianDto dto)
    {
        var technician = new Technician
        {
            Name = dto.Name,
            Phone = dto.Phone,
            Specialty = dto.Specialty ?? string.Empty,
            IsAvailable = dto.IsAvailable,
            Email = dto.Email,
            Address = dto.Address,
            PhotoUrl = dto.PhotoUrl,
            GovtIdNumber = dto.GovtIdNumber,
            LicenseNumber = dto.LicenseNumber,
        };

        _db.Technicians.Add(technician);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { message = "A technician with this phone number already exists" });
        }

        return CreatedAtAction(nameof(GetById), new { id = technician.Id }, MapToResponseDto(technician));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTechnicianDto dto)
    {
        var technician = await _db.Technicians
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (technician == null)
            return NotFound(new { message = "Technician not found" });

        if (dto.Name != null) technician.Name = dto.Name;
        if (dto.Phone != null) technician.Phone = dto.Phone;
        if (dto.Specialty != null) technician.Specialty = dto.Specialty;
        if (dto.IsAvailable.HasValue) technician.IsAvailable = dto.IsAvailable.Value;
        if (dto.Email != null) technician.Email = dto.Email;
        if (dto.Address != null) technician.Address = dto.Address;
        if (dto.PhotoUrl != null) technician.PhotoUrl = dto.PhotoUrl;
        if (dto.GovtIdNumber != null) technician.GovtIdNumber = dto.GovtIdNumber;
        if (dto.LicenseNumber != null) technician.LicenseNumber = dto.LicenseNumber;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { message = "A technician with this phone number already exists" });
        }

        return Ok(MapToResponseDto(technician));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var technician = await _db.Technicians.FindAsync(id);
        if (technician == null)
            return NotFound(new { message = "Technician not found" });

        _db.Technicians.Remove(technician);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Technician deleted" });
    }

    private static TechnicianResponseDto MapToResponseDto(Technician t)
    {
        return new TechnicianResponseDto
        {
            Id = t.Id,
            Name = t.Name,
            Phone = t.Phone,
            Specialty = t.Specialty,
            IsAvailable = t.IsAvailable,
            Email = t.Email,
            Address = t.Address,
            PhotoUrl = t.PhotoUrl,
            GovtIdNumber = t.GovtIdNumber,
            LicenseNumber = t.LicenseNumber,
            JoinedAt = t.JoinedAt,
            ActiveJobs = t.Assignments.Count(a => a.CompletedAt == null),
        };
    }
}
