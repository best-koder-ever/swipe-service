using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SwipeService.Data;
using SwipeService.Models;
using SwipeService.Queries;

namespace SwipeService.Tests.Queries;

public class GetMatchesForUserHandlerTests : IDisposable
{
    private readonly SwipeContext _context;
    private readonly GetMatchesForUserHandler _handler;

    public GetMatchesForUserHandlerTests()
    {
        var options = new DbContextOptionsBuilder<SwipeContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new SwipeContext(options);
        _handler = new GetMatchesForUserHandler(_context, Mock.Of<ILogger<GetMatchesForUserHandler>>());
    }

    [Fact]
    public async Task NoMatches_ReturnsEmptyList()
    {
        var query = new GetMatchesForUserQuery { UserId = 1 };
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task ReturnsActiveMatchesOnly()
    {
        _context.Matches.AddRange(
            new SwipeService.Models.Match { User1Id = 1, User2Id = 2, IsActive = true, CreatedAt = DateTime.UtcNow },
            new SwipeService.Models.Match { User1Id = 1, User2Id = 3, IsActive = false, CreatedAt = DateTime.UtcNow } // inactive
        );
        await _context.SaveChangesAsync();

        var query = new GetMatchesForUserQuery { UserId = 1 };
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(2, result.Value[0].MatchedUserId);
    }

    [Fact]
    public async Task MatchedUserId_ResolvesToOtherUser_WhenUser1()
    {
        _context.Matches.Add(new SwipeService.Models.Match { User1Id = 1, User2Id = 5, IsActive = true });
        await _context.SaveChangesAsync();

        var query = new GetMatchesForUserQuery { UserId = 1 };
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(5, result.Value![0].MatchedUserId);
    }

    [Fact]
    public async Task MatchedUserId_ResolvesToOtherUser_WhenUser2()
    {
        _context.Matches.Add(new SwipeService.Models.Match { User1Id = 1, User2Id = 5, IsActive = true });
        await _context.SaveChangesAsync();

        var query = new GetMatchesForUserQuery { UserId = 5 };
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(1, result.Value![0].MatchedUserId);
    }

    [Fact]
    public async Task OrderedByDate_Descending()
    {
        _context.Matches.AddRange(
            new SwipeService.Models.Match { User1Id = 1, User2Id = 2, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new SwipeService.Models.Match { User1Id = 1, User2Id = 3, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new SwipeService.Models.Match { User1Id = 1, User2Id = 4, IsActive = true, CreatedAt = DateTime.UtcNow }
        );
        await _context.SaveChangesAsync();

        var query = new GetMatchesForUserQuery { UserId = 1 };
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(3, result.Value!.Count);
        Assert.Equal(4, result.Value[0].MatchedUserId); // most recent
        Assert.Equal(3, result.Value[1].MatchedUserId);
        Assert.Equal(2, result.Value[2].MatchedUserId); // oldest
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
