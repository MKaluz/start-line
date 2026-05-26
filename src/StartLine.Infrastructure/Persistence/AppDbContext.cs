using Microsoft.EntityFrameworkCore;
using StartLine.Domain.Events;
using StartLine.Domain.Outbox;
using StartLine.Domain.Registrations;
using StartLine.Domain.Users;

namespace StartLine.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Race> Races => Set<Race>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.Role).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(rt => rt.Id);
            entity.Property(rt => rt.Token).IsRequired().HasMaxLength(512);
            entity.HasIndex(rt => rt.Token).IsUnique();
            entity.Property(rt => rt.UserId).IsRequired();
            entity.Property(rt => rt.CreatedAt).IsRequired();
            entity.Property(rt => rt.ExpiresAt).IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Location).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Description).HasMaxLength(4096);
            entity.Property(e => e.OrganizerId);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.Property(e => e.DeletedAt);
            entity.Property(e => e.DeletedBy);

            entity.HasMany(e => e.Races)
                .WithOne()
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Race>(entity =>
        {
            entity.ToTable("Races");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(256);
            entity.Property(r => r.Capacity).IsRequired();
            entity.Property(r => r.BasePrice).IsRequired().HasColumnType("numeric(18,2)");
            entity.Property(r => r.EarlyBirdPrice).HasColumnType("numeric(18,2)");
            entity.Property(r => r.EarlyBirdDeadline);
            entity.Property(r => r.OrganizerId);
            entity.Property(r => r.CreatedAt).IsRequired();
            entity.Property(r => r.MinAge);
            entity.Property(r => r.MaxAge);
            entity.Property(r => r.AllowedGender);
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.ToTable("Registrations");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.RaceId).IsRequired();
            entity.Property(r => r.AthleteId).IsRequired();
            entity.Property(r => r.Status).IsRequired();
            entity.Property(r => r.ReservationExpiresAt).IsRequired();
            entity.Property(r => r.FirstName).IsRequired().HasMaxLength(256);
            entity.Property(r => r.LastName).IsRequired().HasMaxLength(256);
            entity.Property(r => r.Email).IsRequired().HasMaxLength(256);
            entity.Property(r => r.DateOfBirth).IsRequired();
            entity.Property(r => r.Gender).IsRequired();
            entity.Property(r => r.Club).HasMaxLength(256);
            entity.Property(r => r.Phone).HasMaxLength(64);
            entity.Property(r => r.CreatedAt).IsRequired();

            entity.HasIndex(r => r.RaceId);
            entity.HasIndex(r => r.AthleteId);

            entity.HasOne<Race>()
                .WithMany()
                .HasForeignKey(r => r.RaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Type).IsRequired().HasMaxLength(256);
            entity.Property(o => o.Payload).IsRequired();
            entity.Property(o => o.CreatedAt).IsRequired();
            entity.Property(o => o.ProcessedAt);
            entity.Property(o => o.Error).HasMaxLength(2048);

            entity.HasIndex(o => o.ProcessedAt);
        });
    }
}


