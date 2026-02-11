using System.ComponentModel.DataAnnotations.Schema;

namespace SwipeService.Models
{
    [Table("Swipes")]
    public class Swipe
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int TargetUserId { get; set; }
        public bool IsLike { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? UserLocation { get; set; }
        public string? DeviceInfo { get; set; }

        /// <summary>
        /// Optional idempotency key for retry safety. Prevents duplicate processing of the same logical swipe.
        /// </summary>
        public string? IdempotencyKey { get; set; }

        // Navigation properties
        public Match? Match { get; set; }
    }

    [Table("Matches")]
    public class Match
    {
        public int Id { get; set; }
        public int User1Id { get; set; }
        public int User2Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public DateTime? UnmatchedAt { get; set; }
        public int? UnmatchedByUserId { get; set; }

        // Navigation properties
        public List<Swipe> Swipes { get; set; } = new();
    }
}