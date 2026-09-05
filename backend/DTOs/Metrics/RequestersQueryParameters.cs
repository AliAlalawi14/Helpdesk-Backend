using Microsoft.AspNetCore.Mvc;

namespace backend.DTOs.Metrics;

public sealed record RequestersQueryParameters
{
    [FromQuery(Name = "sort")]
    public string? Sort { get; init; }          // "raisedLast30Days desc"

    [FromQuery(Name = "page")]
    public int Page
    {
        get;
        // Clamped the way Limit is below. Paging is Skip((Page - 1) * Limit), so page 0
        // asked the database to skip a negative number of rows and threw instead of
        // returning a page. Anything before the first page is the first page.
        init => field = value < 1 ? 1 : value;
    } = 1;

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
