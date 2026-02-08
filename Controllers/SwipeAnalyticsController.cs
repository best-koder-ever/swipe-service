namespace SwipeService.DTOs;

public record SwipeAnalyticsDto
{
    public string UserId { get; init; } = string.Empty;
    public int TotalSwipes { get; init; }
    public double LikeRatio { get; init; }
    public int PeakHour { get; init; }
    public int StreakDays { get; init; }
    public DateTime CalculatedAt { get; init; } = DateTime.UtcNow;
}
