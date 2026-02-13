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

/// <summary>
/// Configuration for swipe abuse detection thresholds (Phase 14.9)
/// </summary>
public class SwipeBehaviorConfiguration
{
    /// <summary>Right-swipe ratio above which penalty applies (0.0-1.0).</summary>
    public decimal SuspiciousRightSwipeRatio { get; set; } = 0.7m;

    /// <summary>Swipes/minute above which velocity penalty applies.</summary>
    public decimal SuspiciousVelocity { get; set; } = 10m;

    /// <summary>Consecutive likes above which streak penalty applies.</summary>
    public int SuspiciousStreakLength { get; set; } = 20;

    /// <summary>Max consecutive likes before circuit breaker triggers 429.</summary>
    public int ConsecutiveLikeLimit { get; set; } = 30;

    /// <summary>Minutes to cool down after circuit breaker triggers.</summary>
    public int CooldownMinutes { get; set; } = 15;

    /// <summary>Rapid swipe threshold in seconds — swipes faster than this are flagged.</summary>
    public double RapidSwipeThresholdSeconds { get; set; } = 3.0;

    /// <summary>Recalculate trust score every N swipes (incremental).</summary>
    public int RecalcEveryNSwipes { get; set; } = 10;

    /// <summary>Weight for right-swipe ratio penalty (max penalty points).</summary>
    public decimal RatioPenaltyWeight { get; set; } = 100m;

    /// <summary>Weight for velocity penalty (max penalty points per velocity unit).</summary>
    public decimal VelocityPenaltyWeight { get; set; } = 3m;

    /// <summary>Weight for streak penalty (max penalty per streak unit).</summary>
    public decimal StreakPenaltyWeight { get; set; } = 1.5m;

    /// <summary>Profile completeness below this % adds penalty.</summary>
    public int LowProfileThreshold { get; set; } = 30;

    /// <summary>Penalty points for low profile completeness.</summary>
    public decimal LowProfilePenalty { get; set; } = 15m;

    /// <summary>Bonus points for high message-after-match rate.</summary>
    public decimal MessageBonusBoost { get; set; } = 10m;

    /// <summary>Message-after-match rate above which bonus applies.</summary>
    public decimal MessageBonusThreshold { get; set; } = 0.5m;

    /// <summary>Background recalculation interval in hours.</summary>
    public int BackgroundRecalcIntervalHours { get; set; } = 6;

    /// <summary>Minimum swipes in last 24h to qualify for background recalc.</summary>
    public int MinRecentSwipesForRecalc { get; set; } = 10;

    /// <summary>Trust score below which bot-detection flags are raised.</summary>
    public decimal FlagThreshold { get; set; } = 30m;
}
