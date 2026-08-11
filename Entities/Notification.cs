using System.ComponentModel.DataAnnotations;

namespace UniSecretApi.Entities;

public class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; } // Recipient
    public User User { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty; // reply, mention, approved, etc.

    [Required]
    public string DataJson { get; set; } = string.Empty; // JSON payload

    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}