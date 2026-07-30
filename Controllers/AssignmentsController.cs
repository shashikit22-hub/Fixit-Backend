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
[Authorize]
public class AssignmentsController : ControllerBase
{
    private readonly FixitDbContext _db;
    private readonly WhatsAppService _whatsApp;
    private readonly ConversationService _conversation;

    public AssignmentsController(FixitDbContext db, WhatsAppService whatsApp, ConversationService conversation)
    {
        _db = db;
        _whatsApp = whatsApp;
        _conversation = conversation;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAssignmentDto dto)
    {
        var serviceRequest = await _db.ServiceRequests.FindAsync(dto.ServiceRequestId);
        if (serviceRequest == null)
            return NotFound(new { message = "Service request not found" });

        var technician = await _db.Technicians.FindAsync(dto.TechnicianId);
        if (technician == null)
            return NotFound(new { message = "Technician not found" });

        var assignment = new Assignment
        {
            ServiceRequestId = dto.ServiceRequestId,
            TechnicianId = dto.TechnicianId,
            Notes = dto.Notes,
            AssignedAt = DateTime.UtcNow,
            Status = AssignmentStatus.Pending
        };

        _db.Assignments.Add(assignment);

        // Update service request status to Assigned
        if (serviceRequest.Status == "New")
        {
            serviceRequest.Status = "Assigned";
            serviceRequest.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Load technician for response
        await _db.Entry(assignment).Reference(a => a.Technician).LoadAsync();

        // Send WhatsApp job assignment to technician with Accept/Reject buttons
        _ = _whatsApp.SendJobAssignmentToTechnician(
            assignment.Id,
            assignment.Technician.Phone,
            assignment.Technician.Name,
            serviceRequest.RequestCode ?? $"#{serviceRequest.Id}",
            serviceRequest.CustomerName,
            serviceRequest.CustomerPhone,
            serviceRequest.ServiceType,
            serviceRequest.Description,
            serviceRequest.Address,
            serviceRequest.HouseNumber);

        // Send WhatsApp notification to customer
        _ = _whatsApp.SendTechnicianAssigned(
            serviceRequest.Id,
            serviceRequest.CustomerPhone,
            assignment.Technician.Name,
            assignment.Technician.Phone);

        return Ok(new AssignmentResponseDto
        {
            Id = assignment.Id,
            ServiceRequestId = assignment.ServiceRequestId,
            TechnicianId = assignment.TechnicianId,
            TechnicianName = assignment.Technician.Name,
            TechnicianPhone = assignment.Technician.Phone,
            AssignedAt = assignment.AssignedAt,
            CompletedAt = assignment.CompletedAt,
            Status = assignment.Status,
            AcceptedAt = assignment.AcceptedAt,
            RejectedAt = assignment.RejectedAt,
            Notes = assignment.Notes
        });
    }

    [HttpPut("{id}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var assignment = await _db.Assignments
            .Include(a => a.Technician)
            .Include(a => a.ServiceRequest)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assignment == null)
            return NotFound(new { message = "Assignment not found" });

        assignment.CompletedAt = DateTime.UtcNow;
        assignment.Status = AssignmentStatus.Completed;

        // Check if all assignments for this request are completed
        var allCompleted = await _db.Assignments
            .Where(a => a.ServiceRequestId == assignment.ServiceRequestId && a.Id != id)
            .AllAsync(a => a.CompletedAt != null);

        if (allCompleted)
        {
            assignment.ServiceRequest.Status = "Completed";
            assignment.ServiceRequest.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Send WhatsApp rating request when all assignments are completed
        if (allCompleted)
        {
            var requestCode = assignment.ServiceRequest.RequestCode
                ?? $"#{assignment.ServiceRequest.Id}";
            _ = _conversation.TriggerRatingFlowAsync(
                assignment.ServiceRequest.Id,
                assignment.ServiceRequest.CustomerPhone,
                requestCode);
        }

        return Ok(new AssignmentResponseDto
        {
            Id = assignment.Id,
            ServiceRequestId = assignment.ServiceRequestId,
            TechnicianId = assignment.TechnicianId,
            TechnicianName = assignment.Technician.Name,
            TechnicianPhone = assignment.Technician.Phone,
            AssignedAt = assignment.AssignedAt,
            CompletedAt = assignment.CompletedAt,
            Status = assignment.Status,
            AcceptedAt = assignment.AcceptedAt,
            RejectedAt = assignment.RejectedAt,
            Notes = assignment.Notes
        });
    }

    [HttpPost("{id}/resend")]
    public async Task<IActionResult> Resend(int id)
    {
        var assignment = await _db.Assignments
            .Include(a => a.Technician)
            .Include(a => a.ServiceRequest)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assignment == null)
            return NotFound(new { message = "Assignment not found" });

        if (assignment.Status != AssignmentStatus.Pending)
            return BadRequest(new { message = "Can only resend notifications for pending assignments" });

        _ = _whatsApp.SendJobAssignmentToTechnician(
            assignment.Id,
            assignment.Technician.Phone,
            assignment.Technician.Name,
            assignment.ServiceRequest.RequestCode ?? $"#{assignment.ServiceRequest.Id}",
            assignment.ServiceRequest.CustomerName,
            assignment.ServiceRequest.CustomerPhone,
            assignment.ServiceRequest.ServiceType,
            assignment.ServiceRequest.Description,
            assignment.ServiceRequest.Address,
            assignment.ServiceRequest.HouseNumber);

        return Ok(new { message = "Notification resent to technician" });
    }
}
