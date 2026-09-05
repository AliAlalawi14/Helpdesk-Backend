namespace backend.Entities;

// Domain user. Peer of Ticket and Category, not an auth object — the credentials
// live in the identity schema and are reached through IdentityId.
public sealed class User
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;

    // role as a column — single source of truth, no AspNetUserRoles join
    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    // links to the IdentityUser that holds the credentials. The only auth-facing
    // field here; swap identity providers and this is the one column that changes
    // meaning.
    public string IdentityId { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // navigation — real relationships now, not loose strings
    public ICollection<Ticket> RequestedTickets { get; set; } = new List<Ticket>();
    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();

    public static string NewId() => $"usr_{Guid.CreateVersion7()}";
}

// Stored as int (EF default), same as TicketStatus/TicketPriority, so sorting by
// role is a plain column sort in severity order: User < Moderator < Admin.
public enum UserRole
{
    User,
    Moderator,
    Admin
}
