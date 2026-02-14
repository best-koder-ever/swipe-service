using System.Diagnostics.Metrics;

namespace SwipeService.Metrics;

public sealed class SwipeServiceMetrics
{
    public const string MeterName = "SwipeService";

    private readonly Counter<long> _swipesProcessed;
    private readonly Counter<long> _likes;
    private readonly Counter<long> _passes;
    private readonly Counter<long> _mutualMatches;
    private readonly Counter<long> _rateLimited;

    public SwipeServiceMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _swipesProcessed = meter.CreateCounter<long>("swipes_processed_total",
            description: "Total number of swipes processed");
        _likes = meter.CreateCounter<long>("likes_total",
            description: "Total number of likes (right swipes)");
        _passes = meter.CreateCounter<long>("passes_total",
            description: "Total number of passes (left swipes)");
        _mutualMatches = meter.CreateCounter<long>("mutual_matches_total",
            description: "Total number of mutual matches created");
        _rateLimited = meter.CreateCounter<long>("swipes_rate_limited_total",
            description: "Total number of rate-limited swipe attempts");
    }

    public void SwipeProcessed() => _swipesProcessed.Add(1);
    public void Like() => _likes.Add(1);
    public void Pass() => _passes.Add(1);
    public void MutualMatch() => _mutualMatches.Add(1);
    public void RateLimited() => _rateLimited.Add(1);
}
