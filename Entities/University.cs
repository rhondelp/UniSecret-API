using System.ComponentModel.DataAnnotations;
using UniSecretApi.Enums;

namespace UniSecretApi.Entities;

public class University
{
    public int Id { get; set;}

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Domain {get; set;} = string.Empty;

    public string? LogoUrl { get; set; }

    public UniversityStatus Status { get; set; } = UniversityStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;


    // Navigation property: One university has many users
    public ICollection<User> Users { get; set; } = new List<User>();

    // Navigation property: One university has many confessions
    public ICollection<Confession> Confessions { get; set; } = new List<Confession>();
}