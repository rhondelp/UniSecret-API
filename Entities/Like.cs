using System.ComponentModel.DataAnnotations;

namespace UniSecretApi.Entities;

public class Like
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int LikeableId { get; set; } 

    [Required]
    [MaxLength(20)]
    public string LikeableType { get; set; } = string.Empty; 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}