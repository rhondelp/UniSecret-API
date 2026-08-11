namespace UniSecretApi.Entities;

public class SavedPost
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int ConfessionId { get; set; }
    public Confession Confession { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}