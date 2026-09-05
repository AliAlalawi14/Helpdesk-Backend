using backend.Entities;

namespace backend.DTOs.Users;

// An admin creating an account for someone else. There is no self-registration in this
// system, so the role is set by the creator rather than defaulted, and the password is a
// starting credential the account holder is expected to change.
public sealed record CreateUserDto
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required UserRole Role { get; init; }
    public required string Password { get; init; }
}
