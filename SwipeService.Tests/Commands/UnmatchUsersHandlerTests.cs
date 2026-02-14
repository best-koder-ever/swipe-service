using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SwipeService.Commands;
using SwipeService.Data;
using SwipeService.Models;

namespace SwipeService.Tests.Commands;

public class UnmatchUsersHandlerTests : IDisposable
{
    private readonly SwipeContext _context;
    private readonly UnmatchUsersHandler _handler;

    public UnmatchUsersHandlerTests()
    {
        var options = new DbContextOptionsBuilder<SwipeContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new SwipeContext(options);
        _handler = new UnmatchUsersHandler(_context, Mock.Of<ILogger<UnmatchUsersHandler>>());
    }

    [Fact]
    public async Task HappyPath_SetsMatchInactive()
    {
        _context.Matches.Add(new SwipeService.Models.Match { User1Id = 1, User2Id = 2, IsActive = true });
        await _context.SaveChangesAsync();

        var cmd = new UnmatchUsersCommand { UserId = 1, TargetUserId = 2 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var match = await _context.Matches.SingleAsync();
        Assert.False(match.IsActive);
        Assert.NotNull(match.UnmatchedAt);
        Assert.Equal(1, match.UnmatchedByUserId);
    }

    [Fact]
    public async Task ReversedUserOrder_StillFindsMatch()
    {
        // Match stored as (1,2) — request comes as (2,1)
        _context.Matches.Add(new SwipeService.Models.Match { User1Id = 1, User2Id = 2, IsActive = true });
        await _context.SaveChangesAsync();

        var cmd = new UnmatchUsersCommand { UserId = 2, TargetUserId = 1 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var match = await _context.Matches.SingleAsync();
        Assert.False(match.IsActive);
        Assert.Equal(2, match.UnmatchedByUserId); // The requesting user
    }

    [Fact]
    public async Task NoActiveMatch_ReturnsFailure()
    {
        var cmd = new UnmatchUsersCommand { UserId = 1, TargetUserId = 2 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Match not found", result.Error!);
    }

    [Fact]
    public async Task AlreadyUnmatchedMatch_ReturnsFailure()
    {
        _context.Matches.Add(new SwipeService.Models.Match
        {
            User1Id = 1, User2Id = 2,
            IsActive = false,
            UnmatchedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _context.SaveChangesAsync();

        var cmd = new UnmatchUsersCommand { UserId = 1, TargetUserId = 2 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Match not found", result.Error!);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
