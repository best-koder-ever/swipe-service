using System.Collections.Concurrent;

namespace SwipeService.Services;

/// <summary>
/// Interface for daily swipe rate limiting.
/// </summary>
public interface ISwipeLimitService
{
    bool CanSwipe(string userId);
    void IncrementSwipe(string userId);
    int GetRemaining(string userId);
    SwipeLimitInfo GetLimitInfo(string userId);
}

/// <summary>
/// Swipe limit tracking info
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

    public SwipeLimitService(ILogger<SwipeLimitService> logger)
    {
        _logger = logger;
    }

    public bool CanSwipe(string userId)
    {
        var info = GetOrCreate(userId);
        return info.Count < DailySwipeLimit;
    }

    public void IncrementSwipe(string userId)
    {
        _tracker.AddOrUpdate(
            userId,
            _ => (1, GetNextMidnightUtc()),
            (_, existing) =>
            {
                if (DateTime.UtcNow >= existing.ResetAt)
                    return (1, GetNextMidnightUtc());
                return (existing.Count + 1, existing.ResetAt);
            }
        );

        var remaining = GetRemaining(userId);
        if (remaining <= 10)
        {
            _logger.LogInformation(
                "[SwipeLimit] User {UserId} has {Remaining} swipes remaining today",
                userId, remaining);
        }
    }

    public int GetRemaining(string userId)
    {
        var info = GetOrCreate(userId);
        return Math.Max(0, DailySwipeLimit - info.Count);
    }

    public SwipeLimitInfo GetLimitInfo(string userId)
    {
        var info = GetOrCreate(userId);
        return new SwipeLimitInfo(
            DailyLimit: DailySwipeLimit,
            Used: info.Count,
            Remaining: Math.Max(0, DailySwipeLimit - info.Count),
            ResetsAt: info.ResetAt
        );
    }

    private (int Count, DateTime ResetAt) GetOrCreate(string userId)
    {
        var entry = _tracker.GetOrAdd(userId, _ => (0, GetNextMidnightUtc()));

        // Auto-reset if past midnight
        if (DateTime.UtcNow >= entry.ResetAt)
        {
            var reset = (0, GetNextMidnightUtc());
            _tracker[userId] = reset;
            return reset;
        }

        return entry;
    }

    private static DateTime GetNextMidnightUtc()
    {
        var tomorrow = DateTime.UtcNow.Date.AddDays(1);
        return tomorrow;
    }
}
