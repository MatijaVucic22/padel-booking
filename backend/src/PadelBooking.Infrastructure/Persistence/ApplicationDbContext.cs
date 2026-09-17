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

        public DbSet<Payment> Payments { get; set; }

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

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.Property(payment => payment.Amount).HasPrecision(18, 2);
                entity.Property(payment => payment.Currency).HasMaxLength(3);
                entity.Property(payment => payment.Provider).HasMaxLength(30);
                entity.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(20);
                entity.Property(payment => payment.Purpose).HasConversion<string>().HasMaxLength(30);
                entity.Property(payment => payment.TargetTotalPrice).HasPrecision(18, 2);
                entity.Property(payment => payment.ExternalSessionId).HasMaxLength(255);
                entity.Property(payment => payment.ExternalPaymentIntentId).HasMaxLength(255);
                entity.HasIndex(payment => payment.ReservationId);
                entity.HasIndex(payment => payment.ExternalSessionId).IsUnique();
                entity.HasIndex(payment => new { payment.Purpose, payment.Status,
                    payment.TargetStartTime, payment.TargetEndTime });
                entity.HasOne(payment => payment.Reservation)
                    .WithMany()
                    .HasForeignKey(payment => payment.ReservationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
