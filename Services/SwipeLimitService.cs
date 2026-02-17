using System.Collections.Concurrent;

namespace SwipeService.Services;

/// <summary>
/// Interface for daily swipe rate limiting.
/// </summary>
public interface ISwipeLimitService
{
    /// <summary>Check if user can still swipe today.</summary>
    bool CanSwipe(string userId);

    /// <summary>Record a swipe for rate limiting.</summary>
    void IncrementSwipe(string userId);

    /// <summary>Get remaining swipes for the day.</summary>
    int GetRemaining(string userId);

    /// <summary>Get full limit status info.</summary>
    SwipeLimitInfo GetLimitInfo(string userId);
}

/// <summary>
/// Swipe limit tracking info returned to clients.
/// </summary>
public record SwipeLimitInfo(
    int DailyLimit,
    int Used,
    int Remaining,
    DateTime ResetsAt
);

/// <summary>
/// In-memory daily swipe rate limiter.
/// Free tier: 100 swipes/day, resets at midnight UTC.
/// Production upgrade path: replace with Redis-backed implementation.
/// </summary>
public class SwipeLimitService : ISwipeLimitService
{
    private const int DailySwipeLimit = 100;

    private readonly ConcurrentDictionary<string, (int Count, DateTime ResetAt)> _tracker = new();
    private readonly ILogger<SwipeLimitService> _logger;

    /// <summary>
    /// Initializes the swipe limit service with logging.
    /// </summary>
    public SwipeLimitService(ILogger<SwipeLimitService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool CanSwipe(string userId)
    {
        var (count, _) = GetOrReset(userId);
        return count < DailySwipeLimit;
    }

    /// <inheritdoc/>
    public void IncrementSwipe(string userId)
    {
        var resetAt = GetNextMidnightUtc();
        _tracker.AddOrUpdate(userId,
            _ => (1, resetAt),
            (_, old) => DateTime.UtcNow >= old.ResetAt
                ? (1, resetAt)
                : (old.Count + 1, old.ResetAt));

        var remaining = GetRemaining(userId);
        if (remaining <= 10)
        {
            _logger.LogInformation(
                "[SwipeLimit] User {UserId} has {Remaining} swipes remaining today",
                userId, remaining);
        }
    }

    /// <inheritdoc/>
    public int GetRemaining(string userId)
    {
        var (count, _) = GetOrReset(userId);
        return Math.Max(0, DailySwipeLimit - count);
    }

    /// <inheritdoc/>
    public SwipeLimitInfo GetLimitInfo(string userId)
    {
        var (count, resetAt) = GetOrReset(userId);
        return new SwipeLimitInfo(
            DailyLimit: DailySwipeLimit,
            Used: count,
            Remaining: Math.Max(0, DailySwipeLimit - count),
            ResetsAt: resetAt);
    }

    private (int Count, DateTime ResetAt) GetOrReset(string userId)
    {
        var entry = _tracker.GetOrAdd(userId, _ => (0, GetNextMidnightUtc()));

        if (DateTime.UtcNow < entry.ResetAt)
            return entry;

        var reset = (0, GetNextMidnightUtc());
        _tracker[userId] = reset;
        return reset;
    }

    private static DateTime GetNextMidnightUtc() =>
        DateTime.UtcNow.Date.AddDays(1);
}
