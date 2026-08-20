using EventApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventApi.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Organizer> Organizers => Set<Organizer>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.StudentCode).HasMaxLength(20);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.PasswordSalt).HasMaxLength(500);
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Address).HasMaxLength(250);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<Organizer>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.ContactEmail).HasMaxLength(150);
            entity.Property(e => e.ContactPhone).HasMaxLength(20);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.HasOne(e => e.Location).WithMany(l => l.Events).HasForeignKey(e => e.LocationId);
            entity.HasOne(e => e.Organizer).WithMany(o => o.Events).HasForeignKey(e => e.OrganizerId);
            entity.HasOne(e => e.CreatedBy).WithMany(u => u.Events).HasForeignKey(e => e.CreatedById);
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.HasIndex(e => new { e.EventId, e.StudentId }).IsUnique();
            entity.HasOne(r => r.Event).WithMany(e => e.Registrations).HasForeignKey(r => r.EventId);
            entity.HasOne(r => r.Student).WithMany(u => u.Registrations).HasForeignKey(r => r.StudentId);
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.Property(e => e.Comment).HasMaxLength(1000);
            entity.HasIndex(e => new { e.EventId, e.StudentId }).IsUnique();
            entity.HasOne(f => f.Event).WithMany(e => e.Feedbacks).HasForeignKey(f => f.EventId);
            entity.HasOne(f => f.Student).WithMany(u => u.Feedbacks).HasForeignKey(f => f.StudentId);
        });
    }
}
