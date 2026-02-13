using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwipeService.Models;

/// <summary>
/// T184: Tracks behavioral analytics for swipe abuse detection.
/// One record per user, updated incrementally on each swipe and periodically by background job.
/// SwipeTrustScore is the composite score used for shadow-restricting abusive users.
/// </summary>
[Table("SwipeBehaviorStats")]
public class SwipeBehaviorStats
{
    [Key]
    public int Id { get; set; }

    /// <summary>Unique per user — one stats record per user.</summary>
    public int UserId { get; set; }

    public int TotalSwipes { get; set; }
    public int TotalLikes { get; set; }
    public int TotalPasses { get; set; }

    /// <summary>Right-swipe ratio: TotalLikes / TotalSwipes. Range 0.0 - 1.0.</summary>
    [Column(TypeName = "decimal(5,4)")]
    public decimal RightSwipeRatio { get; set; }

    /// <summary>Average swipes per minute, computed from recent session data.</summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal AvgSwipeVelocity { get; set; }

    /// <summary>Maximum consecutive likes without a single pass.</summary>
    public int PeakSwipeStreak { get; set; }

    /// <summary>Current consecutive like streak (reset on pass).</summary>
    public int CurrentConsecutiveLikes { get; set; }

    /// <summary>Number of rapid swipes (under 3s interval) detected.</summary>
    public int RapidSwipeCount { get; set; }

    /// <summary>Number of distinct days the user has swiped.</summary>
    public int DaysActive { get; set; }

    /// <summary>
    /// Composite trust score: 0 (banned) to 100 (fully trusted). Default 100.
    /// Used for shadow-restricting abusive users in candidate ranking.
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal SwipeTrustScore { get; set; } = 100;

    public DateTime LastCalculatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the user was flagged for suspicious behavior (null = not flagged).</summary>
    public DateTime? FlaggedAt { get; set; }

    /// <summary>Human-readable reason for flagging (null = not flagged).</summary>
    public string? FlagReason { get; set; }

    /// <summary>Timestamp of the most recent swipe (for velocity calculations).</summary>
    public DateTime? LastSwipeAt { get; set; }

    /// <summary>When the consecutive-like cooldown expires (null = not in cooldown).</summary>
    public DateTime? CooldownUntil { get; set; }
}
