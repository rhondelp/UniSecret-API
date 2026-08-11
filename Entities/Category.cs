using System.ComponentModel.DataAnnotations;

namespace UniSecretApi.Entities;

public class Category
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Slug { get; set; } = string.Empty;

    public ICollection<Confession> Confessions { get; set; } = new List<Confession>();
}