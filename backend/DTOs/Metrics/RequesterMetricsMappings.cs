using backend.Services.Sorting;

namespace backend.DTOs.Metrics;

internal static class RequesterMetricsMappings
{
    // <RequesterMetricsDto, RequesterMetricsDto> because every sortable field here is
    // computed by the aggregation, so sorting necessarily runs on the projected shape
    // — there is no Ticket or User column called "raisedLast30Days" to order by.
    // TopCategoryName is deliberately absent: it is filled in after the page is
    // materialised, so it cannot be a sort key.
    public static readonly SortMappingDefinition<RequesterMetricsDto, RequesterMetricsDto> SortMapping = new()
    {
        Mappings =
        [
            new SortMapping(nameof(RequesterMetricsDto.RaisedLast30Days), nameof(RequesterMetricsDto.RaisedLast30Days)),
            new SortMapping(nameof(RequesterMetricsDto.OpenNow), nameof(RequesterMetricsDto.OpenNow)),
            new SortMapping(nameof(RequesterMetricsDto.Name), nameof(RequesterMetricsDto.Name)),
            new SortMapping(nameof(RequesterMetricsDto.LastRaisedAt), nameof(RequesterMetricsDto.LastRaisedAt))
        ]
    };
}
