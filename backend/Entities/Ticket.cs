namespace backend.Entities;

public sealed class Ticket
{
    public string Id { get; set; } = null!;

    public string Subject { get; set; } = null!;
    public string Description { get; set; } = null!;

    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }

    // real FK — every ticket has a category
    public string CategoryId { get; set; } = null!;
    public Category Category { get; set; } = null!;

    // real FKs to the domain user — two navigations onto the same table, so the
    // config has to name each foreign key explicitly (EF can't infer which is which)
    public string RequesterId { get; set; } = null!;
    public User Requester { get; set; } = null!;

    public string? AssigneeId { get; set; }        // null = "unassigned"
    public User? Assignee { get; set; }

    // Set when the ticket reaches a terminal state, cleared when it is reopened.
    // Resolution time must NOT be derived from UpdatedAt: any PATCH — a priority
    // change, a reassignment — bumps that, so a ticket resolved on Monday and
    // touched on Friday would report a five-day resolution.
    public DateTime? ResolvedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // navigation
    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();

    public static string NewId() => $"tkt_{Guid.CreateVersion7()}";
}
public enum TicketStatus { Open, InProgress, Resolved, Closed }

public enum TicketPriority { Low, Medium, High, Urgent }