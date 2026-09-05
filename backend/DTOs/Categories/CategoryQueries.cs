using System.Linq.Expressions;
using backend.Entities;

namespace backend.DTOs.Categories;

internal static class CategoryQueries
{
    public static Expression<Func<Category, CategoryDto>> ProjectToDto()
    {
        return category => new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            CreatedAt = category.CreatedAt,
            TicketCount = category.Tickets.Count
        };
    }
}
