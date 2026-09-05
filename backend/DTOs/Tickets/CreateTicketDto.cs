using backend.Entities;

namespace backend.DTOs.Tickets;

public sealed record CreateTicketDto
{
    public required string Subject { get; init; }
    public required string Description { get; init; }
    public required TicketPriority Priority { get; init; }
    public required string CategoryId { get; init; }

    // Optional, and staff only. Someone raising a ticket for themselves leaves it out and
    // the ticket starts unassigned; a moderator who already knows the owner names them here
    // instead of creating and then patching. The requester is never client-supplied either
    // way — that stays the caller.
    public string? AssigneeId { get; init; }
}
