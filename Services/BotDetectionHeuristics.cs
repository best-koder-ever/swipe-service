using Microsoft.EntityFrameworkCore;
using SwipeService.Data;

namespace SwipeService.Services;

/// <summary>
/// T191: Bot detection heuristics.
/// Analyzes swipe patterns from the database to detect automated/bot behavior.
/// Returns a bot probability score (0.0 = human, 1.0 = definitely bot).
/// </summary>
public interface IBotDetectionService
{
    /// <summary>
    /// Analyze a user's swipe history and return a bot probability score.
    /// </summary>
    Task<BotDetectionResult> AnalyzeAsync(int userId);
}

public record BotDetectionResult(
    int UserId,
    double BotProbability,
    List<string> Signals,
    bool ShouldFlag
);

public class BotDetectionHeuristics : IBotDetectionService
{
    private readonly SwipeContext _context;
    private readonly ILogger<BotDetectionHeuristics> _logger;

    // Thresholds
    private const double ClockRegularityThreshold = 0.85;
    private const double Activity24x7Threshold = 0.9;
    private const int MinSwipesForAnalysis = 20;

    public BotDetectionHeuristics(SwipeContext context, ILogger<BotDetectionHeuristics> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BotDetectionResult> AnalyzeAsync(int userId)
    {
        var signals = new List<string>();
        double totalScore = 0;
        int signalCount = 0;

        var recentSwipes = await _context.Swipes
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(200)
            .Select(s => new { s.CreatedAt, s.IsLike, s.DeviceInfo })
            .ToListAsync();

        if (recentSwipes.Count < MinSwipesForAnalysis)
        {
            return new BotDetectionResult(userId, 0.0, new List<string> { "Insufficient data" }, false);
        }

        // 1. Clock regularity: bot swipes at suspiciously regular intervals
        var clockScore = AnalyzeClockRegularity(recentSwipes.Select(s => s.CreatedAt).ToList());
        if (clockScore > ClockRegularityThreshold)
        {
            signals.Add($"Clock regularity: {clockScore:P0} (threshold: {ClockRegularityThreshold:P0})");
        }
        totalScore += clockScore;
        signalCount++;

        // 2. 24/7 activity: humans sleep, bots don't
        var activitySpread = Analyze24x7Activity(recentSwipes.Select(s => s.CreatedAt).ToList());
        if (activitySpread > Activity24x7Threshold)
        {
            signals.Add($"24/7 activity spread: {activitySpread:P0}");
        }
        totalScore += activitySpread;
        signalCount++;

        // 3. Monotonic like pattern: always like or always pass
        var likeCount = recentSwipes.Count(s => s.IsLike);
        var likeRatio = (double)likeCount / recentSwipes.Count;
        var monoScore = likeRatio > 0.95 || likeRatio < 0.05 ? 0.9 : 0.0;
        if (monoScore > 0)
        {
            signals.Add($"Monotonic pattern: {likeRatio:P0} like rate");
        }
        totalScore += monoScore;
        signalCount++;

        // 4. Device fingerprint reuse: same device info across many swipes
        var deviceGroups = recentSwipes
            .Where(s => !string.IsNullOrEmpty(s.DeviceInfo))
            .GroupBy(s => s.DeviceInfo)
            .ToList();

        double deviceScore = 0;
        if (deviceGroups.Count == 1 && recentSwipes.Count > 50)
        {
            // All swipes from exact same device string with high volume
            deviceScore = 0.3;
            signals.Add("Single device fingerprint across all swipes");
        }
        else if (deviceGroups.Count == 0 && recentSwipes.Count > 20)
        {
            // No device info at all — suspicious for a real app user
            deviceScore = 0.5;
            signals.Add("No device fingerprint data");
        }
        totalScore += deviceScore;
        signalCount++;

        // Compute final bot probability
        var botProbability = signalCount > 0 ? totalScore / signalCount : 0;
        botProbability = Math.Clamp(botProbability, 0, 1);

        var shouldFlag = botProbability > 0.6;

        if (shouldFlag)
        {
            _logger.LogWarning("Bot detection: user {UserId} scored {Score:P0} with signals: {Signals}",
                userId, botProbability, string.Join(", ", signals));
        }

        return new BotDetectionResult(userId, botProbability, signals, shouldFlag);
    }

    /// <summary>
    /// Measures how regular the intervals between swipes are.
    /// Returns 0-1 where 1 = perfectly regular (bot-like).
    /// Uses coefficient of variation (lower CV = more regular = more suspicious).
    /// </summary>
    private static double AnalyzeClockRegularity(List<DateTime> timestamps)
    {
        if (timestamps.Count < 5) return 0;

        var sorted = timestamps.OrderBy(t => t).ToList();
        var intervals = new List<double>();

        for (int i = 1; i < sorted.Count; i++)
        {
            var interval = (sorted[i] - sorted[i - 1]).TotalSeconds;
            if (interval > 0 && interval < 300) // Only within-session intervals
            {
                intervals.Add(interval);
            }
        }

        if (intervals.Count < 5) return 0;

        var mean = intervals.Average();
        var stdDev = Math.Sqrt(intervals.Sum(x => Math.Pow(x - mean, 2)) / intervals.Count);
        var cv = stdDev / mean; // Coefficient of variation

        // Low CV = regular intervals = bot-like
        // CV < 0.1 => very regular (score ~0.9)
        // CV > 0.5 => human-like variation (score ~0.2)
        var regularity = Math.Max(0, 1.0 - (cv * 2));
        return Math.Clamp(regularity, 0, 1);
    }

    /// <summary>
    /// Measures how spread activity is across hours of the day.
    /// Returns 0-1 where 1 = active across all 24 hours (bot-like).
    /// Humans typically have 6-8 hours of inactivity (sleep).
    /// </summary>
    private static double Analyze24x7Activity(List<DateTime> timestamps)
    {
        if (timestamps.Count < 10) return 0;

        var hourBuckets = new HashSet<int>();
        foreach (var ts in timestamps)
        {
            hourBuckets.Add(ts.Hour);
        }

        // Active in what fraction of hours?
        var activeHourRatio = (double)hourBuckets.Count / 24.0;

        // If active in 20+ out of 24 hours, suspicious
        return activeHourRatio > 0.8 ? activeHourRatio : 0;
    }
}
