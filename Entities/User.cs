using System.ComponentModel.DataAnnotations;
using UniSecretApi.Enums;

namespace UniSecretApi.Entities;

public class User
{
    public int Id { get; set; }

    public int UniversityId { get; set; } // Foreign Key
    public University University { get; set; } = null!; // Navigation Property

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    public DateTime? EmailVerifiedAt { get; set; }

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public UserRole Role { get; set; } = UserRole.Student;

    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties for related models
    public ICollection<Confession> Confessions { get; set; } = new List<Confession>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Like> Likes { get; set; } = new List<Like>();
    public ICollection<SavedPost> SavedPosts { get; set; } = new List<SavedPost>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}