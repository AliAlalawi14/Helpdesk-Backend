using System.Diagnostics.CodeAnalysis;
using backend.Database;
using backend.DTOs.Categories;
using backend.DTOs.common;
using backend.Entities;
using backend.Extensions;
using backend.Services.Sorting;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(ApplicationDbContext dbContext) : ControllerBase
{
    // list — any authenticated role (users need it to file a ticket)
    [HttpGet]
    public async Task<IActionResult> GetCategories(
        [FromQuery] CategoriesQueryParameters query,
        [FromServices] SortMappingProvider sortMappingProvider)
    {
        if (!sortMappingProvider.ValidateMappings<CategoryDto, Category>(query.Sort))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided sort parameter isn't valid: '{query.Sort}'");
        }

        string? search = query.Search?.Trim();

        IQueryable<Category> categoriesQuery = dbContext.Categories
            // ILike is Postgres' case-insensitive LIKE, same as the ticket search.
            .Where(c => search == null || EF.Functions.ILike(c.Name, $"%{search}%"));

        SortMapping[] sortMappings = sortMappingProvider.GetMappings<CategoryDto, Category>();

        int totalItems = await categoriesQuery.CountAsync();

        List<CategoryDto> data = await categoriesQuery
            .ApplySort(query.Sort, sortMappings, defaultOrderBy: "Name")
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .Select(CategoryQueries.ProjectToDto())
            .ToListAsync();

        var result = new PaginationResult<CategoryDto>
        {
            Data = data,
            Pagination = new PaginationMeta
            {
                Page = query.Page,
                Limit = query.Limit,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)query.Limit)
            }
        };

        return Ok(result);
    }

    // create — admin only
    // CA1862 wants a StringComparison overload, but this comparison lives in an
    // expression tree that EF translates to SQL lower() — the overload it suggests
    // has no SQL translation and would throw at runtime.
    [SuppressMessage("Performance", "CA1862:Use the 'StringComparison' method overloads",
        Justification = "Comparison is translated to SQL by EF Core; StringComparison overloads are untranslatable.")]
    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryDto dto,
        [FromServices] IValidator<CreateCategoryDto> validator)
    {
        ValidationResult validation = await validator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        // guard against duplicate names (case-insensitive)
        string name = dto.Name;
        bool exists = await dbContext.Categories
            .AnyAsync(c => c.Name.ToLower() == name.ToLower());
        if (exists)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"A category named '{dto.Name}' already exists.");
        }

        Category category = dto.ToEntity();
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        CategoryDto result = await dbContext.Categories
            .Where(c => c.Id == category.Id)
            .Select(CategoryQueries.ProjectToDto())
            .FirstAsync();

        return CreatedAtAction(nameof(GetCategories), new { id = category.Id }, result);
    }

    // rename — admin only
    [SuppressMessage("Performance", "CA1862:Use the 'StringComparison' method overloads",
        Justification = "Comparison is translated to SQL by EF Core; StringComparison overloads are untranslatable.")]
    [Authorize(Roles = Roles.Admin)]
    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateCategory(
        string id,
        [FromBody] UpdateCategoryDto dto,
        [FromServices] IValidator<UpdateCategoryDto> validator)
    {
        ValidationResult validation = await validator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        Category? category = await dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "This category no longer exists.");
        }

        // Same duplicate guard as create, but excluding this row — without the
        // `c.Id != id` a no-op rename would 409 against itself.
        string name = dto.Name;
        bool nameTaken = await dbContext.Categories
            .AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower());
        if (nameTaken)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"A category named '{dto.Name}' already exists.");
        }

        category.Name = dto.Name;
        category.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        CategoryDto result = await dbContext.Categories
            .Where(c => c.Id == category.Id)
            .Select(CategoryQueries.ProjectToDto())
            .FirstAsync();

        return Ok(result);
    }

    // delete — admin only, soft (IsDeleted), same as tickets
    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(string id)
    {
        Category? category = await dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "This category no longer exists.");
        }

        // Ticket -> Category is a REQUIRED relationship and Category carries a
        // !IsDeleted query filter, so soft-deleting a category that still has tickets
        // leaves those tickets unable to resolve ticket.Category.Name — they come back
        // with a null name or drop out of the queue entirely. That's the
        // RequiredNavigationWithQueryFilterInteraction warning biting for real, and a
        // 409 is the clean fix. Soft-deleted tickets aren't counted: the Ticket query
        // filter hides them, so no live query can hit the broken join either.
        int inUse = await dbContext.Tickets.CountAsync(t => t.CategoryId == id);
        if (inUse > 0)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"This category is still used by {inUse} ticket(s). Move each of them to another category first, from the ticket page.");
        }

        category.IsDeleted = true;
        category.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return NoContent();
    }
}
