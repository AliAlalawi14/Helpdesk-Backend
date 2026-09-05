namespace backend.DTOs.Tickets;

public sealed record TicketCommentDto
{
    public required string Id { get; init; }
    public required string Body { get; init; }
    public required string AuthorId { get; init; }
    public required string AuthorName { get; init; }
    public required DateTime CreatedAt { get; init; }
}
