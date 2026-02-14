using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SwipeService.Data;
using SwipeService.Models;
using SwipeService.Queries;

namespace SwipeService.Tests.Queries;

public class GetSwipesByUserHandlerTests : IDisposable
{
    private readonly SwipeContext _context;
    private readonly GetSwipesByUserHandler _handler;

    public GetSwipesByUserHandlerTests()
    {
        var options = new DbContextOptionsBuilder<SwipeContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new SwipeContext(options);
        _handler = new GetSwipesByUserHandler(_context, Mock.Of<ILogger<GetSwipesByUserHandler>>());
    }

    private async Task SeedSwipes()
    {
        _context.Swipes.AddRange(
            new Swipe { UserId = 1, TargetUserId = 10, IsLike = true, CreatedAt = DateTime.UtcNow.AddHours(-3) },
            new Swipe { UserId = 1, TargetUserId = 11, IsLike = true, CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new Swipe { UserId = 1, TargetUserId = 12, IsLike = false, CreatedAt = DateTime.UtcNow.AddHours(-1) },
            new Swipe { UserId = 1, TargetUserId = 13, IsLike = false, CreatedAt = DateTime.UtcNow },
            new Swipe { UserId = 2, TargetUserId = 10, IsLike = true, CreatedAt = DateTime.UtcNow } // other user
        );
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task ReturnsSwipesForUser()
    {
        await SeedSwipes();
        var query = new GetSwipesByUserQuery { UserId = 1, Page = 1, PageSize = 50 };
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.TotalSwipes);
        Assert.Equal(4, result.Value.Swipes.Count);
    }

    [Fact]
    public async Task FilterByLikes()
    {
        await SeedSwipes();
        var query = new GetSwipesByUserQuery { UserId = 1, IsLike = true, Page = 1, PageSize = 50 };
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.Value!.Swipes.Count);
        Assert.All(result.Value.Swipes, s => Assert.True(s.IsLike));
    }

    [Fact]
    public async Task FilterByPasses()
    {
        await SeedSwipes();
        var query = new GetSwipesByUserQuery { UserId = 1, IsLike = false, Page = 1, PageSize = 50 };
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.Value!.Swipes.Count);
        Assert.All(result.Value.Swipes, s => Assert.False(s.IsLike));
    }

    [Fact]
    public async Task TotalLikesAndPasses_ComputedCorrectly()
    {
        await SeedSwipes();
        var query = new GetSwipesByUserQuery { UserId = 1, Page = 1, PageSize = 50 };
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.Value!.TotalLikes);
        Assert.Equal(2, result.Value.TotalPasses);
    }

    [Fact]
    public async Task Pagination_Page1()
    {
        await SeedSwipes();
        var query = new GetSwipesByUserQuery { UserId = 1, Page = 1, PageSize = 2 };
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.Value!.Swipes.Count);
        Assert.Equal(4, result.Value.TotalSwipes);
    }

    [Fact]
    public async Task Pagination_Page2()
    {
        await SeedSwipes();
        var query = new GetSwipesByUserQuery { UserId = 1, Page = 2, PageSize = 2 };
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.Value!.Swipes.Count);
    }

    [Fact]
    public async Task OrderedByDate_Descending()
    {
        await SeedSwipes();
        var query = new GetSwipesByUserQuery { UserId = 1, Page = 1, PageSize = 50 };
        var result = await _handler.Handle(query, CancellationToken.None);

        var dates = result.Value!.Swipes.Select(s => s.CreatedAt).ToList();
        Assert.Equal(dates.OrderByDescending(d => d), dates);
    }

    [Fact]
    public async Task NoSwipes_ReturnsEmptyHistory()
    {
        var query = new GetSwipesByUserQuery { UserId = 999, Page = 1, PageSize = 50 };
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Swipes);
        Assert.Equal(0, result.Value.TotalSwipes);
        Assert.Equal(0, result.Value.TotalLikes);
        Assert.Equal(0, result.Value.TotalPasses);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
