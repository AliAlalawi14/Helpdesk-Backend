using backend.Entities;
using backend.Services.Sorting;

namespace backend.DTOs.Users;

internal static class UserMappings
{
    // <UserDto, User> now — the same shape as the ticket mapping. Sorting runs
    // before projection again, because every sortable field is a real column;
    // Role used to exist only after a join, which is what forced the odd
    // <UserDto, UserDto> pair before.
    public static readonly SortMappingDefinition<UserDto, User> SortMapping = new()
    {
        Mappings =
        [
            new SortMapping(nameof(UserDto.Name), nameof(User.Name)),
            new SortMapping(nameof(UserDto.Email), nameof(User.Email)),
            new SortMapping(nameof(UserDto.Role), nameof(User.Role)),
            new SortMapping(nameof(UserDto.IsActive), nameof(User.IsActive)),
            new SortMapping(nameof(UserDto.CreatedAt), nameof(User.CreatedAt))
        ]
    };
}
