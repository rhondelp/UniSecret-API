namespace UniSecretApi.Entities;

public class ConfessionHashtag
{
    public int ConfessionId { get; set; }
    public Confession Confession { get; set; } = null!;

    public int HashtagId { get; set; }
    public Hashtag Hashtag { get; set; } = null!;
}