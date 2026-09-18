// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using VideoEncoder.Database.Enums;
using VideoEncoder.Database.Models;

namespace VideoEncoder.Database;

public class VideoEncoderDbContext : DbContext
{
    public VideoEncoderDbContext(DbContextOptions<VideoEncoderDbContext> options)
        : base(options)
    {
    }

    // DbSet properties expose tables for querying
    public DbSet<VideoQueue> QueueElements => Set<VideoQueue>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VideoQueue>(entity =>
        {
            entity.ToTable("qt_videos");

            // Configure primary key
            entity.HasKey(p => p.Id);

            // Configure properties
            entity.Property(p => p.UploadTime)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(p => p.UserId)
                .IsRequired();

            entity.Property(p => p.FileUrl)
                .IsRequired();

            entity.Property(p => p.Status)
                .HasDefaultValue(EStatus.NONE);
        });
    }
}