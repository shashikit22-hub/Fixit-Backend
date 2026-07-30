using Microsoft.EntityFrameworkCore;
using backend.Models;

namespace backend.Data;

public class FixitDbContext : DbContext
{
    public FixitDbContext(DbContextOptions<FixitDbContext> options) : base(options) { }

    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<WhatsAppMessage> WhatsAppMessages => Set<WhatsAppMessage>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<ConversationState> ConversationStates => Set<ConversationState>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ServiceRequest>(entity =>
        {
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ServiceType);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.RequestCode);
        });

        modelBuilder.Entity<ConversationState>(entity =>
        {
            entity.HasIndex(e => e.PhoneNumber).IsUnique();
        });

        modelBuilder.Entity<Technician>(entity =>
        {
            entity.HasIndex(e => e.Phone).IsUnique();
            entity.HasIndex(e => e.Email);
        });

        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.HasIndex(a => a.Status);

            entity.HasOne(a => a.ServiceRequest)
                .WithMany(sr => sr.Assignments)
                .HasForeignKey(a => a.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Technician)
                .WithMany(t => t.Assignments)
                .HasForeignKey(a => a.TechnicianId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WhatsAppMessage>(entity =>
        {
            entity.HasOne(w => w.ServiceRequest)
                .WithMany(sr => sr.WhatsAppMessages)
                .HasForeignKey(w => w.ServiceRequestId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MediaFile>(entity =>
        {
            entity.Property(e => e.Id).HasColumnType("varchar(36)");
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.Data).HasColumnType("longblob");
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // Seed default admin users
        modelBuilder.Entity<AdminUser>().HasData(
            new AdminUser
            {
                Id = 1,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Admin",
                FullName = "Admin"
            },
            new AdminUser
            {
                Id = 2,
                Username = "admin@tinyfix.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@#$123456"),
                Role = "Admin",
                FullName = "VarunKumar"
            }
        );
    }
}
