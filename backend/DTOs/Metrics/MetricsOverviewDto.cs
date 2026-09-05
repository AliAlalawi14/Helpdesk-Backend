using System.Text.Json.Serialization;

namespace backend.DTOs.Metrics;

public sealed record MetricsOverviewDto
{
    public required ThresholdCountDto UnassignedOverdue { get; init; }
    public required ThresholdCountDto StaleInProgress { get; init; }
    public required CountDto UrgentUnresolved { get; init; }
    public required ThroughputDto Throughput { get; init; }
    public required TicketsByStatusDto TicketsByStatus { get; init; }
    public required List<TopCategoryDto> TopCategories { get; init; }

    // Mean of (ResolvedAt - CreatedAt) over every resolved ticket, in minutes. Null, not
    // 0, when nothing has been resolved yet: the client prints a dash for null.
    public required double? AvgResolveMinutes { get; init; }
}

// The threshold travels with the count so the card can label itself: change
// Metrics:UnassignedOverdueHours and "Unassigned over 24h" follows on its own.
// Only one of the two threshold fields is populated per card — the other is null
// and is omitted from the payload the client reads.
public sealed record ThresholdCountDto
{
    public required int Count { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ThresholdHours { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ThresholdDays { get; init; }
}

public sealed record CountDto
{
    public required int Count { get; init; }
}

// Names are fixed at "7Days" by the client contract even though the window is
// configurable; ThroughputDays drives what is actually counted.
public sealed record ThroughputDto
{
    public required int ResolvedLast7Days { get; init; }
    public required int RaisedLast7Days { get; init; }
}

// All four keys are always present. A status with no tickets is 0, never absent —
// the client reads four fixed keys and a missing one renders as a blank cell.
public sealed record TicketsByStatusDto
{
    public required int Open { get; init; }
    public required int InProgress { get; init; }
    public required int Resolved { get; init; }
    public required int Closed { get; init; }
}

public sealed record TopCategoryDto
{
    public required string CategoryId { get; init; }
    public required string Name { get; init; }
    public required int Count { get; init; }
}
