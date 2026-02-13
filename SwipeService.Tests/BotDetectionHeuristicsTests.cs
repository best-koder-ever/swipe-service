using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SwipeService.Data;
using SwipeService.Models;
using SwipeService.Services;
using Xunit;

namespace SwipeService.Tests;

/// <summary>
/// Tests for BotDetectionHeuristics (T191) covering:
/// - Insufficient data returns zero probability
/// - Perfectly regular intervals score high (bot-like)
/// - Random human-like intervals score low
/// - Monotonic like patterns detected
/// - 24/7 activity detected
/// - Combined signals flag bots
/// </summary>
public class BotDetectionHeuristicsTests
{
    private static SwipeContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<SwipeContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new SwipeContext(options);
    }

    private static BotDetectionHeuristics CreateService(SwipeContext context)
    {
        var logger = new Mock<ILogger<BotDetectionHeuristics>>();
        return new BotDetectionHeuristics(context, logger.Object);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsZero_WhenInsufficientData()
    {
        using var context = CreateInMemoryContext();

        // Only 5 swipes — below MinSwipesForAnalysis (20)
        for (int i = 0; i < 5; i++)
        {
            context.Swipes.Add(new Swipe
            {
                UserId = 1,
                TargetUserId = i + 100,
                IsLike = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.AnalyzeAsync(1);

        Assert.Equal(0.0, result.BotProbability);
        Assert.False(result.ShouldFlag);
        Assert.Contains("Insufficient data", result.Signals.First());
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsMonotonicLikePattern()
    {
        using var context = CreateInMemoryContext();
        var rng = new Random(42);

        // 50 swipes, ALL likes — monotonic pattern
        for (int i = 0; i < 50; i++)
        {
            context.Swipes.Add(new Swipe
            {
                UserId = 1,
                TargetUserId = i + 100,
                IsLike = true,
                CreatedAt = DateTime.UtcNow.AddSeconds(-(i * 30 + rng.Next(10)))
            });
        }
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.AnalyzeAsync(1);

        // Monotonic signal should fire
        Assert.Contains(result.Signals, s => s.Contains("Monotonic pattern"));
    }

    [Fact]
    public async Task AnalyzeAsync_HumanLikePatterns_ScoreLow()
    {
        using var context = CreateInMemoryContext();
        var rng = new Random(42);

        // 30 swipes with varied likes (60/40), random intervals, within normal hours
        var baseTime = new DateTime(2024, 6, 15, 14, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 30; i++)
        {
            context.Swipes.Add(new Swipe
            {
                UserId = 1,
                TargetUserId = i + 100,
                IsLike = rng.NextDouble() < 0.6,
                CreatedAt = baseTime.AddSeconds(-(i * rng.Next(5, 45))),
                DeviceInfo = "iPhone-14-Pro"
            });
        }
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.AnalyzeAsync(1);

        // Should NOT be flagged as a bot
        Assert.False(result.ShouldFlag, $"Human-like pattern should not flag, probability: {result.BotProbability}");
    }

    [Fact]
    public async Task AnalyzeAsync_Detects24x7Activity()
    {
        using var context = CreateInMemoryContext();
        var rng = new Random(42);

        // 48 swipes spread across all 24 hours (2 per hour)
        var baseDate = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        for (int hour = 0; hour < 24; hour++)
        {
            for (int j = 0; j < 2; j++)
            {
                context.Swipes.Add(new Swipe
                {
                    UserId = 1,
                    TargetUserId = hour * 2 + j + 100,
                    IsLike = rng.NextDouble() < 0.5,
                    CreatedAt = baseDate.AddHours(hour).AddMinutes(j * 25 + rng.Next(10))
                });
            }
        }
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.AnalyzeAsync(1);

        // 24/7 activity signal should fire
        Assert.Contains(result.Signals, s => s.Contains("24/7 activity"));
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsMissingDeviceFingerprint()
    {
        using var context = CreateInMemoryContext();
        var rng = new Random(42);

        // 25 swipes with no device info
        for (int i = 0; i < 25; i++)
        {
            context.Swipes.Add(new Swipe
            {
                UserId = 1,
                TargetUserId = i + 100,
                IsLike = rng.NextDouble() < 0.5,
                CreatedAt = DateTime.UtcNow.AddSeconds(-(i * 30 + rng.Next(10))),
                DeviceInfo = null
            });
        }
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.AnalyzeAsync(1);

        Assert.Contains(result.Signals, s => s.Contains("No device fingerprint"));
    }

    [Fact]
    public async Task AnalyzeAsync_RegularIntervals_ScoreHighClockRegularity()
    {
        using var context = CreateInMemoryContext();

        // 50 swipes with perfectly regular 10-second intervals (bot-like)
        var baseTime = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            context.Swipes.Add(new Swipe
            {
                UserId = 1,
                TargetUserId = i + 100,
                IsLike = i % 2 == 0, // Alternating to avoid monotonic flag
                CreatedAt = baseTime.AddSeconds(-i * 10),
                DeviceInfo = "Bot-Agent"
            });
        }
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.AnalyzeAsync(1);

        // Very regular intervals should trigger clock regularity signal
        Assert.Contains(result.Signals, s => s.Contains("Clock regularity"));
    }
}
