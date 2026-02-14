using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SwipeService.Commands;
using SwipeService.Data;
using SwipeService.Models;
using SwipeService.Services;

namespace SwipeService.Tests.Commands;

public class RecordSwipeHandlerTests : IDisposable
{
    private readonly SwipeContext _context;
    private readonly Mock<MatchmakingNotifier> _notifierMock;
    private readonly Mock<ILogger<RecordSwipeHandler>> _loggerMock;
    private readonly Mock<IRateLimitService> _rateLimitMock;
    private readonly Mock<ISwipeBehaviorAnalyzer> _behaviorMock;

    public RecordSwipeHandlerTests()
    {
        var options = new DbContextOptionsBuilder<SwipeContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new SwipeContext(options);
        _notifierMock = new Mock<MatchmakingNotifier>(new HttpClient()) { CallBase = false };
        _loggerMock = new Mock<ILogger<RecordSwipeHandler>>();
        _rateLimitMock = new Mock<IRateLimitService>();
        _behaviorMock = new Mock<ISwipeBehaviorAnalyzer>();

        // Default: allow everything
        _rateLimitMock
            .Setup(x => x.CheckDailyLimitAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync((true, 50, 100));
        _behaviorMock
            .Setup(x => x.CheckCooldownAsync(It.IsAny<int>()))
            .ReturnsAsync((false, (DateTime?)null));
        _behaviorMock
            .Setup(x => x.IsSwipeSuspiciousAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync((false, (string?)null));
    }

    private RecordSwipeHandler CreateHandler(
        IRateLimitService? rateLimitService = null,
        ISwipeBehaviorAnalyzer? behaviorAnalyzer = null)
    {
        return new RecordSwipeHandler(
            _context,
            _notifierMock.Object,
            _loggerMock.Object,
            rateLimitService,
            behaviorAnalyzer);
    }

    [Fact]
    public async Task SelfSwipe_ReturnsFailure()
    {
        var handler = CreateHandler();
        var cmd = new RecordSwipeCommand { UserId = 1, TargetUserId = 1, IsLike = true };

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Cannot swipe on yourself", result.Error!);
    }

    [Fact]
    public async Task CircuitBreakerCooldown_BlocksSwipe()
    {
        var cooldownTime = DateTime.UtcNow.AddMinutes(10);
        _behaviorMock
            .Setup(x => x.CheckCooldownAsync(1))
            .ReturnsAsync((true, cooldownTime));

        var handler = CreateHandler(behaviorAnalyzer: _behaviorMock.Object);
        var cmd = new RecordSwipeCommand { UserId = 1, TargetUserId = 2, IsLike = true };

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Too many consecutive likes", result.Error!);
    }

    [Fact]
    public async Task RateLimitExceeded_Like_ReturnsFailure()
    {
        _rateLimitMock
            .Setup(x => x.CheckDailyLimitAsync(1, true))
            .ReturnsAsync((false, 0, 50));

        var handler = CreateHandler(rateLimitService: _rateLimitMock.Object);
        var cmd = new RecordSwipeCommand { UserId = 1, TargetUserId = 2, IsLike = true };

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("like", result.Error!);
        Assert.Contains("limit reached", result.Error!);
    }

    [Fact]
    public async Task RateLimitExceeded_Pass_ReturnsSwipeInMessage()
    {
        _rateLimitMock
            .Setup(x => x.CheckDailyLimitAsync(1, false))
            .ReturnsAsync((false, 0, 100));

        var handler = CreateHandler(rateLimitService: _rateLimitMock.Object);
        var cmd = new RecordSwipeCommand { UserId = 1, TargetUserId = 2, IsLike = false };

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("swipe", result.Error!);
    }

    [Fact]
    public async Task SuspiciousSwipe_StillProcesses_ShadowLog()
    {
        _behaviorMock
            .Setup(x => x.IsSwipeSuspiciousAsync(1, true))
            .ReturnsAsync((true, "rapid swiping"));

        var handler = CreateHandler(
            rateLimitService: _rateLimitMock.Object,
            behaviorAnalyzer: _behaviorMock.Object);
        var cmd = new RecordSwipeCommand { UserId = 1, TargetUserId = 2, IsLike = true };

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Success);
    }

    [Fact]
    public async Task IdempotencyKey_ReturnsPreviousResult()
    {
        // Seed an existing swipe with an idempotency key
        _context.Swipes.Add(new Swipe
        {
            UserId = 1,
            TargetUserId = 2,
            IsLike = true,
            IdempotencyKey = "key-123",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var handler = CreateHandler();
        var cmd = new RecordSwipeCommand
        {
            UserId = 1,
            TargetUserId = 2,
            IsLike = true,
            IdempotencyKey = "key-123"
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("idempotent", result.Value!.Message);
        // Should NOT create a second swipe
        Assert.Equal(1, await _context.Swipes.CountAsync());
    }

    [Fact]
    public async Task IdempotencyKey_WithMatch_ReturnsMutualMatch()
    {
        // Seed a swipe+match with the idempotency key
        var match = new SwipeService.Models.Match { User1Id = 1, User2Id = 2, CreatedAt = DateTime.UtcNow };
        _context.Matches.Add(match);
        _context.Swipes.Add(new Swipe
        {
            UserId = 1,
            TargetUserId = 2,
            IsLike = true,
            IdempotencyKey = "key-match",
            Match = match,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var handler = CreateHandler();
        var cmd = new RecordSwipeCommand
        {
            UserId = 1,
            TargetUserId = 2,
            IsLike = true,
            IdempotencyKey = "key-match"
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsMutualMatch);
    }

    [Fact]
    public async Task DuplicateSwipe_ReturnsFailure()
    {
        _context.Swipes.Add(new Swipe
        {
            UserId = 1,
            TargetUserId = 2,
            IsLike = true,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var handler = CreateHandler();
        var cmd = new RecordSwipeCommand { UserId = 1, TargetUserId = 2, IsLike = true };

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Already swiped", result.Error!);
    }

    [Fact]
    public async Task HappyPath_Pass_RecordedSuccessfully()
    {
        var handler = CreateHandler();
        var cmd = new RecordSwipeCommand { UserId = 1, TargetUserId = 2, IsLike = false };

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Success);
        Assert.False(result.Value.IsMutualMatch);
        Assert.Equal(0, result.Value.MatchId);

        // Verify DB
        var swipe = await _context.Swipes.SingleAsync();
        Assert.Equal(1, swipe.UserId);
        Assert.Equal(2, swipe.TargetUserId);
        Assert.False(swipe.IsLike);
    }

    [Fact]
    public async Task HappyPath_Like_NoMutual_NoMatch()
    {
        var handler = CreateHandler();
        var cmd = new RecordSwipeCommand { UserId = 1, TargetUserId = 2, IsLike = true };

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsMutualMatch);
        Assert.Empty(await _context.Matches.ToListAsync());
    }

    [Fact]
    public async Task MutualLike_CreatesMatch()
    {
        // User 2 already liked user 1
        _context.Swipes.Add(new Swipe
        {
            UserId = 2,
            TargetUserId = 1,
            IsLike = true,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _notifierMock
            .Setup(x => x.NotifyMatchmakingServiceAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var cmd = new RecordSwipeCommand { UserId = 1, TargetUserId = 2, IsLike = true };

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsMutualMatch);
        Assert.Contains("match", result.Value.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Value.MatchId > 0);

        // Verify match ordering: User1Id < User2Id
        var match = await _context.Matches.SingleAsync();
        Assert.Equal(1, match.User1Id);
        Assert.Equal(2, match.User2Id);
    }

    [Fact]
    public async Task MutualLike_NotifiesMatchmakingService()
    {
        _context.Swipes.Add(new Swipe
        {
            UserId = 2,
            TargetUserId = 1,
            IsLike = true,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _notifierMock
            .Setup(x => x.NotifyMatchmakingServiceAsync(1, 2))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var cmd = new RecordSwipeCommand { UserId = 1, TargetUserId = 2, IsLike = true };

        await handler.Handle(cmd, CancellationToken.None);

        _notifierMock.Verify(x => x.NotifyMatchmakingServiceAsync(1, 2), Times.Once);
    }

    [Fact]
    public async Task MutualLike_ReversedUserOrder_MatchStillOrdersCorrectly()
    {
        // User 5 already liked user 3
        _context.Swipes.Add(new Swipe
        {
            UserId = 5,
            TargetUserId = 3,
            IsLike = true,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _notifierMock
            .Setup(x => x.NotifyMatchmakingServiceAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var cmd = new RecordSwipeCommand { UserId = 3, TargetUserId = 5, IsLike = true };

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.Value!.IsMutualMatch);
        var match = await _context.Matches.SingleAsync();
        Assert.Equal(3, match.User1Id); // Min
        Assert.Equal(5, match.User2Id); // Max
    }

    [Fact]
    public async Task RateLimitService_IncrementedOnSuccess()
    {
        var handler = CreateHandler(rateLimitService: _rateLimitMock.Object);
        var cmd = new RecordSwipeCommand { UserId = 1, TargetUserId = 2, IsLike = true };

        await handler.Handle(cmd, CancellationToken.None);

        _rateLimitMock.Verify(x => x.IncrementSwipeCountAsync(1), Times.Once);
    }

    [Fact]
    public async Task BehaviorStats_UpdatedOnSuccess()
    {
        var handler = CreateHandler(
            rateLimitService: _rateLimitMock.Object,
            behaviorAnalyzer: _behaviorMock.Object);
        var cmd = new RecordSwipeCommand { UserId = 1, TargetUserId = 2, IsLike = true };

        await handler.Handle(cmd, CancellationToken.None);

        _behaviorMock.Verify(x => x.UpdateStatsOnSwipeAsync(1, true), Times.Once);
    }

    [Fact]
    public async Task BehaviorStatsFailure_NonFatal_SwipeStillSucceeds()
    {
        _behaviorMock
            .Setup(x => x.UpdateStatsOnSwipeAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("DB error"));

        var handler = CreateHandler(
            rateLimitService: _rateLimitMock.Object,
            behaviorAnalyzer: _behaviorMock.Object);
        var cmd = new RecordSwipeCommand { UserId = 1, TargetUserId = 2, IsLike = true };

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Success);
    }

    [Fact]
    public async Task NullOptionalServices_WorksFine()
    {
        // No rate limit service, no behavior analyzer
        var handler = CreateHandler(null, null);
        var cmd = new RecordSwipeCommand { UserId = 1, TargetUserId = 2, IsLike = true };

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
