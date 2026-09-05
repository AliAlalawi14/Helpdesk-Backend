using System.Linq.Expressions;
using backend.Entities;

namespace backend.DTOs.Users;

internal static class UserQueries
{
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
