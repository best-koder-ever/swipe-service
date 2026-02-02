using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwipeService.Data;

namespace SwipeService.Controllers;

[ApiController]
[Route("api/matches")]
public class MatchCheckController : ControllerBase
{
    private readonly SwipeContext _context;
    private readonly ILogger<MatchCheckController> _logger;

    public MatchCheckController(SwipeContext context, ILogger<MatchCheckController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Check if two users have an active match (by Keycloak user IDs)
    /// </summary>
    [HttpGet("check/{userId1}/{userId2}")]
    public async Task<IActionResult> CheckMatch(string userId1, string userId2)
    {
        try
        {
            // Get profile IDs from local mapping table (works in both demo and prod modes)
            var profile1 = await _context.UserProfileMappings
                .Where(m => m.UserId == userId1)
                .Select(m => m.ProfileId)
                .FirstOrDefaultAsync();

            var profile2 = await _context.UserProfileMappings
                .Where(m => m.UserId == userId2)
                .Select(m => m.ProfileId)
                .FirstOrDefaultAsync();

            if (profile1 == 0 || profile2 == 0)
            {
                _logger.LogWarning("Profile ID not found for userId1={User1}, userId2={User2} - denying match", userId1, userId2);
                // Security: Require both users to have profiles
                return Ok(new { hasMatch = false, reason = "One or both profiles not found" });
            }

            // Check if there's an active match between these profiles
            var hasMatch = await _context.Matches
                .AnyAsync(m => 
                    ((m.User1Id == profile1 && m.User2Id == profile2) ||
                     (m.User1Id == profile2 && m.User2Id == profile1)) &&
                    m.IsActive &&
                    m.UnmatchedAt == null);

            return Ok(new { hasMatch });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking match between {User1} and {User2}", userId1, userId2);
            // Security: Fail secure - deny on error
            return Ok(new { hasMatch = false, reason = "Error checking match status" });
        }
    }
}
