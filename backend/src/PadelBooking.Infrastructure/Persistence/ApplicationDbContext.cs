using Microsoft.EntityFrameworkCore;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Court> Courts { get; set; }

        public DbSet<User> Users { get; set; }

        public DbSet<Reservation> Reservations { get; set; }

        public DbSet<BlockedPeriod> BlockedPeriods { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(user => user.Email)
                    .HasMaxLength(255);

                entity.HasIndex(user => user.Email)
                    .IsUnique();
            });

            modelBuilder.Entity<Reservation>(entity =>
            {
                entity.Property(reservation => reservation.Status)
                    .HasMaxLength(20);

                entity.HasIndex(reservation => new
                {
                    reservation.CourtId,
                    reservation.Status,
                    reservation.StartTime,
                    reservation.EndTime
                });
            });

            modelBuilder.Entity<Court>(entity =>
            {
                entity.Property(court => court.ImageUrl)
                    .HasMaxLength(500);
            });

            modelBuilder.Entity<BlockedPeriod>(entity =>
            {
                entity.Property(period => period.Reason)
                    .HasMaxLength(300);

                entity.HasIndex(period => new
                {
                    period.CourtId,
                    period.StartTime,
                    period.EndTime
                });

                entity.HasOne(period => period.Court)
                    .WithMany()
                    .HasForeignKey(period => period.CourtId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
