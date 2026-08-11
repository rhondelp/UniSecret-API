using System.ComponentModel.DataAnnotations;

namespace UniSecretApi.Entities;

public class ModerationLog
{
    public int Id { get; set; }

    public int AdminId { get; set; }
    public User Admin { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty; // approved, rejected, banned_user, etc.

    public int TargetId { get; set; }

    [Required]
    [MaxLength(20)]
    public string TargetType { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}