using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SwipeService.Data;
using SwipeService.Models;

namespace SwipeService.Services;

/// <summary>
/// T185: Swipe behavior analysis service.
/// Calculates SwipeTrustScore from behavioral signals, detects suspicious patterns,
/// and provides the foundation for shadow-restricting abusive users.
/// Trust score = 100 minus ratio/velocity/streak penalties, clamped 0-100.
/// </summary>
public interface ISwipeBehaviorAnalyzer
{
    /// <summary>Full behavioral report for a user.</summary>
    Task<SwipeBehaviorReport> AnalyzeSwipePatternAsync(int userId);

    /// <summary>Calculate and persist the trust score.</summary>
    Task<decimal> CalculateSwipeTrustScoreAsync(int userId);

    /// <summary>Quick check: is this specific swipe suspicious?</summary>
    Task<(bool IsSuspicious, string? Reason)> IsSwipeSuspiciousAsync(int userId, bool isLike);

    /// <summary>Get or create the stats record for a user.</summary>
    Task<SwipeBehaviorStats> GetOrCreateStatsAsync(int userId);

    /// <summary>Update stats after a swipe event (incremental).</summary>
    Task UpdateStatsOnSwipeAsync(int userId, bool isLike);

    /// <summary>Check if user is in cooldown from circuit breaker.</summary>
    Task<(bool InCooldown, DateTime? CooldownUntil)> CheckCooldownAsync(int userId);

    /// <summary>Recalculate trust scores for all qualifying users (background job).</summary>
    Task<int> RecalculateAllAsync(CancellationToken ct);
}

/// <summary>Behavioral report returned by AnalyzeSwipePatternAsync.</summary>
public record SwipeBehaviorReport(
    int UserId,
    decimal TrustScore,
    decimal RightSwipeRatio,
    int TotalSwipes,
    decimal AvgVelocity,
    int PeakStreak,
    int CurrentStreak,
    int RapidSwipeCount,
    bool IsFlagged,
    string? FlagReason,
    List<string> Warnings
);

public class SwipeBehaviorAnalyzer : ISwipeBehaviorAnalyzer
{
    private readonly SwipeContext _context;
    private readonly IOptionsMonitor<SwipeBehaviorConfiguration> _config;
    private readonly ILogger<SwipeBehaviorAnalyzer> _logger;

    public SwipeBehaviorAnalyzer(
        SwipeContext context,
        IOptionsMonitor<SwipeBehaviorConfiguration> config,
        ILogger<SwipeBehaviorAnalyzer> logger)
    {
        _context = context;
        _config = config;
        _logger = logger;
    }

    public async Task<SwipeBehaviorReport> AnalyzeSwipePatternAsync(int userId)
    {
        var stats = await GetOrCreateStatsAsync(userId);
        var warnings = new List<string>();
        var cfg = _config.CurrentValue;

        if (stats.RightSwipeRatio > cfg.SuspiciousRightSwipeRatio)
            warnings.Add($"High right-swipe ratio: {stats.RightSwipeRatio:P1}");

        if (stats.AvgSwipeVelocity > cfg.SuspiciousVelocity)
            warnings.Add($"High swipe velocity: {stats.AvgSwipeVelocity:F1}/min");

        if (stats.PeakSwipeStreak > cfg.SuspiciousStreakLength)
            warnings.Add($"Long consecutive like streak: {stats.PeakSwipeStreak}");

        if (stats.RapidSwipeCount > 20)
            warnings.Add($"Frequent rapid swiping: {stats.RapidSwipeCount} events");

        return new SwipeBehaviorReport(
            UserId: userId,
            TrustScore: stats.SwipeTrustScore,
            RightSwipeRatio: stats.RightSwipeRatio,
            TotalSwipes: stats.TotalSwipes,
            AvgVelocity: stats.AvgSwipeVelocity,
            PeakStreak: stats.PeakSwipeStreak,
            CurrentStreak: stats.CurrentConsecutiveLikes,
            RapidSwipeCount: stats.RapidSwipeCount,
            IsFlagged: stats.FlaggedAt != null,
            FlagReason: stats.FlagReason,
            Warnings: warnings
        );
    }

