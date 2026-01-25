using MediatR;
using Microsoft.EntityFrameworkCore;
using SwipeService.Common;
using SwipeService.Data;
using SwipeService.Models;
using SwipeService.Services;

namespace SwipeService.Commands;

/// <summary>
/// Handles RecordSwipeCommand
/// </summary>
public class RecordSwipeHandler : IRequestHandler<RecordSwipeCommand, Result<SwipeResponse>>
{
    private readonly SwipeContext _context;
    private readonly MatchmakingNotifier _notifier;
    private readonly ILogger<RecordSwipeHandler> _logger;
    private readonly IRateLimitService? _rateLimitService;

    public RecordSwipeHandler(
        SwipeContext context, 
        MatchmakingNotifier notifier, 
        ILogger<RecordSwipeHandler> logger,
        IRateLimitService? rateLimitService = null)
    {
        _context = context;
        _notifier = notifier;
        _logger = logger;
        _rateLimitService = rateLimitService;
    }

    public async Task<Result<SwipeResponse>> Handle(RecordSwipeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Business Rule: Cannot swipe on yourself
            if (request.UserId == request.TargetUserId)
            {
                return Result<SwipeResponse>.Failure("Cannot swipe on yourself");
            }

            // Rate Limiting: Check daily swipe limits
            if (_rateLimitService != null)
            {
                var (isAllowed, remaining, limit) = await _rateLimitService.CheckDailyLimitAsync(request.UserId, request.IsLike);
                if (!isAllowed)
                {
                    return Result<SwipeResponse>.Failure(
                        $"Daily {(request.IsLike ? "like" : "swipe")} limit reached ({limit} per day)");
                }
            }

            // Idempotency: Check if swipe with this idempotency key already exists
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var existingIdempotentSwipe = await _context.Swipes
                    .Include(s => s.Match)
                    .FirstOrDefaultAsync(s => s.IdempotencyKey == request.IdempotencyKey, cancellationToken);

                if (existingIdempotentSwipe != null)
                {
                    // Return original result for idempotent retry
                    var idempotentResponse = new SwipeResponse
                    {
                        Success = true,
                        Message = "Swipe already processed (idempotent)",
                        IsMutualMatch = existingIdempotentSwipe.Match != null,
                        MatchId = existingIdempotentSwipe.Match?.Id ?? 0
                    };
                    return Result<SwipeResponse>.Success(idempotentResponse);
                }
            }

            // Business Rule: Check if swipe already exists (by user + target)
            var existingSwipe = await _context.Swipes
                .FirstOrDefaultAsync(s => s.UserId == request.UserId && s.TargetUserId == request.TargetUserId, cancellationToken);

            if (existingSwipe != null)
            {
                return Result<SwipeResponse>.Failure("Already swiped on this user");
            }

            // Create swipe record
            var swipe = new Swipe
            {
                UserId = request.UserId,
                TargetUserId = request.TargetUserId,
                IsLike = request.IsLike,
                CreatedAt = DateTime.UtcNow,
                IdempotencyKey = request.IdempotencyKey
            };

            _context.Swipes.Add(swipe);
            await _context.SaveChangesAsync(cancellationToken);

            // Increment daily rate limit counter
            if (_rateLimitService != null)
            {
                await _rateLimitService.IncrementSwipeCountAsync(request.UserId);
            }

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
                        s.IsLike, cancellationToken);

                if (mutualSwipe != null)
                {
                    var user1Id = Math.Min(request.UserId, request.TargetUserId);
                    var user2Id = Math.Max(request.UserId, request.TargetUserId);

                    var match = new Match
                    {
                        User1Id = user1Id,
                        User2Id = user2Id,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Matches.Add(match);
                    await _context.SaveChangesAsync(cancellationToken);

                    response.IsMutualMatch = true;
                    response.MatchId = match.Id;
                    response.Message = "It's a match!";

                    // Notify matchmaking service
                    await _notifier.NotifyMatchmakingServiceAsync(request.UserId, request.TargetUserId);

                    _logger.LogInformation("Match created between users {UserId} and {TargetUserId}", 
                        request.UserId, request.TargetUserId);
                }
            }

            return Result<SwipeResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing swipe for user {UserId} on target {TargetUserId}",
                request.UserId, request.TargetUserId);
            return Result<SwipeResponse>.Failure($"Error processing swipe: {ex.Message}");
        }
    }
}
