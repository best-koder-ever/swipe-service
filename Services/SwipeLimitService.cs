namespace SwipeService.DTOs;

/// <summary>
/// Response DTO for swipe limit status
/// </summary>
public record SwipeLimitDto(
    int DailyLimit,
    int Used,
    int Remaining,
    DateTime ResetsAt
);
