using Microsoft.AspNetCore.Mvc;
using SwipeService.Data;
using SwipeService.Models;
using SwipeService.Services;

namespace SwipeService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SwipesController : ControllerBase
    {
        private readonly SwipeContext _context;
        private readonly MatchmakingNotifier _notifier;

        public SwipesController(SwipeContext context, MatchmakingNotifier notifier)
        {
            _context = context;
            _notifier = notifier;
        }

        // POST: Record a swipe
        [HttpPost]
        public async Task<IActionResult> Swipe([FromBody] SwipeRequest request)
        {
            var swipe = new Swipe
            {
                UserId = request.UserId,
                TargetUserId = request.TargetUserId,
                IsLike = request.IsLike
            };

            _context.Swipes.Add(swipe);
            await _context.SaveChangesAsync();

            // Notify MatchmakingService if it's a mutual match
            if (request.IsLike)
            {
                var mutualSwipe = _context.Swipes.FirstOrDefault(s =>
                    s.UserId == request.TargetUserId &&
                    s.TargetUserId == request.UserId &&
                    s.IsLike);

                if (mutualSwipe != null)
                {
                    await _notifier.NotifyMatchmakingServiceAsync(request.UserId, request.TargetUserId);
                }
            }

            return Ok(new { Message = "Swipe recorded successfully!" });
        }

        // GET: Retrieve swipes by user
        [HttpGet("{userId}")]
        public IActionResult GetSwipesByUser(int userId)
        {
            var swipes = _context.Swipes.Where(s => s.UserId == userId).ToList();
            return Ok(swipes);
        }

        // GET: Retrieve users who liked a specific user
        [HttpGet("received-likes/{userId}")]
        public IActionResult GetLikesReceivedByUser(int userId)
        {
            var likes = _context.Swipes
                .Where(s => s.TargetUserId == userId && s.IsLike)
                .Select(s => s.UserId)
                .ToList();

            return Ok(likes);
        }

        // GET: Check if two users have a mutual match
        [HttpGet("match/{userId}/{targetUserId}")]
        public IActionResult CheckMutualMatch(int userId, int targetUserId)
        {
            var mutualMatch = _context.Swipes.Any(s =>
                s.UserId == userId &&
                s.TargetUserId == targetUserId &&
                s.IsLike &&
                _context.Swipes.Any(ms =>
                    ms.UserId == targetUserId &&
                    ms.TargetUserId == userId &&
                    ms.IsLike));

            return Ok(new { IsMutualMatch = mutualMatch });
        }
    }
}