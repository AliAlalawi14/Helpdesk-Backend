using System.Linq.Expressions;
using backend.Entities;

namespace backend.DTOs.Users;

internal static class UserQueries
{
    // The one definition of who may hold a ticket: an active moderator or admin.
    //
    // It lives here because three callers need it — the picker a moderator chooses from,
    // the agent workload table, and the guard that checks what they picked. Written inline
    // at each, the third copy is the one that eventually drifts, and drift here means the
    // UI offers a name the API then refuses.
    public static Expression<Func<User, bool>> IsAssignable()
    {
        return user => user.IsActive
            && (user.Role == UserRole.Moderator || user.Role == UserRole.Admin);
    }

    // Was a three-table left join across the Identity schema to resolve a role.
    // With Role as a column it's an ordinary projection, like the ticket one.
    public static Expression<Func<User, UserDto>> ProjectToDto()
    {
        return user => new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };
    }
}
