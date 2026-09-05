using backend.Database;
using backend.Entities;
using backend.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace backend.Services;

public sealed class UserContext(
    IHttpContextAccessor httpContextAccessor,
    ApplicationDbContext dbContext,
    IMemoryCache memoryCache)
{
    private const string CacheKeyPrefix = "users:id:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public static string CacheKey(string userId) => $"{CacheKeyPrefix}{userId}";

    // `sub` carries the DOMAIN user id, so this stays a claim read — no lookup, no
    // cache. Caching it would cache a value already sitting in memory on the request.
    // Keep the RBAC hot path (ticket scope, ownership checks) on this and IsInRole.
    public string? GetUserId()
    {
        return httpContextAccessor.HttpContext?.User.GetUserId();
    }

    public bool IsInRole(string role)
    {
        return httpContextAccessor.HttpContext?.User.IsInRole(role) ?? false;
    }

    // This one hits the database, so this is where the cache earns its place.
    // Reserve it for when you actually need the record — name, live IsActive,
    // role straight from the column rather than a possibly-stale claim.
    public async Task<User?> GetUserAsync(CancellationToken cancellationToken = default)
    {
        string? userId = GetUserId();
        if (userId is null)
        {
            return null;
        }

        return await memoryCache.GetOrCreateAsync(CacheKey(userId), async entry =>
        {
            entry.SetSlidingExpiration(CacheDuration);

            // AsNoTracking is load-bearing, not an optimisation. IMemoryCache is a
            // singleton and ApplicationDbContext is scoped: caching a tracked entity
            // would pin its change tracker — and the whole disposed DbContext behind
            // it — in memory for the life of the entry, and re-attaching it on a
            // later request throws.
            return await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        });
    }
}
