using MediatR;
using Microsoft.EntityFrameworkCore;
using SwipeService.Common;
using SwipeService.Data;
using SwipeService.Models;

namespace SwipeService.Queries;

public class GetSwipesByUserHandler : IRequestHandler<GetSwipesByUserQuery, Result<UserSwipeHistory>>
{
    private readonly SwipeContext _context;
    private readonly ILogger<GetSwipesByUserHandler> _logger;

    public GetSwipesByUserHandler(SwipeContext context, ILogger<GetSwipesByUserHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<UserSwipeHistory>> Handle(GetSwipesByUserQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _context.Swipes.Where(s => s.UserId == request.UserId);
            
            if (request.IsLike.HasValue)
            {
                query = query.Where(s => s.IsLike == request.IsLike.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            
            var swipes = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(s => new SwipeRecord
                {
                    Id = s.Id,
                    TargetUserId = s.TargetUserId,
                    IsLike = s.IsLike,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var response = new UserSwipeHistory
            {
                UserId = request.UserId,
                Swipes = swipes,
                TotalSwipes = totalCount,
                TotalLikes = await _context.Swipes.CountAsync(s => s.UserId == request.UserId && s.IsLike, cancellationToken),
                TotalPasses = await _context.Swipes.CountAsync(s => s.UserId == request.UserId && !s.IsLike, cancellationToken)
            };

            return Result<UserSwipeHistory>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving swipes for user {UserId}", request.UserId);
            return Result<UserSwipeHistory>.Failure("Failed to retrieve swipe history");
        }
    }
}
