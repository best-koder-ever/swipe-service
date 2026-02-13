using Microsoft.AspNetCore.Mvc;
using SwipeService.Common;
using SwipeService.Services;

namespace SwipeService.Controllers;

/// <summary>
/// T187: Internal API for swipe behavior / trust score queries.
/// Protected by InternalApiKeyAuthFilter for service-to-service calls only.
/// </summary>
[ApiController]
[Route("api/internal/swipe-behavior")]
[ServiceFilter(typeof(InternalApiKeyAuthFilter))]
public class SwipeBehaviorController : ControllerBase
{
    private readonly ISwipeBehaviorAnalyzer _analyzer;
    private readonly ILogger<SwipeBehaviorController> _logger;

    public SwipeBehaviorController(
        ISwipeBehaviorAnalyzer analyzer,
        ILogger<SwipeBehaviorController> logger)
    {
        _analyzer = analyzer;
        _logger = logger;
    }

    /// <summary>
    /// Get the swipe trust score for a user.
    /// Used by MatchmakingService for shadow-restricting low-trust users.
    /// </summary>
    [HttpGet("{userId:int}/trust-score")]
    public async Task<IActionResult> GetTrustScore(int userId)
    {
        try
        {
            var stats = await _analyzer.GetOrCreateStatsAsync(userId);
            return Ok(new
            {
                UserId = userId,
                TrustScore = stats.SwipeTrustScore,
                LastCalculatedAt = stats.LastCalculatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trust score for user {UserId}", userId);
            return StatusCode(500, new { Error = "Failed to fetch trust score" });
        }
    }

    /// <summary>
    /// Get full behavioral report for a user.
    /// Returns trust score, ratio, velocity, streak, flags, and warnings.
    /// </summary>
    [HttpGet("{userId:int}/report")]
    public async Task<IActionResult> GetBehaviorReport(int userId)
    {
        try
        {
            var report = await _analyzer.AnalyzeSwipePatternAsync(userId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating behavior report for user {UserId}", userId);
            return StatusCode(500, new { Error = "Failed to generate behavior report" });
        }
    }

    /// <summary>
    /// Force recalculate trust score for a specific user.
    /// </summary>
    [HttpPost("{userId:int}/recalculate")]
    public async Task<IActionResult> RecalculateTrustScore(int userId)
    {
        try
        {
            var newScore = await _analyzer.CalculateSwipeTrustScoreAsync(userId);
            return Ok(new { UserId = userId, TrustScore = newScore });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating trust score for user {UserId}", userId);
            return StatusCode(500, new { Error = "Failed to recalculate trust score" });
        }
    }

    /// <summary>
    /// Get trust scores for multiple users in a single batch call.
    /// Used by MatchmakingService when scoring candidate pools.
    /// </summary>
    [HttpPost("batch-trust-scores")]
    public async Task<IActionResult> GetBatchTrustScores([FromBody] BatchTrustScoreRequest request)
    {
        try
        {
            var results = new List<object>();
            foreach (var userId in request.UserIds)
            {
                var stats = await _analyzer.GetOrCreateStatsAsync(userId);
                results.Add(new
                {
                    UserId = userId,
                    TrustScore = stats.SwipeTrustScore
                });
            }
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching batch trust scores");
            return StatusCode(500, new { Error = "Failed to fetch batch trust scores" });
        }
    }

    /// <summary>
    /// T191: Run bot detection heuristics for a user.
    /// Returns bot probability score and signals.
    /// </summary>
    [HttpGet("{userId:int}/bot-check")]
    public async Task<IActionResult> CheckBot(int userId, [FromServices] IBotDetectionService botDetection)
    {
        try
        {
            var result = await botDetection.AnalyzeAsync(userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running bot detection for user {UserId}", userId);
            return StatusCode(500, new { Error = "Failed to run bot detection" });
        }
    }
}

/// <summary>Request model for batch trust score lookups.</summary>
public record BatchTrustScoreRequest(List<int> UserIds);
