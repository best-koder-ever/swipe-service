using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwipeService.Data;

namespace SwipeService.Controllers;

[ApiController]
[Route("api/swipes")]
public class SwipeDeletionController : ControllerBase
{
    private readonly SwipeContext _context;
    private readonly ILogger<SwipeDeletionController> _logger;

    public SwipeDeletionController(SwipeContext context, ILogger<SwipeDeletionController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Cascade delete all swipes for a user (called during account deletion)
    /// </summary>
    [HttpDelete("user/{userProfileId:int}")]
    [AllowAnonymous] // Service-to-service call from UserService
    public async Task<IActionResult> DeleteUserSwipes(int userProfileId)
    {
        try
        {
            var swipes = await _context.Swipes
                .Where(s => s.UserId == userProfileId || s.TargetUserId == userProfileId)
                .ToListAsync();

            var count = swipes.Count;

            if (count > 0)
            {
                _context.Swipes.RemoveRange(swipes);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted {Count} swipes for user {UserId}", count, userProfileId);
            }

            return Ok(count.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting swipes for user {UserId}", userProfileId);
            return StatusCode(500, "0"); // Return 0 count on error, allow cascade to continue
        }
    }
}
