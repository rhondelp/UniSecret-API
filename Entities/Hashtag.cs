using System.ComponentModel.DataAnnotations;

namespace UniSecretApi.Entities;

public class Hashtag
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Tag { get; set; } = string.Empty;

    public ICollection<ConfessionHashtag> ConfessionHashtags { get; set; } = new List<ConfessionHashtag>();
}