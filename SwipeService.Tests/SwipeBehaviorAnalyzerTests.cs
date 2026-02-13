using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SwipeService.Data;
using SwipeService.Models;
using SwipeService.Services;
using Xunit;

namespace SwipeService.Tests;

/// <summary>
/// Tests for SwipeBehaviorAnalyzer (T185) covering:
/// - Trust score calculation (normal, high ratio, high velocity, high streak)
/// - Stats tracking (UpdateStatsOnSwipeAsync)
/// - Circuit breaker / cooldown (T189)
/// - Suspicious swipe detection (T186)
/// - Background recalculation (T190)
/// </summary>
public class SwipeBehaviorAnalyzerTests
{
    private static SwipeContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<SwipeContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new SwipeContext(options);
    }

    private static IOptionsMonitor<SwipeBehaviorConfiguration> CreateDefaultConfig()
    {
        var config = new SwipeBehaviorConfiguration();
        var mock = new Mock<IOptionsMonitor<SwipeBehaviorConfiguration>>();
        mock.Setup(m => m.CurrentValue).Returns(config);
        return mock.Object;
    }

    private static SwipeBehaviorAnalyzer CreateAnalyzer(
        SwipeContext context, SwipeBehaviorConfiguration? config = null)
    {
        var cfg = config ?? new SwipeBehaviorConfiguration();
        var mockConfig = new Mock<IOptionsMonitor<SwipeBehaviorConfiguration>>();
        mockConfig.Setup(m => m.CurrentValue).Returns(cfg);
        var logger = new Mock<ILogger<SwipeBehaviorAnalyzer>>();
        return new SwipeBehaviorAnalyzer(context, mockConfig.Object, logger.Object);
    }

    [Fact]
    public async Task GetOrCreateStatsAsync_CreatesNewStats_ForNewUser()
    {
        using var context = CreateInMemoryContext();
        var analyzer = CreateAnalyzer(context);

        var stats = await analyzer.GetOrCreateStatsAsync(42);

        Assert.NotNull(stats);
        Assert.Equal(42, stats.UserId);
        Assert.Equal(100m, stats.SwipeTrustScore);
        Assert.Equal(0, stats.TotalSwipes);
    }

    [Fact]
    public async Task GetOrCreateStatsAsync_ReturnsExistingStats_ForKnownUser()
    {
        using var context = CreateInMemoryContext();
        context.SwipeBehaviorStats.Add(new SwipeBehaviorStats
        {
            UserId = 42,
            SwipeTrustScore = 75m,
            TotalSwipes = 50,
            LastCalculatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var analyzer = CreateAnalyzer(context);
        var stats = await analyzer.GetOrCreateStatsAsync(42);

        Assert.Equal(75m, stats.SwipeTrustScore);
        Assert.Equal(50, stats.TotalSwipes);
    }

    [Fact]
    public async Task CalculateSwipeTrustScoreAsync_Returns100_ForNormalUser()
    {
        using var context = CreateInMemoryContext();
        context.SwipeBehaviorStats.Add(new SwipeBehaviorStats
        {
            UserId = 1,
            TotalSwipes = 50,
            TotalLikes = 25,
            TotalPasses = 25,
            RightSwipeRatio = 0.5m,
            AvgSwipeVelocity = 5m,
            PeakSwipeStreak = 5,
            SwipeTrustScore = 100m,
            LastCalculatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var analyzer = CreateAnalyzer(context);
        var score = await analyzer.CalculateSwipeTrustScoreAsync(1);

        Assert.Equal(100m, score);
    }

    [Fact]
    public async Task CalculateSwipeTrustScoreAsync_PenalizesHighRatio()
    {
        using var context = CreateInMemoryContext();
        context.SwipeBehaviorStats.Add(new SwipeBehaviorStats
        {
            UserId = 1,
            TotalSwipes = 100,
            TotalLikes = 95,
            TotalPasses = 5,
            RightSwipeRatio = 0.95m, // Way above 0.7 threshold
            AvgSwipeVelocity = 5m,
            PeakSwipeStreak = 5,
            SwipeTrustScore = 100m,
            LastCalculatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var analyzer = CreateAnalyzer(context);
        var score = await analyzer.CalculateSwipeTrustScoreAsync(1);

        Assert.True(score < 100m, $"Score should be penalized for high ratio, got {score}");
        Assert.True(score >= 70m, $"Score shouldn't be extremely low for ratio alone, got {score}");
    }

    [Fact]
    public async Task CalculateSwipeTrustScoreAsync_PenalizesHighVelocity()
    {
        using var context = CreateInMemoryContext();
        context.SwipeBehaviorStats.Add(new SwipeBehaviorStats
        {
            UserId = 1,
            TotalSwipes = 100,
            TotalLikes = 50,
            TotalPasses = 50,
            RightSwipeRatio = 0.5m,
            AvgSwipeVelocity = 20m, // Way above 10 threshold
            PeakSwipeStreak = 5,
            SwipeTrustScore = 100m,
            LastCalculatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var analyzer = CreateAnalyzer(context);
        var score = await analyzer.CalculateSwipeTrustScoreAsync(1);

        Assert.True(score < 100m, $"Score should be penalized for high velocity, got {score}");
    }

    [Fact]
    public async Task CalculateSwipeTrustScoreAsync_PenalizesLongStreak()
    {
        using var context = CreateInMemoryContext();
        context.SwipeBehaviorStats.Add(new SwipeBehaviorStats
        {
            UserId = 1,
            TotalSwipes = 100,
            TotalLikes = 50,
            TotalPasses = 50,
            RightSwipeRatio = 0.5m,
            AvgSwipeVelocity = 5m,
            PeakSwipeStreak = 40, // Way above 20 threshold
            SwipeTrustScore = 100m,
            LastCalculatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var analyzer = CreateAnalyzer(context);
        var score = await analyzer.CalculateSwipeTrustScoreAsync(1);

        Assert.True(score < 100m, $"Score should be penalized for long streak, got {score}");
    }

    [Fact]
    public async Task CalculateSwipeTrustScoreAsync_NeverBelowZero()
    {
        using var context = CreateInMemoryContext();
        context.SwipeBehaviorStats.Add(new SwipeBehaviorStats
        {
            UserId = 1,
            TotalSwipes = 1000,
            TotalLikes = 999,
            TotalPasses = 1,
            RightSwipeRatio = 0.999m,
            AvgSwipeVelocity = 60m,
            PeakSwipeStreak = 200,
            SwipeTrustScore = 100m,
            LastCalculatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var analyzer = CreateAnalyzer(context);
        var score = await analyzer.CalculateSwipeTrustScoreAsync(1);

        Assert.True(score >= 0m, "Score must never be negative");
        Assert.True(score <= 100m, "Score must never exceed 100");
    }

    [Fact]
    public async Task UpdateStatsOnSwipeAsync_IncrementsLikesAndStreak()
    {
        using var context = CreateInMemoryContext();
        var analyzer = CreateAnalyzer(context);

        // First swipe (creates stats)
        await analyzer.UpdateStatsOnSwipeAsync(1, isLike: true);
        var stats = await context.SwipeBehaviorStats.FirstAsync(s => s.UserId == 1);
        Assert.Equal(1, stats.TotalSwipes);
        Assert.Equal(1, stats.TotalLikes);
        Assert.Equal(0, stats.TotalPasses);
        Assert.Equal(1, stats.CurrentConsecutiveLikes);

        // Second like
        await analyzer.UpdateStatsOnSwipeAsync(1, isLike: true);
        stats = await context.SwipeBehaviorStats.FirstAsync(s => s.UserId == 1);
        Assert.Equal(2, stats.TotalSwipes);
        Assert.Equal(2, stats.TotalLikes);
        Assert.Equal(2, stats.CurrentConsecutiveLikes);
        Assert.Equal(2, stats.PeakSwipeStreak);
    }

    [Fact]
    public async Task UpdateStatsOnSwipeAsync_ResetsStreakOnPass()
    {
        using var context = CreateInMemoryContext();
        var analyzer = CreateAnalyzer(context);

        await analyzer.UpdateStatsOnSwipeAsync(1, isLike: true);
        await analyzer.UpdateStatsOnSwipeAsync(1, isLike: true);
        await analyzer.UpdateStatsOnSwipeAsync(1, isLike: true);

        // Pass breaks the streak
        await analyzer.UpdateStatsOnSwipeAsync(1, isLike: false);
        var stats = await context.SwipeBehaviorStats.FirstAsync(s => s.UserId == 1);
        Assert.Equal(0, stats.CurrentConsecutiveLikes);
        Assert.Equal(3, stats.PeakSwipeStreak); // Peak preserved
        Assert.Equal(1, stats.TotalPasses);
    }

    [Fact]
    public async Task UpdateStatsOnSwipeAsync_UpdatesRightSwipeRatio()
    {
        using var context = CreateInMemoryContext();
        var analyzer = CreateAnalyzer(context);

        // 3 likes, 1 pass = 75% ratio
        await analyzer.UpdateStatsOnSwipeAsync(1, isLike: true);
        await analyzer.UpdateStatsOnSwipeAsync(1, isLike: true);
        await analyzer.UpdateStatsOnSwipeAsync(1, isLike: true);
        await analyzer.UpdateStatsOnSwipeAsync(1, isLike: false);

        var stats = await context.SwipeBehaviorStats.FirstAsync(s => s.UserId == 1);
        Assert.Equal(0.75m, stats.RightSwipeRatio);
    }

    [Fact]
    public async Task CheckCooldownAsync_ReturnsFalse_WhenNoCooldown()
    {
        using var context = CreateInMemoryContext();
        var analyzer = CreateAnalyzer(context);

        var (inCooldown, until) = await analyzer.CheckCooldownAsync(99);
        Assert.False(inCooldown);
        Assert.Null(until);
    }

    [Fact]
    public async Task CheckCooldownAsync_ReturnsTrue_WhenInCooldown()
    {
        using var context = CreateInMemoryContext();
        context.SwipeBehaviorStats.Add(new SwipeBehaviorStats
        {
            UserId = 1,
            SwipeTrustScore = 100m,
            LastCalculatedAt = DateTime.UtcNow,
            CooldownUntil = DateTime.UtcNow.AddMinutes(10)
        });
        await context.SaveChangesAsync();

        var analyzer = CreateAnalyzer(context);
        var (inCooldown, until) = await analyzer.CheckCooldownAsync(1);
        Assert.True(inCooldown);
        Assert.NotNull(until);
    }

    [Fact]
    public async Task IsSwipeSuspiciousAsync_TriggersCircuitBreaker_OnConsecutiveLikes()
    {
        using var context = CreateInMemoryContext();
        var config = new SwipeBehaviorConfiguration { ConsecutiveLikeLimit = 5, CooldownMinutes = 10 };

        context.SwipeBehaviorStats.Add(new SwipeBehaviorStats
        {
            UserId = 1,
            SwipeTrustScore = 100m,
            LastCalculatedAt = DateTime.UtcNow,
            CurrentConsecutiveLikes = 5, // At the limit
            LastSwipeAt = DateTime.UtcNow.AddMinutes(-1) // Not rapid
        });
        await context.SaveChangesAsync();

        var analyzer = CreateAnalyzer(context, config);
        var (isSuspicious, reason) = await analyzer.IsSwipeSuspiciousAsync(1, isLike: true);

        Assert.True(isSuspicious);
        Assert.Contains("Consecutive like limit", reason);

        // Verify cooldown was set
        var stats = await context.SwipeBehaviorStats.FirstAsync(s => s.UserId == 1);
        Assert.NotNull(stats.CooldownUntil);
    }

    [Fact]
    public async Task IsSwipeSuspiciousAsync_ReturnsFalse_ForNormalSwipe()
    {
        using var context = CreateInMemoryContext();
        context.SwipeBehaviorStats.Add(new SwipeBehaviorStats
        {
            UserId = 1,
            SwipeTrustScore = 100m,
            LastCalculatedAt = DateTime.UtcNow,
            CurrentConsecutiveLikes = 2,
            LastSwipeAt = DateTime.UtcNow.AddSeconds(-10)
        });
        await context.SaveChangesAsync();

        var analyzer = CreateAnalyzer(context);
        var (isSuspicious, _) = await analyzer.IsSwipeSuspiciousAsync(1, isLike: true);

        Assert.False(isSuspicious);
    }

    [Fact]
    public async Task AnalyzeSwipePatternAsync_ReturnsReport_WithWarnings()
    {
        using var context = CreateInMemoryContext();
        context.SwipeBehaviorStats.Add(new SwipeBehaviorStats
        {
            UserId = 1,
            TotalSwipes = 100,
            TotalLikes = 95,
            TotalPasses = 5,
            RightSwipeRatio = 0.95m,
            AvgSwipeVelocity = 15m,
            PeakSwipeStreak = 30,
            RapidSwipeCount = 25,
            SwipeTrustScore = 50m,
            LastCalculatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var analyzer = CreateAnalyzer(context);
        var report = await analyzer.AnalyzeSwipePatternAsync(1);

        Assert.Equal(1, report.UserId);
        Assert.Equal(50m, report.TrustScore);
        Assert.Equal(100, report.TotalSwipes);
        Assert.NotEmpty(report.Warnings);
        Assert.True(report.Warnings.Count >= 3, "Should have warnings for ratio, velocity, streak, and rapid swiping");
    }

    [Fact]
    public async Task RecalculateAllAsync_RecalculatesActiveUsers()
    {
        using var context = CreateInMemoryContext();

        // Add a user with swipe history
        for (int i = 0; i < 15; i++)
        {
            context.Swipes.Add(new Swipe
            {
                UserId = 1,
                TargetUserId = i + 100,
                IsLike = true,
                CreatedAt = DateTime.UtcNow.AddHours(-i)
            });
        }

        context.SwipeBehaviorStats.Add(new SwipeBehaviorStats
        {
            UserId = 1,
            SwipeTrustScore = 100m,
            TotalSwipes = 10, // Will be updated by recalc
            LastCalculatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var analyzer = CreateAnalyzer(context);
        var count = await analyzer.RecalculateAllAsync(CancellationToken.None);

        Assert.True(count >= 1, "Should have recalculated at least 1 user");

        var stats = await context.SwipeBehaviorStats.FirstAsync(s => s.UserId == 1);
        Assert.Equal(15, stats.TotalSwipes); // Updated from DB
        Assert.Equal(15, stats.TotalLikes);
        Assert.Equal(0, stats.TotalPasses);
    }
}
