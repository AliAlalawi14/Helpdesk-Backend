using backend.Entities;
using Microsoft.AspNetCore.Mvc;

namespace backend.DTOs.Users;

public sealed record UsersQueryParameters
{
    [FromQuery(Name = "search")]
    public string? Search { get; init; }        // name or email

    [FromQuery(Name = "role")]
    public string? Role { get; init; }          // "moderator,admin"

    [FromQuery(Name = "isActive")]
    public bool? IsActive { get; init; }

    [FromQuery(Name = "sort")]
    public string? Sort { get; init; }          // "role,name desc"

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

    // Role is an enum column now, so this parses like ParseStatuses/ParsePriorities
    // do on the ticket parameters: unknown values are dropped rather than 400-ing,
    // matching how ?status=nonsense already behaves.
    public List<UserRole> ParseRoles()
    {
        if (string.IsNullOrWhiteSpace(Role))
        {
            return [];
        }

        return Role
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(v => Enum.TryParse<UserRole>(v, ignoreCase: true, out _))
            .Select(v => Enum.Parse<UserRole>(v, ignoreCase: true))
            .ToList();
    }
}
