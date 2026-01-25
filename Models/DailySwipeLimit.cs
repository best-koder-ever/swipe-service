using System.ComponentModel.DataAnnotations.Schema;

namespace SwipeService.Models
{
    /// <summary>
    /// Tracks daily swipe counts per user for rate limiting
    /// </summary>
    [Table("DailySwipeLimits")]
    public class DailySwipeLimit
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime Date { get; set; } // Date only (UTC), no time component
        public int SwipeCount { get; set; }
        public DateTime LastSwipeAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
