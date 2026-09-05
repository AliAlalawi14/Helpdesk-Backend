using Microsoft.AspNetCore.Identity;

namespace backend.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    // the IDENTITY user id, not the domain one: a refresh token belongs to the
    // credential, so it lives and dies with the identity record.
    public required string UserId { get; set; }
    public required string Token { get; set; }
    public required DateTime ExpiresAtUtc { get; set; }

    public IdentityUser User { get; set; } = null!;
}
