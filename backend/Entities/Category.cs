namespace backend.Entities;

public sealed class Category
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

    public static string NewId() => $"cat_{Guid.CreateVersion7()}";

}