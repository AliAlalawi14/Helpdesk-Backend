using Microsoft.AspNetCore.Mvc;

namespace backend.DTOs.Metrics;

public sealed record RequestersQueryParameters
{
    [FromQuery(Name = "sort")]
    public string? Sort { get; init; }          // "raisedLast30Days desc"

    [FromQuery(Name = "page")]
    public int Page { get; init; } = 1;

    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    [FromQuery(Name = "limit")]
    public int Limit
    {
        get;
        init => field = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    } = DefaultPageSize;
}
