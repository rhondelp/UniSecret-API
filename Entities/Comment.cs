using System.ComponentModel.DataAnnotations;

namespace UniSecretApi.Entities;

public class Comment
{
    public int Id { get; set; }

    public int ConfessionId { get; set; } // FK
    public Confession Confession { get; set; } = null!;

    public int UserId { get; set; } // FK
    public User User { get; set; } = null!;

    // Self-referencing FK for threaded replies
    public int? ParentId { get; set; }
    public Comment? Parent { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();

    [Required]
    public string Body { get; set; } = string.Empty;

    public bool IsAnonymous { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Mention> Mentions { get; set; } = new List<Mention>();
}