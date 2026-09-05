using backend.Entities;

namespace backend.DTOs.Auth;

public sealed record CurrentUserDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required UserRole Role { get; init; }
    public required bool IsActive { get; init; }
}
