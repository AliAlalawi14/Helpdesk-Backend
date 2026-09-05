using backend.Entities;

namespace backend.DTOs.Users;

// Role is a column on the domain user now, not a join-table lookup.
// Serialised as a string ("Admin") by the global JsonStringEnumConverter.
public sealed record UserDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required UserRole Role { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
}
