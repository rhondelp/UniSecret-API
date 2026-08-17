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

        modelBuilder.Entity<ConfessionHashtag>()
            .HasKey(ch => new
            {
                ch.ConfessionId,
                ch.HashtagId
            });

        modelBuilder.Entity<ConfessionHashtag>()
            .HasOne(ch => ch.Confession)
            .WithMany(c => c.ConfessionHashtags)
            .HasForeignKey(ch => ch.ConfessionId);

        modelBuilder.Entity<ConfessionHashtag>()
            .HasOne(ch => ch.Hashtag)
            .WithMany(h => h.ConfessionHashtags)
            .HasForeignKey(ch => ch.HashtagId);

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

        // ------------------------------------------------------------
        // User indexes
        // ------------------------------------------------------------

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // ------------------------------------------------------------
        // Category / hashtag indexes
        // ------------------------------------------------------------

        modelBuilder.Entity<Category>()
            .HasIndex(c => c.Slug)
            .IsUnique();

        modelBuilder.Entity<Hashtag>()
            .HasIndex(h => h.Tag)
            .IsUnique();

        // ------------------------------------------------------------
        // High-traffic confession feed indexes
        // ------------------------------------------------------------
        //
        // The public feed filters by Status and optionally
        // UniversityId, then sorts by CreatedAt.
        //
        // These indexes support those access patterns.
        // ------------------------------------------------------------

        modelBuilder.Entity<Confession>()
            .HasIndex(c => new
            {
                c.Status,
                c.CreatedAt
            });

        modelBuilder.Entity<Confession>()
            .HasIndex(c => new
            {
                c.UniversityId,
                c.Status,
                c.CreatedAt
            });

        // ------------------------------------------------------------
        // Comment hierarchy
        // ------------------------------------------------------------

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Parent)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // ------------------------------------------------------------
        // Report relationships
        // ------------------------------------------------------------

        modelBuilder.Entity<Report>()
            .HasOne(r => r.Reporter)
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        // ------------------------------------------------------------
        // Moderation log
        // ------------------------------------------------------------

        modelBuilder.Entity<ModerationLog>()
            .HasOne(m => m.Admin)
            .WithMany()
            .HasForeignKey(m => m.AdminId)
            .OnDelete(DeleteBehavior.Restrict);

        // ------------------------------------------------------------
        // Enum storage
        // ------------------------------------------------------------

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