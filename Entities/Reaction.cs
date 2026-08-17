using System.ComponentModel.DataAnnotations;
using UniSecretApi.Enums;

namespace UniSecretApi.Entities;

public class Reaction
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int ReactableId { get; set; }

    [Required]
    [MaxLength(20)]
    public string ReactableType { get; set; } = string.Empty; // "Confession" or "Comment"

    public ReactionType Type { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}