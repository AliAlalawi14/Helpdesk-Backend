using backend.Database;
using backend.DTOs.common;
using backend.DTOs.Tickets;
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

namespace backend.Controllers;

[ApiController]
[Route("api/tickets")]
public sealed class TicketsController(
    ApplicationDbContext dbContext,
    UserContext userContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTickets(
        [FromQuery] TicketQueryParameters query,
        [FromServices] SortMappingProvider sortMappingProvider)
    {
        if (!sortMappingProvider.ValidateMappings<TicketDto, Ticket>(query.Sort))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided sort parameter isn't valid: '{query.Sort}'");
        }

        List<TicketStatus> statuses = query.ParseStatuses();
        List<TicketPriority> priorities = query.ParsePriorities();
        List<string> categories = query.ParseCategories();
        List<string> requesters = query.ParseRequesters();
        string? search = query.Search?.Trim();
        // "HD-42", "hd 42" and "42" all mean ticket 42. The reference is quoted back at
        // an agent far more often than a subject line is, so the one search box has to
        // find it; without this the number would be readable and useless.
        int? searchReference = ParseReference(search);
        // Npgsql refuses a non-UTC DateTime against timestamptz; a bound instant may
        // arrive as Local or Unspecified depending on how the client wrote it.
        DateTime? createdBefore = AsUtc(query.CreatedBefore);
        DateTime? updatedBefore = AsUtc(query.UpdatedBefore);

        string currentUserId = userContext.GetUserId()!;
        bool isStaff = userContext.IsInRole(Roles.Moderator) || userContext.IsInRole(Roles.Admin);

        IQueryable<Ticket> ticketsQuery = dbContext.Tickets
            // a plain user is scoped to their own tickets; staff see everything.
            // this goes first so every filter below runs inside that scope.
            .Where(t => isStaff || t.RequesterId == currentUserId)
            .Where(t => statuses.Count == 0 || statuses.Contains(t.Status))
            .Where(t => priorities.Count == 0 || priorities.Contains(t.Priority))
            .Where(t => categories.Count == 0 || categories.Contains(t.CategoryId))
            // A plain user is already scoped to their own tickets above, so this narrows
            // nothing for them; it is the staff board's "whose ticket is this?" filter.
            .Where(t => requesters.Count == 0 || requesters.Contains(t.RequesterId))
            .Where(t => createdBefore == null || t.CreatedAt < createdBefore)
            .Where(t => updatedBefore == null || t.UpdatedAt < updatedBefore)
            // ILike is Postgres' case-insensitive LIKE and EF translates it
            // directly. string.Contains(value, StringComparison) has no SQL
            // translation at all - EF throws rather than falling back.
            .Where(t => search == null
                || searchReference != null && t.Reference == searchReference
                || EF.Functions.ILike(t.Subject, $"%{search}%")
                || EF.Functions.ILike(t.Description, $"%{search}%"));

        // assignee: "unassigned" | "me" | specific userId
        if (query.Assignee == "unassigned")
        {
            ticketsQuery = ticketsQuery.Where(t => t.AssigneeId == null);
        }
        else if (query.Assignee == "me")
        {
            ticketsQuery = ticketsQuery.Where(t => t.AssigneeId == currentUserId);
        }
        else if (!string.IsNullOrWhiteSpace(query.Assignee))
        {
            ticketsQuery = ticketsQuery.Where(t => t.AssigneeId == query.Assignee);
        }

        SortMapping[] sortMappings = sortMappingProvider.GetMappings<TicketDto, Ticket>();

        int totalItems = await ticketsQuery.CountAsync();

        List<TicketDto> data = await ticketsQuery
            .ApplySort(query.Sort, sortMappings, defaultOrderBy: "CreatedAt DESC")
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .Select(TicketQueries.ProjectToDto())
            .ToListAsync();

        var result = new PaginationResult<TicketDto>
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

    private static DateTime? AsUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } utc => utc,
        { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
        var unspecified => DateTime.SpecifyKind(unspecified.Value, DateTimeKind.Utc)
    };

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTicketById(string id)
    {
        TicketDetailDto? ticket = await dbContext.Tickets
            .Where(t => t.Id == id)
            .Select(TicketQueries.ProjectToDetailDto())
            .FirstOrDefaultAsync();

        if (ticket is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "This ticket does not exist or has been deleted.");
        }

        // ownership: a plain user may only view their own ticket
        bool isStaff = userContext.IsInRole(Roles.Moderator) || userContext.IsInRole(Roles.Admin);
        if (!isStaff && ticket.RequesterId != userContext.GetUserId())
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have permission to view this ticket.");
        }

        return Ok(ticket);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTicket(
        [FromBody] CreateTicketDto dto,
        [FromServices] IValidator<CreateTicketDto> validator)
    {
        ValidationResult validation = await validator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        // category must exist (validator can't check the DB)
        bool categoryExists = await dbContext.Categories.AnyAsync(c => c.Id == dto.CategoryId);
        if (!categoryExists)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "That category no longer exists. Pick another one.");
        }

        // the current user IS the requester - never client-supplied
        string requesterId = userContext.GetUserId()!;

        if (dto.AssigneeId is not null)
        {
            // Any account may raise a ticket, so this action carries no role attribute and
            // the check lives here instead. Handing the ticket to someone is a different
            // permission from raising it, and the matrix gives it to staff alone.
            bool isStaff = userContext.IsInRole(Roles.Moderator) || userContext.IsInRole(Roles.Admin);
            if (!isStaff)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    detail: "Only a moderator or admin may assign a ticket.");
            }

            if (!await IsAssignableAsync(dto.AssigneeId))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: "That person cannot be assigned tickets. Pick an active moderator or admin.");
            }
        }

        // Status is server-decided and starts Open even when an owner is named: assigned
        // means someone holds it, in progress means work has started.
        Ticket ticket = dto.ToEntity(requesterId, dto.AssigneeId);
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        TicketDto result = await dbContext.Tickets
            .Where(t => t.Id == ticket.Id)
            .Select(TicketQueries.ProjectToDto())
            .FirstAsync();

        return CreatedAtAction(nameof(GetTicketById), new { id = ticket.Id }, result);
    }

    [HttpPost("{id}/comments")]
    public async Task<IActionResult> AddComment(
        string id,
        [FromBody] CreateCommentDto dto,
        [FromServices] IValidator<CreateCommentDto> validator)
    {
        ValidationResult validation = await validator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        // parent ticket must exist (and not be soft-deleted — query filter handles that).
        // select RequesterId rather than AnyAsync: the ownership check needs it anyway.
        var ticket = await dbContext.Tickets
            .Where(t => t.Id == id)
            .Select(t => new { t.RequesterId })
            .FirstOrDefaultAsync();

        if (ticket is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "This ticket does not exist or has been deleted.");
        }

        string currentUserId = userContext.GetUserId()!;
        bool isStaff = userContext.IsInRole(Roles.Moderator) || userContext.IsInRole(Roles.Admin);
        if (!isStaff && ticket.RequesterId != currentUserId)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have permission to comment on this ticket.");
        }

        TicketComment comment = dto.ToEntity(id, currentUserId);
        dbContext.TicketComments.Add(comment);
        await dbContext.SaveChangesAsync();

        // the author is the caller, so their name comes from the cached record
        // rather than a second query
        User? author = await userContext.GetUserAsync();

        return CreatedAtAction(
            nameof(GetTicketById), new { id }, comment.ToDto(author?.Name ?? string.Empty));
    }

    [Authorize(Roles = $"{Roles.Moderator},{Roles.Admin}")]
    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateTicket(
        string id,
        [FromBody] UpdateTicketDto dto,
        [FromServices] IValidator<UpdateTicketDto> validator)
    {
        ValidationResult validation = await validator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        Ticket? ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "This ticket does not exist or has been deleted.");
        }

        bool changed = false;

        if (dto.Status.HasValue)
        {
            ticket.Status = dto.Status.Value;
            changed = true;

            // ResolvedAt is the clock the metrics read, so it moves only on the
            // status transition — never on any other edit.
            if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
            {
                // ??= so Resolved -> Closed keeps the moment the work actually
                // finished rather than restamping it at the second transition.
                ticket.ResolvedAt ??= DateTime.UtcNow;
            }
            else
            {
                // reopened — the ticket is live again, so it has no resolution time
                ticket.ResolvedAt = null;
            }
        }

        if (dto.Priority.HasValue)
        {
            ticket.Priority = dto.Priority.Value;
            changed = true;
        }

        if (dto.AssigneeId is not null)
        {
            // Same shape as the category check below: a bogus id is a 400 here rather than
            // a 500 out of the foreign key. It also refuses a real id that is not a valid
            // owner — a requester, or a deactivated colleague.
            if (!await IsAssignableAsync(dto.AssigneeId))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: "That person cannot be assigned tickets. Pick an active moderator or admin.");
            }

            ticket.AssigneeId = dto.AssigneeId;
            changed = true;
        }

        if (dto.CategoryId is not null)
        {
            // same DB existence check CreateTicket does, so a bogus id is a 400 here
            // rather than a 500 out of the foreign key
            bool categoryExists = await dbContext.Categories
                .AnyAsync(c => c.Id == dto.CategoryId);
            if (!categoryExists)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: "That category no longer exists. Pick another one.");
            }

            ticket.CategoryId = dto.CategoryId;
            changed = true;
        }

        if (changed)
        {
            ticket.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        TicketDto result = await dbContext.Tickets
            .Where(t => t.Id == ticket.Id)
            .Select(TicketQueries.ProjectToDto())
            .FirstAsync();

        return Ok(result);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTicket(string id)
    {
        Ticket? ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "This ticket does not exist or has been deleted.");
        }

        ticket.IsDeleted = true;
        ticket.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    // Reads UserQueries.IsAssignable, the same predicate GET /api/users/assignable filters
    // the picker by, so the list a reader chooses from and the guard on their choice can
    // never disagree.
    // Digits out of whatever was typed, so a quoted "HD-0042" finds ticket 42. Anything
    // with no digits, or a number too large to be a reference, simply does not match on
    // this clause and the text search still runs.
    private static int? ParseReference(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        string digits = new([.. search.Where(char.IsDigit)]);

        return digits.Length > 0 && int.TryParse(digits, out int reference) ? reference : null;
    }

    private Task<bool> IsAssignableAsync(string userId)
    {
        return dbContext.Users
            .Where(UserQueries.IsAssignable())
            .AnyAsync(u => u.Id == userId);
    }
}