    public async Task<decimal> CalculateSwipeTrustScoreAsync(int userId)
    {
        var stats = await GetOrCreateStatsAsync(userId);
        var cfg = _config.CurrentValue;

        decimal baseScore = 100m;

        // Penalty: high right-swipe ratio
        if (stats.RightSwipeRatio > cfg.SuspiciousRightSwipeRatio)
        {
            var excess = stats.RightSwipeRatio - cfg.SuspiciousRightSwipeRatio;
            baseScore -= Math.Min(30m, excess * cfg.RatioPenaltyWeight);
        }

        // Penalty: high swipe velocity
        if (stats.AvgSwipeVelocity > cfg.SuspiciousVelocity)
        {
            var excess = stats.AvgSwipeVelocity - cfg.SuspiciousVelocity;
            baseScore -= Math.Min(30m, excess * cfg.VelocityPenaltyWeight);
        }

        // Penalty: long consecutive like streak
        if (stats.PeakSwipeStreak > cfg.SuspiciousStreakLength)
        {
            var excess = stats.PeakSwipeStreak - cfg.SuspiciousStreakLength;
            baseScore -= Math.Min(30m, excess * cfg.StreakPenaltyWeight);
        }

        // Clamp to [0, 100]
        var trustScore = Math.Clamp(baseScore, 0m, 100m);

        // Persist
        stats.SwipeTrustScore = trustScore;
        stats.LastCalculatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogDebug("Trust score for user {UserId}: {Score}", userId, trustScore);
        return trustScore;
    }

    public async Task<(bool IsSuspicious, string? Reason)> IsSwipeSuspiciousAsync(int userId, bool isLike)
    {
        var stats = await GetOrCreateStatsAsync(userId);
        var cfg = _config.CurrentValue;

        // Check cooldown (circuit breaker)
        if (stats.CooldownUntil.HasValue && stats.CooldownUntil > DateTime.UtcNow)
        {
            return (true, $"Cooldown active until {stats.CooldownUntil:HH:mm}");
        }

        // Check rapid swiping
        if (stats.LastSwipeAt.HasValue)
        {
            var interval = (DateTime.UtcNow - stats.LastSwipeAt.Value).TotalSeconds;
            if (interval < cfg.RapidSwipeThresholdSeconds)
            {
                return (true, $"Rapid swiping detected ({interval:F1}s interval)");
            }
        }

        // Check consecutive likes (circuit breaker trigger)
        if (isLike && stats.CurrentConsecutiveLikes >= cfg.ConsecutiveLikeLimit)
        {
            // Trigger circuit breaker
            stats.CooldownUntil = DateTime.UtcNow.AddMinutes(cfg.CooldownMinutes);
            await _context.SaveChangesAsync();
            return (true, $"Consecutive like limit ({cfg.ConsecutiveLikeLimit}) reached. Cooldown {cfg.CooldownMinutes}m.");
        }

        return (false, null);
    }

    public async Task<SwipeBehaviorStats> GetOrCreateStatsAsync(int userId)
    {
        var stats = await _context.SwipeBehaviorStats
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (stats == null)
        {
            stats = new SwipeBehaviorStats
            {
                UserId = userId,
                SwipeTrustScore = 100m,
                LastCalculatedAt = DateTime.UtcNow
            };
            _context.SwipeBehaviorStats.Add(stats);
            await _context.SaveChangesAsync();
        }

        return stats;
    }

