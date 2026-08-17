using System.ComponentModel.DataAnnotations;

namespace UniSecretApi.Entities;

public class Share
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int ConfessionId { get; set; }
    public Confession Confession { get; set; } = null!;

    public string? Caption { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}