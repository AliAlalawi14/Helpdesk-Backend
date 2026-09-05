namespace backend.DTOs.Categories;

public sealed record CreateCategoryDto
{
    public required string Name { get; init; }
}
