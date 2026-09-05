namespace backend.DTOs.Categories;

public sealed record CategoryDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required DateTime CreatedAt { get; init; }

    // Counted server-side so the category screen costs one request instead of one
    // per row. The Ticket !IsDeleted query filter applies to this navigation, so the
    // number always agrees with the DELETE endpoint's in-use check - otherwise the UI
    // would offer a delete the API then refuses.
    public required int TicketCount { get; init; }
}
