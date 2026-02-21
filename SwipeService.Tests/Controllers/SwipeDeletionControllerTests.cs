using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SwipeService.Controllers;
using SwipeService.Data;
using SwipeService.Models;

namespace SwipeService.Tests.Controllers;

/// <summary>
/// Tests for SwipeDeletionController — cascade delete of user swipes during account deletion.
/// Called by UserService as service-to-service during account cleanup.
/// </summary>
public class SwipeDeletionControllerTests : IDisposable
{
    private readonly SwipeContext _context;
    private readonly Mock<ILogger<SwipeDeletionController>> _mockLogger;
    private readonly SwipeDeletionController _controller;

    public SwipeDeletionControllerTests()
    {
        var options = new DbContextOptionsBuilder<SwipeContext>()
            .UseInMemoryDatabase(databaseName: $"SwipeDeletion_{Guid.NewGuid()}")
            .Options;

        _context = new SwipeContext(options);
        _mockLogger = new Mock<ILogger<SwipeDeletionController>>();
        _controller = new SwipeDeletionController(_context, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task SeedSwipe(int userId, int targetId)
    {
        _context.Swipes.Add(new Swipe
        {
            UserId = userId,
            TargetUserId = targetId,
            IsLike = true,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task DeleteUserSwipes_WithSwipes_RemovesAllAndReturnsCount()
    {
        // Arrange — 3 swipes by user 42 + 1 swipe targeting user 42
        await SeedSwipe(42, 10);
        await SeedSwipe(42, 11);
        await SeedSwipe(42, 12);
        await SeedSwipe(99, 42); // someone swiped on user 42

        // Act
        var result = await _controller.DeleteUserSwipes(42);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("4", okResult.Value);
        Assert.Equal(0, await _context.Swipes.CountAsync(s => s.UserId == 42 || s.TargetUserId == 42));
    }

    [Fact]
    public async Task DeleteUserSwipes_NoSwipes_ReturnsZero()
    {
        var result = await _controller.DeleteUserSwipes(999);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("0", okResult.Value);
    }

    [Fact]
    public async Task DeleteUserSwipes_OnlyDeletesTargetUser()
    {
        // Arrange — swipes for user 1 and user 2
        await SeedSwipe(1, 10);
        await SeedSwipe(2, 20);
        await SeedSwipe(2, 30);

        // Act — delete only user 1's swipes
        await _controller.DeleteUserSwipes(1);

        // Assert — user 2's swipes remain
        Assert.Equal(2, await _context.Swipes.CountAsync());
        Assert.True(await _context.Swipes.AllAsync(s => s.UserId == 2));
    }

    [Fact]
    public async Task DeleteUserSwipes_DeletesInboundAndOutbound()
    {
        // Arrange — user 5 swiped on 10, and 20 swiped on user 5
        await SeedSwipe(5, 10);  // outbound
        await SeedSwipe(20, 5);  // inbound

        // Act
        var result = await _controller.DeleteUserSwipes(5);

        // Assert — both removed
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("2", okResult.Value);
        Assert.Equal(0, await _context.Swipes.CountAsync());
    }
}
