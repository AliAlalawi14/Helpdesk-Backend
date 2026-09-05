
using Microsoft.EntityFrameworkCore;


namespace backend.DTOs.common;



public sealed record PaginationResult<T>
{
    public required List<T> Data { get; init; }
    public required PaginationMeta Pagination { get; init; }

}

public sealed record PaginationMeta
{
    public required int Page { get; init; }
    public required int Limit { get; init; }
    public required int TotalItems { get; init; }
    public required int TotalPages { get; init; }
}
