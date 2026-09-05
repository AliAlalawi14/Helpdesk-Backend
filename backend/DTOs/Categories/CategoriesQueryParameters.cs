using Microsoft.AspNetCore.Mvc;

namespace backend.DTOs.Categories;

public sealed record CategoriesQueryParameters
{
    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    [FromQuery(Name = "sort")]
    public string? Sort { get; init; }          // "name desc,createdAt"

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
