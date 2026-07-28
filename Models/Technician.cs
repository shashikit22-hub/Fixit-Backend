using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class Technician
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Specialty { get; set; } = string.Empty;

    public bool IsAvailable { get; set; } = true;

    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
