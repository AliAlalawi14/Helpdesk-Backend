namespace backend.Settings;

// "Stale", "overdue" and the throughput window are business rules an admin should be
// able to retune without a redeploy, so they are configuration rather than constants.
// The overview response echoes them back, which is what lets the UI label a card
// "Unassigned over 24h" without hardcoding 24 on the client.
public sealed class MetricsOptions
{
    public const string SectionName = "Metrics";

    public int UnassignedOverdueHours { get; init; } = 24;
    public int StaleInProgressDays { get; init; } = 3;
    public int ThroughputDays { get; init; } = 7;
}
