using backend.Entities;
using Microsoft.AspNetCore.Mvc;

namespace backend.DTOs.Tickets;

public sealed record TicketQueryParameters
{
    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    [FromQuery(Name = "status")]
    public string? Status { get; init; }        // "open,in_progress"

    [FromQuery(Name = "priority")]
    public string? Priority { get; init; }      // "high,urgent"

    [FromQuery(Name = "category")]
    public string? Category { get; init; }      // "cat_it_access,cat_hr_payroll"

    [FromQuery(Name = "assignee")]
    public string? Assignee { get; init; }      // "me" | "unassigned" | a userId

    [FromQuery(Name = "requester")]
    public string? Requester { get; init; }     // "usr_jordan,usr_priya"

    // Upper bounds on age, as ISO instants. The admin overview counts "unassigned for
    // over 24h" and "no update in 3 days"; these let a link open exactly that set rather
    // than a superset the reader has to eyeball.
    [FromQuery(Name = "createdBefore")]
    public DateTime? CreatedBefore { get; init; }

    [FromQuery(Name = "updatedBefore")]
    public DateTime? UpdatedBefore { get; init; }

    [FromQuery(Name = "sort")]
    public string? Sort { get; init; }          // "priority desc,createdAt"

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

    public List<TicketStatus> ParseStatuses() => ParseEnums<TicketStatus>(Status);
    public List<TicketPriority> ParsePriorities() => ParseEnums<TicketPriority>(Priority);

    // Categories and requesters are plain string ids - no enum conversion, no
    // underscore handling - so these are just split-and-trim rather than ParseEnums.
    public List<string> ParseCategories() => ParseIds(Category);

    // Requester is a list of user ids, so it splits the same way categories do rather
    // than following Assignee, which carries the single "me"/"unassigned" vocabulary.
    public List<string> ParseRequesters() => ParseIds(Requester);

    private static List<string> ParseIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static List<TEnum> ParseEnums<TEnum>(string? raw) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.Replace("_", "", StringComparison.OrdinalIgnoreCase))  // in_progress -> inprogress
            .Where(v => Enum.TryParse<TEnum>(v, ignoreCase: true, out _))
            .Select(v => Enum.Parse<TEnum>(v, ignoreCase: true))
            .ToList();
    }
}