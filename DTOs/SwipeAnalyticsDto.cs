namespace SwipeService.DTOs;

public record SwipeAnalyticsDto
{
    public int UserId { get; init; }
    public int TotalSwipes { get; init; }
    public double LikeRatio { get; init; }
    public int PeakHour { get; init; }
    public int StreakDays { get; init; }
    public DateTime CalculatedAt { get; init; } = DateTime.UtcNow;
}
