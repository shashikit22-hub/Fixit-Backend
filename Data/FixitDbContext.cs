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
        });

        modelBuilder.Entity<Assignment>(entity =>
        {
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

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // Seed default admin user (password: admin123)
        modelBuilder.Entity<AdminUser>().HasData(new AdminUser
        {
            Id = 1,
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = "Admin"
        });
    }
}
