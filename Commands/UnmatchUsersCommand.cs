using MediatR;
using SwipeService.Common;

namespace SwipeService.Commands;

public class UnmatchUsersCommand : IRequest<Result>
{
    public int UserId { get; set; }
    public int TargetUserId { get; set; }
}
