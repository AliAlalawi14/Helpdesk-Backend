namespace backend.DTOs.Metrics;

// Demand, not a leaderboard: repeated tickets in one category usually mean a
// systemic problem, which is why TopCategoryName carries the signal rather than
// raw volume alone.
public sealed record RequesterMetricsDto
{
    public required string UserId { get; init; }
    public required string Name { get; init; }
    public required int RaisedLast30Days { get; init; }
    public required int OpenNow { get; init; }

    // null when the requester has no tickets, or when their top two categories are
    // tied — an arbitrary winner would read as a finding that isn't there.
    public string? TopCategoryName { get; init; }

    public required DateTime LastRaisedAt { get; init; }
}
