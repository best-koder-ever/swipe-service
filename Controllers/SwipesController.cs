using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwipeService.Data;
using SwipeService.Models;
using SwipeService.Services;
using System.ComponentModel.DataAnnotations;

namespace SwipeService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SwipesController : ControllerBase
    {
        private readonly SwipeContext _context;
        private readonly MatchmakingNotifier _notifier;
        private readonly ILogger<SwipesController> _logger;

        public SwipesController(SwipeContext context, MatchmakingNotifier notifier, ILogger<SwipesController> logger)
        {
            _context = context;
            _notifier = notifier;
            _logger = logger;
        }

        // POST: Record a single swipe
        [HttpPost]
        public async Task<IActionResult> Swipe([FromBody] SwipeRequest request)
        {
            try
            {
                // Validate input
                if (request.UserId == request.TargetUserId)
                {
                    return BadRequest(new SwipeResponse 
                    { 
                        Success = false, 
                        Message = "Cannot swipe on yourself" 
                    });
                }

                // Check if swipe already exists
                var existingSwipe = await _context.Swipes
                    .FirstOrDefaultAsync(s => s.UserId == request.UserId && s.TargetUserId == request.TargetUserId);

                if (existingSwipe != null)
                {
                    return BadRequest(new SwipeResponse 
                    { 
                        Success = false, 
                        Message = "Already swiped on this user" 
                    });
                }

                var swipe = new Swipe
                {
                    UserId = request.UserId,
                    TargetUserId = request.TargetUserId,
                    IsLike = request.IsLike,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Swipes.Add(swipe);
                await _context.SaveChangesAsync();

                var response = new SwipeResponse
                {
                    Success = true,
                    Message = "Swipe recorded successfully",
                    IsMutualMatch = false
                };

                // Check for mutual match if it's a like
                if (request.IsLike)
                {
                    var mutualSwipe = await _context.Swipes
                        .FirstOrDefaultAsync(s =>
                            s.UserId == request.TargetUserId &&
                            s.TargetUserId == request.UserId &&
                            s.IsLike);

                    if (mutualSwipe != null)
                    {
                        // Create match record
                        var user1Id = Math.Min(request.UserId, request.TargetUserId);
                        var user2Id = Math.Max(request.UserId, request.TargetUserId);

                        var match = new Match
                        {
                            User1Id = user1Id,
                            User2Id = user2Id,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Matches.Add(match);
                        await _context.SaveChangesAsync();

                        response.IsMutualMatch = true;
                        response.MatchId = match.Id;
                        response.Message = "It's a match!";

                        // Notify matchmaking service
                        await _notifier.NotifyMatchmakingServiceAsync(request.UserId, request.TargetUserId);
                    }
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing swipe for user {UserId} on target {TargetUserId}", 
                    request.UserId, request.TargetUserId);
                return StatusCode(500, new SwipeResponse 
                { 
                    Success = false, 
                    Message = "Internal server error" 
                });
            }
        }

        // POST: Record multiple swipes in batch
        [HttpPost("batch")]
        public async Task<IActionResult> BatchSwipe([FromBody] BatchSwipeRequest request)
        {
            try
            {
                var responses = new List<SwipeResponse>();
                var matches = new List<Match>();

                foreach (var swipeAction in request.Swipes)
                {
                    // Validate individual swipe
                    if (request.UserId == swipeAction.TargetUserId)
                    {
                        responses.Add(new SwipeResponse
                        {
                            Success = false,
                            Message = $"Cannot swipe on yourself (Target: {swipeAction.TargetUserId})"
                        });
                        continue;
                    }

                    // Check if swipe already exists
                    var existingSwipe = await _context.Swipes
                        .FirstOrDefaultAsync(s => s.UserId == request.UserId && s.TargetUserId == swipeAction.TargetUserId);

                    if (existingSwipe != null)
                    {
                        responses.Add(new SwipeResponse
                        {
                            Success = false,
                            Message = $"Already swiped on user {swipeAction.TargetUserId}"
                        });
                        continue;
                    }

                    var swipe = new Swipe
                    {
                        UserId = request.UserId,
                        TargetUserId = swipeAction.TargetUserId,
                        IsLike = swipeAction.IsLike,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Swipes.Add(swipe);

                    var response = new SwipeResponse
                    {
                        Success = true,
                        Message = $"Swipe recorded for user {swipeAction.TargetUserId}",
                        IsMutualMatch = false
                    };

                    // Check for mutual match if it's a like
                    if (swipeAction.IsLike)
                    {
                        var mutualSwipe = await _context.Swipes
                            .FirstOrDefaultAsync(s =>
                                s.UserId == swipeAction.TargetUserId &&
                                s.TargetUserId == request.UserId &&
                                s.IsLike);

                        if (mutualSwipe != null)
                        {
                            var user1Id = Math.Min(request.UserId, swipeAction.TargetUserId);
                            var user2Id = Math.Max(request.UserId, swipeAction.TargetUserId);

                            var match = new Match
                            {
                                User1Id = user1Id,
                                User2Id = user2Id,
                                CreatedAt = DateTime.UtcNow
                            };

                            matches.Add(match);
                            _context.Matches.Add(match);

                            response.IsMutualMatch = true;
                            response.Message = $"It's a match with user {swipeAction.TargetUserId}!";
                        }
                    }

                    responses.Add(response);
                }

                await _context.SaveChangesAsync();

                // Notify matchmaking service for all matches
                foreach (var match in matches)
                {
                    await _notifier.NotifyMatchmakingServiceAsync(match.User1Id, match.User2Id);
                }

                return Ok(new { Responses = responses, TotalMatches = matches.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch swipes for user {UserId}", request.UserId);
                return StatusCode(500, new { Success = false, Message = "Internal server error" });
            }
        }

        // GET: Retrieve swipes by user with pagination and filtering
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetSwipesByUser(int userId, 
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 50,
            [FromQuery] bool? isLike = null)
        {
            try
            {
                var query = _context.Swipes.Where(s => s.UserId == userId);
                
                if (isLike.HasValue)
                {
                    query = query.Where(s => s.IsLike == isLike.Value);
                }

                var totalCount = await query.CountAsync();
                
                var swipes = await query
                    .OrderByDescending(s => s.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new SwipeRecord
                    {
                        Id = s.Id,
                        TargetUserId = s.TargetUserId,
                        IsLike = s.IsLike,
                        CreatedAt = s.CreatedAt
                    })
                    .ToListAsync();

                var response = new UserSwipeHistory
                {
                    UserId = userId,
                    Swipes = swipes,
                    TotalSwipes = totalCount,
                    TotalLikes = await _context.Swipes.CountAsync(s => s.UserId == userId && s.IsLike),
                    TotalPasses = await _context.Swipes.CountAsync(s => s.UserId == userId && !s.IsLike)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving swipes for user {UserId}", userId);
                return StatusCode(500, new { Success = false, Message = "Internal server error" });
            }
        }

        // GET: Get matches for a user
        [HttpGet("matches/{userId}")]
        public async Task<IActionResult> GetMatchesForUser(int userId)
        {
            try
            {
                var matches = await _context.Matches
                    .Where(m => (m.User1Id == userId || m.User2Id == userId) && m.IsActive)
                    .Select(m => new MatchResult
                    {
                        Id = m.Id,
                        UserId = userId,
                        MatchedUserId = m.User1Id == userId ? m.User2Id : m.User1Id,
                        MatchedAt = m.CreatedAt,
                        IsActive = m.IsActive
                    })
                    .OrderByDescending(m => m.MatchedAt)
                    .ToListAsync();

                return Ok(matches);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving matches for user {UserId}", userId);
                return StatusCode(500, new { Success = false, Message = "Internal server error" });
            }
        }

        // GET: Retrieve users who liked a specific user
        [HttpGet("received-likes/{userId}")]
        public async Task<IActionResult> GetLikesReceivedByUser(int userId)
        {
            try
            {
                var likes = await _context.Swipes
                    .Where(s => s.TargetUserId == userId && s.IsLike)
                    .Select(s => new { UserId = s.UserId, LikedAt = s.CreatedAt })
                    .OrderByDescending(s => s.LikedAt)
                    .ToListAsync();

                return Ok(likes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving received likes for user {UserId}", userId);
                return StatusCode(500, new { Success = false, Message = "Internal server error" });
            }
        }

        // GET: Check if two users have a mutual match
        [HttpGet("match/{userId}/{targetUserId}")]
        public async Task<IActionResult> CheckMutualMatch(int userId, int targetUserId)
        {
            try
            {
                var user1Id = Math.Min(userId, targetUserId);
                var user2Id = Math.Max(userId, targetUserId);

                var match = await _context.Matches
                    .FirstOrDefaultAsync(m => m.User1Id == user1Id && m.User2Id == user2Id && m.IsActive);

                return Ok(new { IsMutualMatch = match != null, MatchId = match?.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking mutual match between {UserId} and {TargetUserId}", userId, targetUserId);
                return StatusCode(500, new { Success = false, Message = "Internal server error" });
            }
        }

        // DELETE: Unmatch users
        [HttpDelete("match/{userId}/{targetUserId}")]
        public async Task<IActionResult> Unmatch(int userId, int targetUserId)
        {
            try
            {
                var user1Id = Math.Min(userId, targetUserId);
                var user2Id = Math.Max(userId, targetUserId);

                var match = await _context.Matches
                    .FirstOrDefaultAsync(m => m.User1Id == user1Id && m.User2Id == user2Id && m.IsActive);

                if (match == null)
                {
                    return NotFound(new { Success = false, Message = "Match not found" });
                }

                match.IsActive = false;
                match.UnmatchedAt = DateTime.UtcNow;
                match.UnmatchedByUserId = userId;

                await _context.SaveChangesAsync();

                return Ok(new { Success = true, Message = "Successfully unmatched" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unmatching users {UserId} and {TargetUserId}", userId, targetUserId);
                return StatusCode(500, new { Success = false, Message = "Internal server error" });
            }
        }

        // GET: Health check
        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new { Status = "Healthy", Service = "SwipeService", Timestamp = DateTime.UtcNow });
        }
    }
}