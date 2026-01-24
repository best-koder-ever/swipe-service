using MediatR;
using Microsoft.EntityFrameworkCore;
using SwipeService.Common;
using SwipeService.Data;

namespace SwipeService.Commands;

public class UnmatchUsersHandler : IRequestHandler<UnmatchUsersCommand, Result>
{
    private readonly SwipeContext _context;
    private readonly ILogger<UnmatchUsersHandler> _logger;

    public UnmatchUsersHandler(SwipeContext context, ILogger<UnmatchUsersHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result> Handle(UnmatchUsersCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user1Id = Math.Min(request.UserId, request.TargetUserId);
            var user2Id = Math.Max(request.UserId, request.TargetUserId);

            var match = await _context.Matches
                .FirstOrDefaultAsync(m => m.User1Id == user1Id && m.User2Id == user2Id && m.IsActive, cancellationToken);

            if (match == null)
            {
                return Result.Failure("Match not found");
            }

            match.IsActive = false;
            match.UnmatchedAt = DateTime.UtcNow;
            match.UnmatchedByUserId = request.UserId;

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unmatching users {UserId} and {TargetUserId}", request.UserId, request.TargetUserId);
            return Result.Failure("Failed to unmatch users");
        }
    }
}
