using backend.Entities;

namespace backend.DTOs.Users;

// Partial update — same semantics as UpdateTicketDto: only the fields the
// caller actually sends are changed.
public sealed record UpdateUserDto
{
    public UserRole? Role { get; init; }    // provided = change role
    public bool? IsActive { get; init; }    // provided = activate / deactivate
}
