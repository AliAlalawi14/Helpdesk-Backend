namespace backend.Entities;

public sealed class TicketComment
{
    public string Id { get; set; } = null!;

    // real FK — a comment belongs to one ticket
    public string TicketId { get; set; } = null!;
    public Ticket Ticket { get; set; } = null!;

    // real FK to the domain user
    public string AuthorId { get; set; } = null!;
    public User Author { get; set; } = null!;

    public string Body { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public static string NewId() => $"cmt_{Guid.CreateVersion7()}";
}