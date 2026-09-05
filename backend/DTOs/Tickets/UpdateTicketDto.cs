using backend.Entities;

namespace backend.DTOs.Tickets;

public sealed record UpdateTicketDto
{
    public TicketStatus? Status { get; init; }
    public TicketPriority? Priority { get; init; }
    public string? AssigneeId { get; init; }   // provided = assign/reassign; omitted = leave as-is

    // Recategorising is triage, not administration. Without it a ticket filed under
    // the wrong category is stuck there forever, and the DELETE 409's instruction to
    // "reassign them before removing it" describes something no endpoint could do.
    public string? CategoryId { get; init; }
}
