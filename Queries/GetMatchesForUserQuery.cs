using MediatR;
using SwipeService.Common;
using SwipeService.Models;

namespace SwipeService.Queries;

public class GetMatchesForUserQuery : IRequest<Result<List<MatchResult>>>
{
    public int UserId { get; set; }
}
