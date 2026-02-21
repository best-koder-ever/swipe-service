using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SwipeService.Controllers;
using SwipeService.Data;
using SwipeService.Models;
using Match = SwipeService.Models.Match;

namespace SwipeService.Tests.Controllers;

/// <summary>
/// Tests for MatchCheckController — match existence checks by Keycloak user IDs.
/// Used by messaging-service to verify users can chat.
/// </summary>
public class MatchCheckControllerTests : IDisposable
{
    private readonly SwipeContext _context;
    private readonly Mock<ILogger<MatchCheckController>> _mockLogger;
    private readonly MatchCheckController _controller;

    public MatchCheckControllerTests()
    {
        var options = new DbContextOptionsBuilder<SwipeContext>()
            .UseInMemoryDatabase(databaseName: $"MatchCheck_{Guid.NewGuid()}")
            .Options;

        _context = new SwipeContext(options);
        _mockLogger = new Mock<ILogger<MatchCheckController>>();
        _controller = new MatchCheckController(_context, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task SeedMapping(string userId, int profileId)
    {
        _context.UserProfileMappings.Add(new UserProfileMapping
        {
            UserId = userId,
            ProfileId = profileId
        });
        await _context.SaveChangesAsync();
    }

    private async Task SeedMatch(int user1Id, int user2Id, bool isActive = true)
    {
        _context.Matches.Add(new Match
        {
            User1Id = user1Id,
            User2Id = user2Id,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UnmatchedAt = isActive ? null : DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task CheckMatch_ActiveMatch_ReturnsTrue()
    {
        await SeedMapping("user-a", 1);
        await SeedMapping("user-b", 2);
        await SeedMatch(1, 2, isActive: true);

        var result = await _controller.CheckMatch("user-a", "user-b");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var hasMatch = GetProperty<bool>(okResult.Value!, "hasMatch");
        Assert.True(hasMatch);
    }

    [Fact]
    public async Task CheckMatch_ReversedOrder_StillFindsMatch()
    {
        await SeedMapping("user-a", 1);
        await SeedMapping("user-b", 2);
        await SeedMatch(2, 1, isActive: true); // reversed

        var result = await _controller.CheckMatch("user-a", "user-b");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var hasMatch = GetProperty<bool>(okResult.Value!, "hasMatch");
        Assert.True(hasMatch);
    }

    [Fact]
    public async Task CheckMatch_InactiveMatch_ReturnsFalse()
    {
        await SeedMapping("user-a", 1);
        await SeedMapping("user-b", 2);
        await SeedMatch(1, 2, isActive: false);

        var result = await _controller.CheckMatch("user-a", "user-b");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var hasMatch = GetProperty<bool>(okResult.Value!, "hasMatch");
        Assert.False(hasMatch);
    }

    [Fact]
    public async Task CheckMatch_NoMapping_ReturnsFalseWithReason()
    {
        var result = await _controller.CheckMatch("unknown-user", "other-user");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var hasMatch = GetProperty<bool>(okResult.Value!, "hasMatch");
        Assert.False(hasMatch);
    }

    [Fact]
    public async Task CheckMatch_OneUserMissing_ReturnsFalse()
    {
        await SeedMapping("user-a", 1);
        // user-b has no mapping

        var result = await _controller.CheckMatch("user-a", "user-b");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var hasMatch = GetProperty<bool>(okResult.Value!, "hasMatch");
        Assert.False(hasMatch);
    }

    [Fact]
    public async Task CheckMatch_NoMatchBetweenUsers_ReturnsFalse()
    {
        await SeedMapping("user-a", 1);
        await SeedMapping("user-b", 2);
        // No match seeded

        var result = await _controller.CheckMatch("user-a", "user-b");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var hasMatch = GetProperty<bool>(okResult.Value!, "hasMatch");
        Assert.False(hasMatch);
    }

    private static T GetProperty<T>(object obj, string name)
    {
        var prop = obj.GetType().GetProperty(name);
        Assert.NotNull(prop);
        return (T)prop.GetValue(obj)!;
    }
}
