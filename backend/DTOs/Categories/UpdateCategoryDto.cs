namespace backend.DTOs.Categories;

// Rename only. Name is the sole editable field — Id is the stable handle the
// tickets point at, and the timestamps are server-owned.
public sealed record UpdateCategoryDto
{
    public required string Name { get; init; }
}
