using FluentValidation;

namespace backend.DTOs.Users;

public sealed class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        // An unknown role name is already rejected by JSON binding, which is a 400 before
        // this runs. This catches the other door: a raw integer outside the enum, which
        // binds happily to (UserRole)99.
        RuleFor(x => x.Role)
            .IsInEnum()
            .WithMessage("Role must be one of: User, Moderator, Admin.");

        // Length only. Identity owns the composition rules (upper, lower, digit, symbol)
        // and reports each one it refuses in its own words, so restating them here would
        // mean two sources of truth that could drift apart.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);
    }
}
