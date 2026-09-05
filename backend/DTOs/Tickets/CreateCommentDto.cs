namespace backend.DTOs.Tickets;

public sealed record CreateCommentDto
{
    public required string Body { get; init; }
}
