using Microsoft.EntityFrameworkCore;
using SwipeService.Models;

namespace SwipeService.Data
{
    public class SwipeContext : DbContext
    {
        public SwipeContext(DbContextOptions<SwipeContext> options) : base(options) { }

        public DbSet<Swipe> Swipes { get; set; }
        public DbSet<Match> Matches { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Swipe entity configuration
            modelBuilder.Entity<Swipe>()
                .HasIndex(s => s.UserId)
                .HasDatabaseName("IX_UserId");

            modelBuilder.Entity<Swipe>()
                .HasIndex(s => s.TargetUserId)
                .HasDatabaseName("IX_TargetUserId");
                
            modelBuilder.Entity<Swipe>()
                .HasIndex(s => new { s.UserId, s.TargetUserId })
                .IsUnique()
                .HasDatabaseName("IX_UserId_TargetUserId");

            // Match entity configuration
            modelBuilder.Entity<Match>()
                .HasIndex(m => m.User1Id)
                .HasDatabaseName("IX_Match_User1Id");
                
            modelBuilder.Entity<Match>()
                .HasIndex(m => m.User2Id)
                .HasDatabaseName("IX_Match_User2Id");
                
            modelBuilder.Entity<Match>()
                .HasIndex(m => new { m.User1Id, m.User2Id })
                .IsUnique()
                .HasDatabaseName("IX_Match_User1Id_User2Id");

            // Ensure proper ordering for matches (smaller userId first)
            modelBuilder.Entity<Match>()
                .ToTable(table => table.HasCheckConstraint("CK_Match_UserOrder", "User1Id < User2Id"));
        }
    }
}