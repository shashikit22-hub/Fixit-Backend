using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TechniciansController : ControllerBase
{
    private readonly FixitDbContext _db;

    public TechniciansController(FixitDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var technicians = await _db.Technicians
            .OrderBy(t => t.Name)
            .ToListAsync();
        return Ok(technicians);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var technician = await _db.Technicians.FindAsync(id);
        if (technician == null)
            return NotFound(new { message = "Technician not found" });
        return Ok(technician);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Technician technician)
    {
        technician.Id = 0;
        _db.Technicians.Add(technician);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = technician.Id }, technician);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Technician updated)
    {
        var technician = await _db.Technicians.FindAsync(id);
        if (technician == null)
            return NotFound(new { message = "Technician not found" });

        technician.Name = updated.Name;
        technician.Phone = updated.Phone;
        technician.Specialty = updated.Specialty;
        technician.IsAvailable = updated.IsAvailable;

        await _db.SaveChangesAsync();
        return Ok(technician);
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
}
