namespace UniSecretApi.Entities;

public class Mention
{
    public int Id { get; set; }

    public int CommentId { get; set; }
    public Comment Comment { get; set; } = null!;

    public int MentionedUserId { get; set; }
    public User MentionedUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}