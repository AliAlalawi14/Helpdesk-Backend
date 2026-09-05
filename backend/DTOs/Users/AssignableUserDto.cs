using backend.Entities;

namespace backend.DTOs.Users;

// What a moderator may know about a colleague: enough to hand them a ticket. The full
// UserDto (email, active flag, join date) stays admin-only.
public sealed record AssignableUserDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required UserRole Role { get; init; }
}
