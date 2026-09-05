using backend.Entities;

namespace backend.DTOs.Tickets;

public sealed record TicketDetailDto
{
    public required string Id { get; init; }
    public required string Subject { get; init; }
    public required string Description { get; init; }
    public required TicketStatus Status { get; init; }
    public required TicketPriority Priority { get; init; }
    public required string CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required string RequesterId { get; init; }
    public required string RequesterName { get; init; }
    public string? AssigneeId { get; init; }
    public string? AssigneeName { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required List<TicketCommentDto> Comments { get; init; }
}
