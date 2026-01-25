using Microsoft.EntityFrameworkCore;
using SwipeService.Data;
using SwipeService.Models;

namespace SwipeService.Services
{
    public interface IRateLimitService
    {
        Task<(bool IsAllowed, int Remaining, int Limit)> CheckDailyLimitAsync(int userId, bool isLike);
        Task IncrementSwipeCountAsync(int userId);
    }

    public class RateLimitService : IRateLimitService
    {
        private readonly SwipeContext _context;
        private readonly SwipeLimitsConfiguration _config;
        private readonly ILogger<RateLimitService> _logger;

        public RateLimitService(
            SwipeContext context,
            SwipeLimitsConfiguration config,
            ILogger<RateLimitService> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        public async Task<(bool IsAllowed, int Remaining, int Limit)> CheckDailyLimitAsync(int userId, bool isLike)
        {
            var today = DateTime.UtcNow.Date;
            var limit = isLike ? _config.DailyLikeLimit : _config.DailySwipeLimit;

            var dailyLimit = await _context.DailySwipeLimits
                .FirstOrDefaultAsync(d => d.UserId == userId && d.Date == today);

            var currentCount = dailyLimit?.SwipeCount ?? 0;
            var remaining = Math.Max(0, limit - currentCount);
            var isAllowed = currentCount < limit;

            if (!isAllowed)
            {
                _logger.LogWarning(
                    "User {UserId} exceeded daily {LimitType} limit ({CurrentCount}/{Limit})",
                    userId,
                    isLike ? "like" : "swipe",
                    currentCount,
                    limit);
            }

            return (isAllowed, remaining, limit);
        }

        public async Task IncrementSwipeCountAsync(int userId)
        {
            var today = DateTime.UtcNow.Date;

            var dailyLimit = await _context.DailySwipeLimits
                .FirstOrDefaultAsync(d => d.UserId == userId && d.Date == today);

            if (dailyLimit == null)
            {
                dailyLimit = new DailySwipeLimit
                {
                    UserId = userId,
                    Date = today,
                    SwipeCount = 1,
                    LastSwipeAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                _context.DailySwipeLimits.Add(dailyLimit);
            }
            else
            {
                dailyLimit.SwipeCount++;
                dailyLimit.LastSwipeAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
    }
}
