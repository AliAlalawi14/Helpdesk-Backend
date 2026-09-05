using backend.Entities;
using backend.Services.Sorting;

namespace backend.DTOs.Tickets;

internal static class TicketMappings
{
    public static readonly SortMappingDefinition<TicketDto, Ticket> SortMapping = new()
    {
        Mappings =
        [
            new SortMapping(nameof(TicketDto.Subject), nameof(Ticket.Subject)),
            new SortMapping(nameof(TicketDto.Status), nameof(Ticket.Status)),
            new SortMapping(nameof(TicketDto.Priority), nameof(Ticket.Priority)),
            new SortMapping(nameof(TicketDto.CreatedAt), nameof(Ticket.CreatedAt)),
            new SortMapping(nameof(TicketDto.UpdatedAt), nameof(Ticket.UpdatedAt))
        ]
    };

    public static Ticket ToEntity(this CreateTicketDto dto, string requesterId, string? assigneeId)
    {
        DateTime now = DateTime.UtcNow;
        return new Ticket
        {
            Id = Ticket.NewId(),
            Subject = dto.Subject,
            Description = dto.Description,
            Status = TicketStatus.Open,        // server-decided
            Priority = dto.Priority,
            CategoryId = dto.CategoryId,
            RequesterId = requesterId,         // the caller, never client-supplied
            AssigneeId = assigneeId,           // null unless staff named an owner
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static TicketComment ToEntity(this CreateCommentDto dto, string ticketId, string authorId)
    {
        return new TicketComment
        {
            Id = TicketComment.NewId(),
            TicketId = ticketId,
            AuthorId = authorId,
            Body = dto.Body,
            CreatedAt = DateTime.UtcNow
        };
    }

    // authorName is passed in rather than read off comment.Author: the entity was
    // just constructed, so the navigation isn't loaded and touching it would be a
    // null ref (or a lazy-load round trip). The caller already has the name.
    public static TicketCommentDto ToDto(this TicketComment comment, string authorName)
    {
        return new TicketCommentDto
        {
            Id = comment.Id,
            Body = comment.Body,
            AuthorId = comment.AuthorId,
            AuthorName = authorName,
            CreatedAt = comment.CreatedAt
        };
    }
}