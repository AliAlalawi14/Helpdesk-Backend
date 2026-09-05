using backend.Entities;
using backend.Services.Sorting;

namespace backend.DTOs.Categories;

internal static class CategoryMappings
{
    // Sorting runs before projection here (unlike UserMappings), so the pair is
    // <CategoryDto, Category> — the same shape as the ticket mapping.
    public static readonly SortMappingDefinition<CategoryDto, Category> SortMapping = new()
    {
        Mappings =
        [
            new SortMapping(nameof(CategoryDto.Name), nameof(Category.Name)),
            new SortMapping(nameof(CategoryDto.CreatedAt), nameof(Category.CreatedAt))
        ]
    };

    public static Category ToEntity(this CreateCategoryDto dto)
    {
        DateTime now = DateTime.UtcNow;
        return new Category
        {
            Id = Category.NewId(),
            Name = dto.Name,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
