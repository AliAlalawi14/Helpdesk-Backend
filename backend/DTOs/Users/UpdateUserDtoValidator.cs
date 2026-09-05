using FluentValidation;

namespace backend.DTOs.Users;

public sealed class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
{
    public UpdateUserDtoValidator()
    {
        // An unknown role *name* ("superadmin") is already rejected by JSON binding,
        // which is a 400 before this runs. This catches the other door: a raw integer
        // outside the enum, which binds happily to (UserRole)99.
        RuleFor(x => x.Role!.Value)
            .IsInEnum()
            .WithMessage("Role must be one of: User, Moderator, Admin.")
            .When(x => x.Role is not null);
    }
}
