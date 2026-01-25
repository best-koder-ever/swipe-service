namespace SwipeService.Models
{
    /// <summary>
    /// Configuration for swipe rate limits
    /// </summary>
    public class SwipeLimitsConfiguration
    {
        public int DailySwipeLimit { get; set; } = 100;
        public int DailyLikeLimit { get; set; } = 50;
    }
}
