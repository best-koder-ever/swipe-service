using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwipeService.DTOs;
using SwipeService.Data;
using Microsoft.EntityFrameworkCore;

namespace SwipeService.Controllers;

[ApiController]
[Route("api/swipes/analytics")]
[Authorize]
public class SwipeAnalyticsController : ControllerBase
{
    private readonly SwipeDbContext _db;

    public SwipeAnalyticsController(SwipeDbContext db)
    {
        _db = db;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetAnalytics(string userId)
    {
        var swipes = await _db.Swipes
            .Where(s => s.SwiperId == userId)
            .ToListAsync();

        var total = swipes.Count;
        var likes = swipes.Count(s => s.IsLike);
        var likeRatio = total > 0 ? (double)likes / total : 0;
        var peakHour = swipes.Any()
            ? swipes.GroupBy(s => s.CreatedAt.Hour)
                    .OrderByDescending(g => g.Count())
                    .First().Key
            : 0;

        var dto = new SwipeAnalyticsDto
        {
            UserId = userId,
            TotalSwipes = total,
            LikeRatio = Math.Round(likeRatio, 2),
            PeakHour = peakHour,
            StreakDays = 0
        };

        return Ok(dto);
    }
}
