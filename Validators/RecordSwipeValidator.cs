using FluentValidation;
using SwipeService.Commands;

namespace SwipeService.Validators;

/// <summary>
/// Validates RecordSwipeCommand
/// </summary>
public class RecordSwipeValidator : AbstractValidator<RecordSwipeCommand>
{
    public RecordSwipeValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("User ID must be greater than 0");

        RuleFor(x => x.TargetUserId)
            .GreaterThan(0).WithMessage("Target user ID must be greater than 0")
            .NotEqual(x => x.UserId).WithMessage("Cannot swipe on yourself");
    }
}