    public async Task UpdateStatsOnSwipeAsync(int userId, bool isLike)
    {
        var stats = await GetOrCreateStatsAsync(userId);
        var cfg = _config.CurrentValue;
        var now = DateTime.UtcNow;

        // Track velocity (rapid swipe detection)
        if (stats.LastSwipeAt.HasValue)
        {
            var intervalSeconds = (now - stats.LastSwipeAt.Value).TotalSeconds;
            if (intervalSeconds < cfg.RapidSwipeThresholdSeconds && intervalSeconds > 0)
            {
                stats.RapidSwipeCount++;
            }

            // Update rolling average velocity (swipes/min)
            if (intervalSeconds > 0 && intervalSeconds < 300) // Only count within 5-min sessions
            {
                var instantVelocity = (decimal)(60.0 / intervalSeconds);
                // Exponential moving average (alpha = 0.1)
                stats.AvgSwipeVelocity = stats.AvgSwipeVelocity * 0.9m + instantVelocity * 0.1m;
            }
        }

        // Update totals
        stats.TotalSwipes++;
        if (isLike)
        {
            stats.TotalLikes++;
            stats.CurrentConsecutiveLikes++;
            if (stats.CurrentConsecutiveLikes > stats.PeakSwipeStreak)
                stats.PeakSwipeStreak = stats.CurrentConsecutiveLikes;
        }
        else
        {
            stats.TotalPasses++;
            stats.CurrentConsecutiveLikes = 0;  // Reset streak on pass
        }

        // Update ratio
        stats.RightSwipeRatio = stats.TotalSwipes > 0
            ? (decimal)stats.TotalLikes / stats.TotalSwipes
            : 0m;

        stats.LastSwipeAt = now;

        // Track active days (simple: compare date)
        var today = now.Date;
        var lastDate = stats.LastCalculatedAt.Date;
        if (today != lastDate)
            stats.DaysActive++;

        // Recalculate trust score every N swipes
        if (stats.TotalSwipes % cfg.RecalcEveryNSwipes == 0)
        {
            await CalculateTrustScoreInternal(stats, cfg);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<(bool InCooldown, DateTime? CooldownUntil)> CheckCooldownAsync(int userId)
    {
        var stats = await _context.SwipeBehaviorStats
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (stats?.CooldownUntil != null && stats.CooldownUntil > DateTime.UtcNow)
        {
            return (true, stats.CooldownUntil);
        }

        return (false, null);
    }

    public async Task<int> RecalculateAllAsync(CancellationToken ct)
    {
        var cfg = _config.CurrentValue;
        var cutoff = DateTime.UtcNow.AddHours(-24);

        // Users with recent swipe activity
        var activeUserIds = await _context.Swipes
            .Where(s => s.CreatedAt > cutoff)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync(ct);

        int recalculated = 0;

        foreach (var userId in activeUserIds)
        {
            ct.ThrowIfCancellationRequested();

            var stats = await GetOrCreateStatsAsync(userId);

            // Recalculate from full swipe history
            var fullStats = await _context.Swipes
                .Where(s => s.UserId == userId)
                .GroupBy(s => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Likes = g.Count(s => s.IsLike),
                    Passes = g.Count(s => !s.IsLike)
                })
                .FirstOrDefaultAsync(ct);

            if (fullStats != null && fullStats.Total >= cfg.MinRecentSwipesForRecalc)
            {
                stats.TotalSwipes = fullStats.Total;
                stats.TotalLikes = fullStats.Likes;
                stats.TotalPasses = fullStats.Passes;
                stats.RightSwipeRatio = fullStats.Total > 0
                    ? (decimal)fullStats.Likes / fullStats.Total
                    : 0m;

                await CalculateTrustScoreInternal(stats, cfg);

                // Auto-flag if below threshold
                if (stats.SwipeTrustScore < cfg.FlagThreshold && stats.FlaggedAt == null)
                {
                    stats.FlaggedAt = DateTime.UtcNow;
                    stats.FlagReason = $"Auto-flagged: trust score {stats.SwipeTrustScore:F1} below {cfg.FlagThreshold}";
                    _logger.LogWarning("User {UserId} auto-flagged: trust score {Score}",
                        userId, stats.SwipeTrustScore);
                }

                recalculated++;
            }
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Background recalc: {Count} users recalculated", recalculated);
        return recalculated;
    }

    private Task CalculateTrustScoreInternal(SwipeBehaviorStats stats, SwipeBehaviorConfiguration cfg)
    {
        decimal baseScore = 100m;

        // Penalty: high right-swipe ratio
        if (stats.RightSwipeRatio > cfg.SuspiciousRightSwipeRatio)
        {
            var excess = stats.RightSwipeRatio - cfg.SuspiciousRightSwipeRatio;
            baseScore -= Math.Min(30m, excess * cfg.RatioPenaltyWeight);
        }

        // Penalty: high swipe velocity
        if (stats.AvgSwipeVelocity > cfg.SuspiciousVelocity)
        {
            var excess = stats.AvgSwipeVelocity - cfg.SuspiciousVelocity;
            baseScore -= Math.Min(30m, excess * cfg.VelocityPenaltyWeight);
        }

        // Penalty: long consecutive like streak
        if (stats.PeakSwipeStreak > cfg.SuspiciousStreakLength)
        {
            var excess = stats.PeakSwipeStreak - cfg.SuspiciousStreakLength;
            baseScore -= Math.Min(30m, excess * cfg.StreakPenaltyWeight);
        }

        stats.SwipeTrustScore = Math.Clamp(baseScore, 0m, 100m);
        stats.LastCalculatedAt = DateTime.UtcNow;

        return Task.CompletedTask;
    }
}
