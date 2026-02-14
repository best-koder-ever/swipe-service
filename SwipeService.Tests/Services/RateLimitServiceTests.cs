using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SwipeService.Data;
using SwipeService.Models;
using SwipeService.Services;

namespace SwipeService.Tests.Services;

public class RateLimitServiceTests : IDisposable
{
    private readonly SwipeContext _context;
    private readonly SwipeLimitsConfiguration _config;
    private readonly RateLimitService _service;

    public RateLimitServiceTests()
    {
        var options = new DbContextOptionsBuilder<SwipeContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new SwipeContext(options);
        _config = new SwipeLimitsConfiguration
        {
            DailySwipeLimit = 100,
            DailyLikeLimit = 50
        };
        _service = new RateLimitService(_context, _config, Mock.Of<ILogger<RateLimitService>>());
    }

    [Fact]
    public async Task CheckDailyLimit_NoPriorSwipes_Allowed()
    {
        var (isAllowed, remaining, limit) = await _service.CheckDailyLimitAsync(1, false);

        Assert.True(isAllowed);
        Assert.Equal(100, remaining);
        Assert.Equal(100, limit);
    }

    [Fact]
    public async Task CheckDailyLimit_Like_UsesLikeLimit()
    {
        var (isAllowed, remaining, limit) = await _service.CheckDailyLimitAsync(1, true);

        Assert.True(isAllowed);
        Assert.Equal(50, remaining);
        Assert.Equal(50, limit);
    }

    [Fact]
    public async Task CheckDailyLimit_UnderLimit_ReturnsCorrectRemaining()
    {
        _context.DailySwipeLimits.Add(new DailySwipeLimit
        {
            UserId = 1,
            Date = DateTime.UtcNow.Date,
            SwipeCount = 30,
            LastSwipeAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var (isAllowed, remaining, limit) = await _service.CheckDailyLimitAsync(1, false);

        Assert.True(isAllowed);
        Assert.Equal(70, remaining);
    }

    [Fact]
    public async Task CheckDailyLimit_AtLimit_NotAllowed()
    {
        _context.DailySwipeLimits.Add(new DailySwipeLimit
        {
            UserId = 1,
            Date = DateTime.UtcNow.Date,
            SwipeCount = 100,
            LastSwipeAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var (isAllowed, remaining, limit) = await _service.CheckDailyLimitAsync(1, false);

        Assert.False(isAllowed);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task CheckDailyLimit_OverLimit_NotAllowed()
    {
        _context.DailySwipeLimits.Add(new DailySwipeLimit
        {
            UserId = 1,
            Date = DateTime.UtcNow.Date,
            SwipeCount = 120,
            LastSwipeAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var (isAllowed, remaining, limit) = await _service.CheckDailyLimitAsync(1, false);

        Assert.False(isAllowed);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task CheckDailyLimit_YesterdaySwipes_NotCounted()
    {
        _context.DailySwipeLimits.Add(new DailySwipeLimit
        {
            UserId = 1,
            Date = DateTime.UtcNow.Date.AddDays(-1), // yesterday
            SwipeCount = 100,
            LastSwipeAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        var (isAllowed, remaining, limit) = await _service.CheckDailyLimitAsync(1, false);

        Assert.True(isAllowed);
        Assert.Equal(100, remaining); // today is fresh
    }

    [Fact]
    public async Task IncrementSwipeCount_FirstSwipe_CreatesRecord()
    {
        await _service.IncrementSwipeCountAsync(1);

        var record = await _context.DailySwipeLimits.SingleAsync();
        Assert.Equal(1, record.UserId);
        Assert.Equal(DateTime.UtcNow.Date, record.Date);
        Assert.Equal(1, record.SwipeCount);
    }

    [Fact]
    public async Task IncrementSwipeCount_ExistingRecord_Increments()
    {
        _context.DailySwipeLimits.Add(new DailySwipeLimit
        {
            UserId = 1,
            Date = DateTime.UtcNow.Date,
            SwipeCount = 5,
            LastSwipeAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _context.SaveChangesAsync();

        await _service.IncrementSwipeCountAsync(1);

        var record = await _context.DailySwipeLimits.SingleAsync();
        Assert.Equal(6, record.SwipeCount);
    }

    [Fact]
    public async Task IncrementSwipeCount_UpdatesLastSwipeAt()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        await _service.IncrementSwipeCountAsync(1);

        var record = await _context.DailySwipeLimits.SingleAsync();
        Assert.True(record.LastSwipeAt >= before);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
