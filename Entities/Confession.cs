using System.ComponentModel.DataAnnotations;
using UniSecretApi.Enums;

namespace UniSecretApi.Entities;

public class Confession
{
    public int Id { get; set; }

    public int UserId { get; set; } // FK -> Always stored for accountability
    public User User { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public int UniversityId { get; set; } // FK
    public University University { get; set; } = null!;

    public int CategoryId { get; set; } // FK
    public Category Category { get; set; } = null!;

    [Required]
    public string Body { get; set; } = string.Empty;

    public bool IsAnonymous { get; set; } = true;

    public ConfessionStatus Status { get; set; } = ConfessionStatus.Pending;

    public DateTime? ScheduledAt { get; set; }

    public int? ApprovedById { get; set; } 
    public User? ApprovedBy { get; set; }

    public string? RejectedReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<ConfessionHashtag> ConfessionHashtags { get; set; } = new List<ConfessionHashtag>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public ICollection<Share> Shares { get; set; } = new List<Share>();
}