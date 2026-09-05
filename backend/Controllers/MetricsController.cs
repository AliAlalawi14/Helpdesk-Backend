using backend.Database;
using backend.DTOs.common;
using backend.DTOs.Metrics;
using backend.DTOs.Users;
using backend.Entities;
using backend.Services.Sorting;
using backend.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Controllers;

// Admin-only oversight surface. Everything here aggregates in SQL: the ticket list is
// paginated, so anything counted on the client would only ever see one page.
//
// INVISIBLE IN THE LINQ: the !IsDeleted query filters on Ticket, User and Category
// apply automatically to every query below, so soft-deleted rows are excluded from
// every count, average and group without appearing in any Where clause. That is
// intended - it is also what keeps CategoryDto.TicketCount agreeing with the DELETE
// endpoint's own in-use check.
[ApiController]
[Route("api/metrics")]
[Authorize(Roles = Roles.Admin)]
public sealed class MetricsController(
    ApplicationDbContext dbContext,
    IOptions<MetricsOptions> options) : ControllerBase
{
    private readonly MetricsOptions _metrics = options.Value;

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        DateTime now = DateTime.UtcNow;
        DateTime unassignedCutoff = now.AddHours(-_metrics.UnassignedOverdueHours);
        DateTime staleCutoff = now.AddDays(-_metrics.StaleInProgressDays);
        DateTime throughputCutoff = now.AddDays(-_metrics.ThroughputDays);

        // Query 1 - every headline count in a single pass. GroupBy over a constant key
        // turns these into conditional aggregates (count(*) FILTER (WHERE ...)) rather
        // than five round trips.
        var counts = await dbContext.Tickets
            .GroupBy(_ => 1)
            .Select(g => new
            {
                UnassignedOverdue = g.Count(t =>
                    t.Status == TicketStatus.Open &&
                    t.AssigneeId == null &&
                    t.CreatedAt < unassignedCutoff),
                StaleInProgress = g.Count(t =>
                    t.Status == TicketStatus.InProgress &&
                    t.UpdatedAt < staleCutoff),
                UrgentUnresolved = g.Count(t =>
                    t.Priority == TicketPriority.Urgent &&
                    (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress)),
                Resolved = g.Count(t => t.ResolvedAt >= throughputCutoff),
                Raised = g.Count(t => t.CreatedAt >= throughputCutoff),
                // AVG over a CASE: unresolved tickets contribute NULL, which AVG skips,
                // so an empty set comes back null rather than 0.
                AvgResolveMinutes = g.Average(t => t.ResolvedAt != null
                    ? (double?)(t.ResolvedAt.Value - t.CreatedAt).TotalMinutes
                    : null)
            })
            // Single, not First: the constant key yields exactly one row, and
            // FirstOrDefault without an OrderBy makes EF log a warning on every call.
            .SingleOrDefaultAsync();

        // Query 2 - status histogram. Only statuses that actually occur come back...
        List<StatusCount> statusCounts = await dbContext.Tickets
            .GroupBy(t => t.Status)
            .Select(g => new StatusCount(g.Key, g.Count()))
            .ToListAsync();

        // Query 3 - busiest categories. A GroupBy over tickets can never yield a zero
        // count (a group exists only where a ticket does), so "exclude zero counts" is
        // satisfied by construction rather than by a filter.
        List<TopCategoryDto> topCategories = await dbContext.Tickets
            .GroupBy(t => new { t.CategoryId, t.Category.Name })
            .Select(g => new TopCategoryDto
            {
                CategoryId = g.Key.CategoryId,
                Name = g.Key.Name,
                Count = g.Count()
            })
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name)
            .Take(4)
            .ToListAsync();

        int CountFor(TicketStatus status) =>
            statusCounts.FirstOrDefault(s => s.Status == status)?.Count ?? 0;

        var result = new MetricsOverviewDto
        {
            UnassignedOverdue = new ThresholdCountDto
            {
                Count = counts?.UnassignedOverdue ?? 0,
                ThresholdHours = _metrics.UnassignedOverdueHours
            },
            StaleInProgress = new ThresholdCountDto
            {
                Count = counts?.StaleInProgress ?? 0,
                ThresholdDays = _metrics.StaleInProgressDays
            },
            UrgentUnresolved = new CountDto { Count = counts?.UrgentUnresolved ?? 0 },
            Throughput = new ThroughputDto
            {
                ResolvedLast7Days = counts?.Resolved ?? 0,
                RaisedLast7Days = counts?.Raised ?? 0
            },
            // ...so the four keys are filled in here, defaulting to 0. A missing key
            // renders as a blank cell on the client, not a zero.
            TicketsByStatus = new TicketsByStatusDto
            {
                Open = CountFor(TicketStatus.Open),
                InProgress = CountFor(TicketStatus.InProgress),
                Resolved = CountFor(TicketStatus.Resolved),
                Closed = CountFor(TicketStatus.Closed)
            },
            TopCategories = topCategories,
            AvgResolveMinutes = counts?.AvgResolveMinutes is double average
                ? Math.Round(average)
                : null
        };

        return Ok(result);
    }

    [HttpGet("agents")]
    public async Task<IActionResult> GetAgents()
    {
        DateTime now = DateTime.UtcNow;
        DateTime throughputCutoff = now.AddDays(-_metrics.ThroughputDays);

        // Query 1 - the staff list. This is the spine of the result: building from the
        // users and attaching counts is what keeps a zero-load agent in the table.
        // Starting from tickets would silently drop exactly the idle agent an admin
        // most needs to see.
        List<AgentIdentity> staff = await dbContext.Users
            .Where(UserQueries.IsAssignable())
            .OrderBy(u => u.Name)
            .Select(u => new AgentIdentity(u.Id, u.Name))
            .ToListAsync();

        // Query 2 - load and oldest ticket together, one group per assignee. The null
        // key is the unassigned bucket, so this covers the synthetic row too.
        var activeByAssignee = await dbContext.Tickets
            .Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress)
            .GroupBy(t => t.AssigneeId)
            .Select(g => new
            {
                AssigneeId = g.Key,
                Load = g.Count(),
                OldestCreatedAt = g.Min(t => t.CreatedAt)
            })
            .ToListAsync();

        // Query 3 - throughput and the all-time average, one group per assignee.
        // The average spans every resolved ticket, not just the recent window.
        var resolvedByAssignee = await dbContext.Tickets
            .Where(t => t.ResolvedAt != null && t.AssigneeId != null)
            .GroupBy(t => t.AssigneeId!)
            .Select(g => new
            {
                AssigneeId = g.Key,
                RecentCount = g.Count(t => t.ResolvedAt >= throughputCutoff),
                AvgMinutes = g.Average(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalMinutes)
            })
            .ToListAsync();

        Dictionary<string, (int Load, DateTime Oldest)> activeLookup = activeByAssignee
            .Where(a => a.AssigneeId != null)
            .ToDictionary(a => a.AssigneeId!, a => (a.Load, a.OldestCreatedAt));

        Dictionary<string, (int Recent, double Avg)> resolvedLookup = resolvedByAssignee
            .ToDictionary(r => r.AssigneeId, r => (r.RecentCount, r.AvgMinutes));

        // Left join in memory against the staff list - three queries total, no matter
        // how many agents exist.
        var agents = staff
            .Select(agent =>
            {
                bool hasLoad = activeLookup.TryGetValue(agent.Id, out (int Load, DateTime Oldest) active);
                bool hasResolved = resolvedLookup.TryGetValue(agent.Id, out (int Recent, double Avg) resolved);

                return new AgentMetricsDto
                {
                    UserId = agent.Id,
                    Name = agent.Name,
                    ActiveLoad = hasLoad ? active.Load : 0,
                    ResolvedLast7Days = hasResolved ? resolved.Recent : 0,
                    // null, not 0: an agent who has resolved nothing must not sort
                    // alongside one who resolves instantly.
                    AvgResolveMinutes = hasResolved ? Math.Round(resolved.Avg) : null,
                    OldestOpenAgeHours = hasLoad ? AgeInHours(now, active.Oldest) : null
                };
            })
            .ToList();

        var unassignedGroup = activeByAssignee.Find(a => a.AssigneeId == null);

        var result = new AgentMetricsResponseDto
        {
            Agents = agents,
            Unassigned = new UnassignedMetricsDto
            {
                ActiveLoad = unassignedGroup?.Load ?? 0,
                OldestOpenAgeHours = unassignedGroup is null
                    ? null
                    : AgeInHours(now, unassignedGroup.OldestCreatedAt)
            }
        };

        return Ok(result);
    }

    [HttpGet("requesters")]
    public async Task<IActionResult> GetRequesters(
        [FromQuery] RequestersQueryParameters query,
        [FromServices] SortMappingProvider sortMappingProvider)
    {
        if (!sortMappingProvider.ValidateMappings<RequesterMetricsDto, RequesterMetricsDto>(query.Sort))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided sort parameter isn't valid: '{query.Sort}'");
        }

        DateTime raisedCutoff = DateTime.UtcNow.AddDays(-30);

        // Grouping over tickets means only users who have actually raised something
        // appear - no role filter, because moderators and admins raise tickets too.
        // The join to Users is inner: a soft-deleted user drops out with their row,
        // which is the same thing the query filter does everywhere else here.
        IQueryable<RequesterMetricsDto> requestersQuery = dbContext.Tickets
            .GroupBy(t => t.RequesterId)
            .Select(g => new
            {
                UserId = g.Key,
                RaisedLast30Days = g.Count(t => t.CreatedAt >= raisedCutoff),
                OpenNow = g.Count(t =>
                    t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress),
                LastRaisedAt = g.Max(t => t.CreatedAt)
            })
            .Join(
                dbContext.Users,
                a => a.UserId,
                u => u.Id,
                (a, u) => new RequesterMetricsDto
                {
                    UserId = a.UserId,
                    Name = u.Name,
                    RaisedLast30Days = a.RaisedLast30Days,
                    OpenNow = a.OpenNow,
                    LastRaisedAt = a.LastRaisedAt

                    // TopCategoryName stays null here and is filled after paging.
                });

        SortMapping[] sortMappings =
            sortMappingProvider.GetMappings<RequesterMetricsDto, RequesterMetricsDto>();

        int totalItems = await requestersQuery.CountAsync();

        List<RequesterMetricsDto> data = await requestersQuery
            .ApplySort(query.Sort, sortMappings, defaultOrderBy: "RaisedLast30Days DESC")
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .ToListAsync();

        Dictionary<string, string?> topCategories = await GetTopCategoriesAsync(data);

        data = data
            .Select(r => r with { TopCategoryName = topCategories.GetValueOrDefault(r.UserId) })
            .ToList();

        var result = new PaginationResult<RequesterMetricsDto>
        {
            Data = data,
            Pagination = new PaginationMeta
            {
                Page = query.Page,
                Limit = query.Limit,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)query.Limit)
            }
        };

        return Ok(result);
    }

    // One grouped query for the whole page rather than a "most common category" lookup
    // per row. Restricted to the ids just materialised, so the cost is bounded by the
    // page size, not the user count.
    private async Task<Dictionary<string, string?>> GetTopCategoriesAsync(
        List<RequesterMetricsDto> page)
    {
        if (page.Count == 0)
        {
            return [];
        }

        List<string> userIds = page.ConvertAll(r => r.UserId);

        var counts = await dbContext.Tickets
            .Where(t => userIds.Contains(t.RequesterId))
            .GroupBy(t => new { t.RequesterId, t.Category.Name })
            .Select(g => new
            {
                g.Key.RequesterId,
                CategoryName = g.Key.Name,
                Count = g.Count()
            })
            .ToListAsync();

        return counts
            .GroupBy(c => c.RequesterId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var ranked = g.OrderByDescending(c => c.Count).ToList();

                    // A tie has no winner: picking one arbitrarily would read as a
                    // finding the data doesn't support.
                    bool tied = ranked.Count > 1 && ranked[0].Count == ranked[1].Count;

                    return tied ? null : ranked[0].CategoryName;
                });
    }

    private static double AgeInHours(DateTime now, DateTime createdAt) =>
        Math.Round((now - createdAt).TotalHours);

    private sealed record StatusCount(TicketStatus Status, int Count);

    private sealed record AgentIdentity(string Id, string Name);
}
