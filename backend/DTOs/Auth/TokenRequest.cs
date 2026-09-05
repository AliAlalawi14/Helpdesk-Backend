namespace backend.DTOs.Auth;

public sealed record TokenRequest(string UserId, string Email, IEnumerable<string> Roles);
