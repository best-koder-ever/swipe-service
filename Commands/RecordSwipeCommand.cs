using MediatR;
using SwipeService.Common;
using SwipeService.Models;

namespace SwipeService.Commands;

/// <summary>
/// Command to record a swipe action
/// </summary>
public class RecordSwipeCommand : IRequest<Result<SwipeResponse>>
{
    public int UserId { get; set; }
    public int TargetUserId { get; set; }
    public bool IsLike { get; set; }
    public string? IdempotencyKey { get; set; }
}
