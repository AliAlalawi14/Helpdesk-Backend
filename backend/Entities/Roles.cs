namespace backend.Entities;

// The claim-string side of UserRole.
//
// These have to stay as const strings: [Authorize(Roles = ...)] is an attribute
// argument, so it can't take an enum, and the check is an exact string match
// against the role claim. UserRole is the column; this is the wire value.
public static class Roles
{
    public const string User = "user";
    public const string Moderator = "moderator";
    public const string Admin = "admin";

    // The switch is exhaustive and has no default arm on purpose: adding a member
    // to UserRole breaks the build here rather than silently emitting a claim no
    // [Authorize] attribute matches, which would fail closed and lock people out.
    public static string ToClaimValue(this UserRole role) => role switch
    {
        UserRole.User => User,
        UserRole.Moderator => Moderator,
        UserRole.Admin => Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unmapped role.")
    };
}
