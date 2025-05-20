using Microsoft.EntityFrameworkCore;
using SwipeService.Models;

namespace SwipeService.Data
{
    public class SwipeContext : DbContext
    {
        public SwipeContext(DbContextOptions<SwipeContext> options) : base(options) { }

        public DbSet<Swipe> Swipes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Define indexes
            modelBuilder.Entity<Swipe>()
                .HasIndex(s => s.UserId)
                .HasDatabaseName("IX_UserId");

            modelBuilder.Entity<Swipe>()
                .HasIndex(s => s.TargetUserId)
                .HasDatabaseName("IX_TargetUserId");
        }
    }
}