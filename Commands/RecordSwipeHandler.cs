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

    public RecordSwipeHandler(SwipeContext context, MatchmakingNotifier notifier, ILogger<RecordSwipeHandler> logger)
    {
        _context = context;
        _notifier = notifier;
        _logger = logger;
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

            // Business Rule: Check if swipe already exists
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
                CreatedAt = DateTime.UtcNow
            };

            _context.Swipes.Add(swipe);
            await _context.SaveChangesAsync(cancellationToken);

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
