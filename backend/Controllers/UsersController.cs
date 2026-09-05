using backend.Database;
using backend.DTOs.common;
using backend.DTOs.Users;
using backend.Entities;
using backend.Services;
using backend.Services.Sorting;
using backend.Extensions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

// `User` is also ControllerBase.User (the ClaimsPrincipal), so the entity is aliased
// wherever it would otherwise be read as the property.
using DomainUser = backend.Entities.User;

namespace backend.Controllers;

// Account management is admin surface, so every action here is admin-only except
// the one deliberate exception: the assignable-staff list, which any moderator may
// read so that a ticket can be handed to a teammate. The attribute therefore sits on
// each action rather than on the class, where it would have applied to that one too.
//
// Role and IsActive are columns on the domain user, so listing and updating is ordinary
// EF and this controller needs no Identity types at all. Accounts are created by the
// seed; there is no create endpoint.
[ApiController]
[Route("api/users")]
public sealed class UsersController(
    ApplicationDbContext dbContext,
    UserContext userContext,
    IMemoryCache memoryCache) : ControllerBase
{
    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] UsersQueryParameters query,
        [FromServices] SortMappingProvider sortMappingProvider)
    {
        if (!sortMappingProvider.ValidateMappings<UserDto, DomainUser>(query.Sort))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided sort parameter isn't valid: '{query.Sort}'");
        }

        List<UserRole> roles = query.ParseRoles();
        string? search = query.Search?.Trim();

        IQueryable<DomainUser> usersQuery = dbContext.Users
            .Where(u => roles.Count == 0 || roles.Contains(u.Role))
            .Where(u => query.IsActive == null || u.IsActive == query.IsActive)
            // ILike is Postgres' case-insensitive LIKE, same as the ticket search.
            .Where(u => search == null
                || EF.Functions.ILike(u.Name, $"%{search}%")
                || EF.Functions.ILike(u.Email, $"%{search}%"));

        SortMapping[] sortMappings = sortMappingProvider.GetMappings<UserDto, DomainUser>();

        int totalItems = await usersQuery.CountAsync();

        List<UserDto> data = await usersQuery
            .ApplySort(query.Sort, sortMappings, defaultOrderBy: "Name")
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .Select(UserQueries.ProjectToDto())
            .ToListAsync();

        var result = new PaginationResult<UserDto>
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

    // Who a ticket may be handed to: active moderators and admins, name and role only.
    // Readable by moderators, which is what lets them assign to a teammate; the brief
    // asks for exactly that, and the full directory (emails, join dates) stays
    // admin-only above.
    [Authorize(Roles = $"{Roles.Moderator},{Roles.Admin}")]
    [HttpGet("assignable")]
    public async Task<IActionResult> GetAssignableUsers()
    {
        List<AssignableUserDto> data = await dbContext.Users
            .Where(u => u.IsActive && (u.Role == UserRole.Moderator || u.Role == UserRole.Admin))
            .OrderBy(u => u.Name)
            .Select(u => new AssignableUserDto { Id = u.Id, Name = u.Name, Role = u.Role })
            .ToListAsync();

        return Ok(data);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateUser(
        string id,
        [FromBody] UpdateUserDto dto,
        [FromServices] IValidator<UpdateUserDto> validator)
    {
        ValidationResult validation = await validator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        DomainUser? user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "This account no longer exists.");
        }

        // Self-lockout guard: every caller here is an admin, so "change my own
        // role" means demoting myself and "deactivate me" locks me out on the
        // next login. Both are almost always a misclick, so refuse them.
        if (id == userContext.GetUserId())
        {
            if (dto.IsActive == false)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    detail: "You can't deactivate your own account.");
            }

            if (dto.Role is not null && dto.Role != UserRole.Admin)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    detail: "You can't change your own role away from admin.");
            }
        }

        // Both are plain columns now — no join-table surgery, no UserManager.
        if (dto.Role is not null)
        {
            user.Role = dto.Role.Value;
        }

        // IsActive is the flag the login gate reads — deactivating here blocks
        // the next login.
        if (dto.IsActive.HasValue)
        {
            user.IsActive = dto.IsActive.Value;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        // UserContext.GetUserAsync caches this record for 30 minutes, so without
        // this eviction a deactivated or demoted user keeps their old record for
        // up to half an hour. Easy to forget; belongs right next to the write.
        memoryCache.Remove(UserContext.CacheKey(id));

        UserDto result = await dbContext.Users
            .Where(u => u.Id == id)
            .Select(UserQueries.ProjectToDto())
            .FirstAsync();

        return Ok(result);
    }
}
