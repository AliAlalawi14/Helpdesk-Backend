namespace backend.DTOs.Metrics;

public sealed record AgentMetricsResponseDto
{
    public required List<AgentMetricsDto> Agents { get; init; }
    public required UnassignedMetricsDto Unassigned { get; init; }
}

public sealed record AgentMetricsDto
{
    public required string UserId { get; init; }
    public required string Name { get; init; }
    public required int ActiveLoad { get; init; }
    public required int ResolvedLast7Days { get; init; }

    // Genuinely nullable, and never 0 as a stand-in. "Has resolved nothing" and
    // "resolves instantly" are different facts, and collapsing them would sort a
    // false champion to the top of the column.
    public double? AvgResolveMinutes { get; init; }
    public double? OldestOpenAgeHours { get; init; }
}

// The work nobody owns, shaped like an agent row so the table can render it inline.
public sealed record UnassignedMetricsDto
{
    public required int ActiveLoad { get; init; }
    public double? OldestOpenAgeHours { get; init; }
}
