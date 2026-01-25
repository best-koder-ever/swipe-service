using System.ComponentModel.DataAnnotations;

namespace SwipeService.Models
{
    public class SwipeRequest
    {
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public int TargetUserId { get; set; }
        
        [Required]
        public bool IsLike { get; set; }
        
        /// <summary>
        /// Optional idempotency key for retry safety. If provided, duplicate requests with the same key
        /// will return the original result instead of creating a duplicate swipe.
        /// </summary>
        public string? IdempotencyKey { get; set; }
    }
    
    public class BatchSwipeRequest
    {
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public List<SwipeAction> Swipes { get; set; } = new();
        
        /// <summary>
        /// Optional batch-level idempotency key. If provided, the entire batch will be treated as idempotent.
        /// </summary>
        public string? IdempotencyKey { get; set; }
    }
    
    public class SwipeAction
    {
        [Required]
        public int TargetUserId { get; set; }
        
        [Required]
        public bool IsLike { get; set; }
    }
    
    public class SwipeResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsMutualMatch { get; set; }
        public int MatchId { get; set; }
    }
    
    public class UserSwipeHistory
    {
        public int UserId { get; set; }
        public List<SwipeRecord> Swipes { get; set; } = new();
        public int TotalSwipes { get; set; }
        public int TotalLikes { get; set; }
        public int TotalPasses { get; set; }
    }
    
    public class SwipeRecord
    {
        public int Id { get; set; }
        public int TargetUserId { get; set; }
        public bool IsLike { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    
    public class MatchResult
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int MatchedUserId { get; set; }
        public DateTime MatchedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
