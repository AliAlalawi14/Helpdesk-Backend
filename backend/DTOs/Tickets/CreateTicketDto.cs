using backend.Entities;

namespace backend.DTOs.Tickets;

public sealed record CreateTicketDto
{
    public required string Subject { get; init; }
    public required string Description { get; init; }
    public required TicketPriority Priority { get; init; }
    public required string CategoryId { get; init; }
}
