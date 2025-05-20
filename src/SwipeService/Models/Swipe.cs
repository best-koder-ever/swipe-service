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

        // Add indexes using Fluent API in DbContext instead of attributes
    }
}