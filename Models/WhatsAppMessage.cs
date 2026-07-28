using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class WhatsAppMessage
{
    public int Id { get; set; }

    public int? ServiceRequestId { get; set; }
    public ServiceRequest? ServiceRequest { get; set; }

    [MaxLength(10)]
    public string Direction { get; set; } = "Inbound";

    public string MessageBody { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
