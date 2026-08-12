using Microsoft.EntityFrameworkCore;
using UniSecretApi.Entities;

namespace UniSecretApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // DbSets represent database tables
    public DbSet<University> Universities { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Confession> Confessions { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Hashtag> Hashtags { get; set; } = null!;
    public DbSet<ConfessionHashtag> ConfessionHashtags { get; set; } = null!;
    public DbSet<Comment> Comments { get; set; } = null!;
    public DbSet<Like> Likes { get; set; } = null!;
    public DbSet<Mention> Mentions { get; set; } = null!;
    public DbSet<SavedPost> SavedPosts { get; set; } = null!;
    public DbSet<Report> Reports { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<ModerationLog> ModerationLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Configure Composite Primary Key for ConfessionHashtag (Pivot Table)
        modelBuilder.Entity<ConfessionHashtag>()
            .HasKey(ch => new { ch.ConfessionId, ch.HashtagId });

        modelBuilder.Entity<ConfessionHashtag>()
            .HasOne(ch => ch.Confession)
            .WithMany(c => c.ConfessionHashtags)
            .HasForeignKey(ch => ch.ConfessionId);

        modelBuilder.Entity<ConfessionHashtag>()
            .HasOne(ch => ch.Hashtag)
            .WithMany(h => h.ConfessionHashtags)
            .HasForeignKey(ch => ch.HashtagId);

        // 2. Configure Confession Relationships & Prevent Multiple Cascade Paths
        modelBuilder.Entity<Confession>()
            .HasOne(c => c.User)
            .WithMany(u => u.Confessions)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Confession>()
            .HasOne(c => c.ApprovedBy)
            .WithMany()
            .HasForeignKey(c => c.ApprovedById)
            .OnDelete(DeleteBehavior.Restrict);

        // 3. Configure Unique Constraints
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Category>()
            .HasIndex(c => c.Slug)
            .IsUnique();

        modelBuilder.Entity<Hashtag>()
            .HasIndex(h => h.Tag)
            .IsUnique();

        // 4. Configure Comment Parent-Child (Threaded Replies)
        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Parent)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // 5. Configure Report Foreign Keys
        modelBuilder.Entity<Report>()
            .HasOne(r => r.Reporter)
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);
            
        // 6. Configure ModerationLog Foreign Keys
        modelBuilder.Entity<ModerationLog>()
            .HasOne(m => m.Admin)
            .WithMany()
            .HasForeignKey(m => m.AdminId)
            .OnDelete(DeleteBehavior.Restrict);

        // 7. Store Enums as Strings in Database
        modelBuilder.Entity<University>()
            .Property(u => u.Status)
            .HasConversion<string>();

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<string>();

        modelBuilder.Entity<User>()
            .Property(u => u.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Confession>()
            .Property(c => c.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Report>()
            .Property(r => r.Reason)
            .HasConversion<string>();

        modelBuilder.Entity<Report>()
            .Property(r => r.Status)
            .HasConversion<string>();
    }
}