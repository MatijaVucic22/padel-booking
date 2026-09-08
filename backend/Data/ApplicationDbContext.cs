using Microsoft.EntityFrameworkCore;
using PadelBooking.Api.Models;

namespace PadelBooking.Api.Data
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
        }
    }
}
