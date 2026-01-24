using MediatR;
using SwipeService.Common;
using SwipeService.Models;

namespace SwipeService.Queries;

public class GetSwipesByUserQuery : IRequest<Result<UserSwipeHistory>>
{
    public int UserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public bool? IsLike { get; set; }
}
