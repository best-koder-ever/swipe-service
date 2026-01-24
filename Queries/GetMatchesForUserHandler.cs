using MediatR;
using Microsoft.EntityFrameworkCore;
using SwipeService.Common;
using SwipeService.Data;
using SwipeService.Models;

namespace SwipeService.Queries;

public class GetMatchesForUserHandler : IRequestHandler<GetMatchesForUserQuery, Result<List<MatchResult>>>
{
    private readonly SwipeContext _context;
    private readonly ILogger<GetMatchesForUserHandler> _logger;

    public GetMatchesForUserHandler(SwipeContext context, ILogger<GetMatchesForUserHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<List<MatchResult>>> Handle(GetMatchesForUserQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var matches = await _context.Matches
                .Where(m => (m.User1Id == request.UserId || m.User2Id == request.UserId) && m.IsActive)
                .Select(m => new MatchResult
                {
                    Id = m.Id,
                    UserId = request.UserId,
                    MatchedUserId = m.User1Id == request.UserId ? m.User2Id : m.User1Id,
                    MatchedAt = m.CreatedAt,
                    IsActive = m.IsActive
                })
                .OrderByDescending(m => m.MatchedAt)
                .ToListAsync(cancellationToken);

            return Result<List<MatchResult>>.Success(matches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving matches for user {UserId}", request.UserId);
            return Result<List<MatchResult>>.Failure("Failed to retrieve matches");
        }
    }
}
