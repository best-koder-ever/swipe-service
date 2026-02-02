using Microsoft.EntityFrameworkCore;
using SwipeService.Models;

namespace SwipeService.Data
{
    public class SwipeContext : DbContext
    {
        public SwipeContext(DbContextOptions<SwipeContext> options) : base(options) { }

        public DbSet<Swipe> Swipes { get; set; }
        public DbSet<Match> Matches { get; set; }
        public DbSet<DailySwipeLimit> DailySwipeLimits { get; set; }
        
        // Read-only access to Profiles for match validation (from user_service_db)
        public DbSet<Profile> Profiles { get; set; }
        
        // Local mapping of UserId → ProfileId for match validation in demo mode
        public DbSet<UserProfileMapping> UserProfileMappings { get; set; }

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
                
            // Idempotency key index for fast lookup and uniqueness enforcement
            modelBuilder.Entity<Swipe>()
                .HasIndex(s => s.IdempotencyKey)
                .IsUnique()
                .HasDatabaseName("IX_IdempotencyKey")
                .HasFilter("[IdempotencyKey] IS NOT NULL");
            
            // T062: Composite indexes for common queries
            modelBuilder.Entity<Swipe>()
                .HasIndex(s => new { s.UserId, s.IsLike, s.CreatedAt })
                .HasDatabaseName("IX_Swipes_User_Like_Created");
                
            modelBuilder.Entity<Swipe>()
                .HasIndex(s => new { s.UserId, s.CreatedAt })
                .HasDatabaseName("IX_Swipes_User_Created");

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

            // DailySwipeLimit entity configuration
            modelBuilder.Entity<DailySwipeLimit>()
                .HasIndex(d => new { d.UserId, d.Date })
                .IsUnique()
                .HasDatabaseName("IX_DailySwipeLimit_UserId_Date");

            modelBuilder.Entity<DailySwipeLimit>()
                .HasIndex(d => d.Date)
                .HasDatabaseName("IX_DailySwipeLimit_Date");
            
            // Profile entity configuration (read-only, no migrations needed)
            modelBuilder.Entity<Profile>()
                .ToTable("Profiles", tb => tb.ExcludeFromMigrations())
                .HasKey(p => p.Id);
                
            modelBuilder.Entity<Profile>()
                .HasIndex(p => p.UserId)
                .HasDatabaseName("IX_Profile_UserId");
            
            // UserProfileMapping configuration (local cache for demo mode)
            modelBuilder.Entity<UserProfileMapping>()
                .HasKey(m => m.ProfileId);
                
            modelBuilder.Entity<UserProfileMapping>()
                .HasIndex(m => m.UserId)
                .IsUnique()
                .HasDatabaseName("IX_UserProfileMapping_UserId");
        }
    }
}